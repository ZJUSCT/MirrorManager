using Orchestrator.Utils;

namespace Orchestrator.DataModels;

/// <summary>
/// A mirror item is the config with runtime information.
/// </summary>
public class MirrorItemInfo
{
    public MirrorItemInfo(ConfigInfo config)
    {
        Config = new ConfigInfo(config);
    }

    public MirrorItemInfo(MirrorItemInfo info) : this(info.Config)
    {
        Status = info.Status;
        LastSyncAt = info.LastSyncAt;
        LastSuccessAt = info.LastSuccessAt;
        Size = info.Size;
    }

    public ConfigInfo Config { get; }
    public MirrorStatus Status { get; set; }
    public DateTime LastSyncAt { get; set; }
    public DateTime LastSuccessAt { get; set; }
    public ulong Size { get; set; }

    public DateTime NextSyncAt()
    {
        return Config.Sync == null
            ? DateTimeConstants.UnixEpoch
            : Config.Sync.Interval.GetNextSyncTime(LastSyncAt);
    }
}

public class SyncJob(MirrorItemInfo mirrorItemInfo, DateTime shouldStartAt, string workerId = "")
{
    public Guid Guid { get; } = Guid.NewGuid();
    public MirrorItemInfo MirrorItem { get; } = mirrorItemInfo;
    /// <summary>
    /// A job will be stale if
    /// 1) the job queue is reloaded and
    /// 2) the job is in the syncing queue.
    /// If the job is stale,
    /// the job will be permanently removed from the queue once it finished.
    /// </summary>
    public bool Stale { get; set; } = false;
    /// <summary>
    /// When the task should be started, based on the <see cref="IntervalInfo"/>.
    /// </summary>
    public DateTime TaskShouldStartAt { get; set; } = shouldStartAt;
    /// <summary>
    /// When the task is actually fetched by a worker.
    /// </summary>
    public DateTime TaskStartedAt { get; set; } = DateTimeConstants.UnixEpoch;
    public string WorkerId { get; set; } = workerId;

    public SyncJob(SyncJob job) : this(job.MirrorItem,
        job.MirrorItem.Config.Sync!.Interval.GetNextSyncTime(DateTime.Now))
    {
    }
}
