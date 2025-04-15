using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Orchestrator.DataModels;
using Orchestrator.Utils;

namespace Orchestrator.Services;

/// <summary>
/// The service that manages all runtime information.
///                               UpdateJobStatus()
///             ┌─────────────────────────────────────────────────────┐
///             │                                                     │
///             ├──────────────────────────────┐                      │
///           ┌─▼────────────┐                 │   ┌───────────────┐  │
///  Reload() │Status: ---   │ TryGetNewJob()  │   │Status: Syncing│  │
/// ─────────►│Stale: false  ├─────────────────┴──►│Stale: false   ┼──┘
///           │Queue: Pending│ check for           │Queue: Syncing │
///           └─┬────────────┘ .TaskShouldStartAt  └─┬─────────────┘
///             │                                    │
///             │ Reload()                           │ Reload()
///           ┌─▼────────────┐  CheckLostJobs()    ┌─▼─────────────┐
///           │(Discarded)   │◄────────────────────┤Status: Syncing│
///           └──────────────┘  UpdateJobStatus()  │Stale: true    │
///                                                │Queue: Syncing │
///                                                └───────────────┘
/// </summary>
public class JobQueue
{
    private readonly IConfiguration _conf;
    private readonly ILogger<JobQueue> _log;
    private readonly ConcurrentQueue<SyncJob> _pendingQueue = new();
    private readonly ReaderWriterLockSlim _rwLock = new();
    private readonly IStateStore _stateStore;
    private readonly ConcurrentDictionary<Guid, SyncJob> _syncingDict = new();
    private readonly ConcurrentDictionary<string, byte> _forceRefreshDict = new();
    /// <summary>
    /// Indicates the last time the job queue was active (the last communication with worker) .
    /// </summary>
    public DateTime LastActive { get; private set; } = DateTime.Now;

    public JobQueue(IConfiguration conf, ILogger<JobQueue> log, IStateStore stateStore)
    {
        _conf = conf;
        _log = log;
        _stateStore = stateStore;

        Reload();
    }

    /// <summary>
    /// The time to wait before a finished job to be enqueued again.
    /// </summary>
    public TimeSpan CoolDown { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Reload configs and job queue.
    /// </summary>
    public void Reload()
    {
        // Reload configs from json files
        _stateStore.Reload();

        // Generate job objects from configs
        var syncJobs = _stateStore.GetMirrorItemInfos()
            .Select(x => x.Value)
            .Where(x => x.Config.Info.Type == SyncType.Sync)
            .OrderBy(x => x.NextSyncAt())
            .Select(x => new SyncJob(x, x.Config.Sync!.Interval.GetNextSyncTime(x.LastSyncAt)));

        using var guard = new ScopeWriteLock(_rwLock);
        // Remove all pending jobs
        _pendingQueue.Clear();
        // DO NOT clear _syncingDict since workers may still be working on them
        // and we need to update their status
        _syncingDict.ForEach(x => x.Value.Stale = true);

        // Enqueue new jobs
        syncJobs.ForEach(x => _pendingQueue.Enqueue(x));
    }

    public IEnumerable<KeyValuePair<string, MirrorItemInfo>> GetMirrorItems()
    {
        return _stateStore.GetMirrorItemInfos();
    }

    public MirrorItemInfo? GetMirrorItemById(string id)
    {
        return _stateStore.GetMirrorItemInfoById(id);
    }

    public (int pendingCount, int syncingCount) GetQueueStatus()
    {
        return (_pendingQueue.Count, _syncingDict.Count);
    }

    public (List<SyncJob> pendingJobs, List<SyncJob> syncingJobs) GetJobs()
    {
        using var _ = new ScopeReadLock(_rwLock);
        var pendingJobs = _pendingQueue.ToList();
        var syncingJobs = _syncingDict.Values.ToList();
        return (pendingJobs, syncingJobs);
    }

    /// <summary>
    /// Find jobs whose worker may have died and do cleanup.
    /// </summary>
    private void CheckLostJobs()
    {
        // we need to acquire a write lock here
        // in case other threads will read or write the dictionary
        List<SyncJob> jobs = [];
        using (var guard = new ScopeWriteLock(_rwLock))
        {
            foreach (var (guid, job) in _syncingDict)
            {
                var taskStartedAt = job.TaskStartedAt;
                var timeout = job.MirrorItem.Config.Sync!.Timeout.IntervalFree!.Value;

                // if timeout
                if (taskStartedAt.Add(timeout).Add(CoolDown) < DateTime.Now)
                {
                    // remove from syncing dict
                    jobs.Add(job);
                    _syncingDict.Remove(guid, out _);
                }
            }
        }

        // log and re-enqueue the job
        foreach (var job in jobs)
        {
            _log.LogWarning("Job {guid}({id}) took too long, marking as failed", job.Guid,
                job.MirrorItem.Config.Id);
            job.MirrorItem.LastSyncAt = job.TaskStartedAt;
            _stateStore.SetMirrorInfo(MirrorStatus.Failed, job.MirrorItem);

            if (job.Stale) continue;

            var newJob = new SyncJob(job)
            {
                TaskShouldStartAt = DateTime.Now
            };
            _pendingQueue.Enqueue(newJob);
        }
    }

    /// <summary>
    /// Set the job's TaskShouldStartAt to Now. <seealso cref="TryGetNewJob"/>
    /// </summary>
    public void ForceRefresh(string mirrorId)
    {
        if (string.IsNullOrWhiteSpace(mirrorId))
        {
            _forceRefreshDict.Clear();
        }
        else
        {
            _forceRefreshDict[mirrorId] = 0;
        }
    }

    /// <summary>
    /// Get a new job for the worker, if available.
    /// </summary>
    public bool TryGetNewJob(in string workerId, [MaybeNullWhen(false)] out SyncJob job)
    {
        LastActive = DateTime.Now;
        // worker should report a fail job if time exceeds limit
        // so we assume the worker has encountered an error when timeout
        CheckLostJobs();

        using var _ = new ScopeReadLock(_rwLock);

        // deque a job from the queue
        var hasJob = _pendingQueue.TryDequeue(out job);
        if (!hasJob) return false;

        // check for start time
        if (job!.TaskShouldStartAt > DateTime.Now)
        {
            // if the task is not ready
            if (!_forceRefreshDict.TryRemove(job.MirrorItem.Config.Id, out var _))
            {
                // enqueue the item again
                _pendingQueue.Enqueue(job);
                return false;
            }

            // or the task was force refreshed, start it now
            job.TaskShouldStartAt = DateTime.Now;
        }

        job.TaskStartedAt = DateTime.Now;
        job.WorkerId = workerId;
        job.MirrorItem.LastSyncAt = DateTime.Now;
        _syncingDict[job.Guid] = job;
        _stateStore.SetMirrorInfo(MirrorStatus.Syncing, job.MirrorItem);
        return true;
    }

    public void UpdateJobStatus(Guid guid, MirrorStatus status)
    {
        LastActive = DateTime.Now;
        using var guard = new ScopeReadLock(_rwLock);
        if (!_syncingDict.TryRemove(guid, out var job))
        {
            _log.LogWarning("Job not found: {guid}", guid);
            return;
        }

        if (status != MirrorStatus.Failed && status != MirrorStatus.Succeeded)
        {
            _log.LogWarning("Unsupported job status: {status}", status);
            _syncingDict[guid] = job;
            return;
        }

        if (status == MirrorStatus.Succeeded)
        {
            job.MirrorItem.LastSuccessAt = DateTime.Now;
        }

        _stateStore.SetMirrorInfo(status, job.MirrorItem);

        if (job.Stale) return;
        _pendingQueue.Enqueue(new SyncJob(job));
    }
}
