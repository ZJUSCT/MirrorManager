using Microsoft.Extensions.Logging;
using Orchestrator.DataModels;
using Orchestrator.Services;

namespace Orchestrator.Tests;

public class TestJobQueue
{
    private MirrorItemInfo GenMirrorItem(string id, TimeSpan interval, TimeSpan timeout, MirrorStatus status,
        DateTime lastSyncAt)
    {
        I18NField i18N = new("en", "zh");
        var info = new MirrorInfo { Name = i18N, Description = i18N, Type = SyncType.Sync, Upstream = "", Url = "" };
        return new MirrorItemInfo(new ConfigInfo
            {
                Id = id, Info = info, Sync = new SyncInfo
                {
                    JobName = $"job-{id}", Interval = new IntervalInfo(interval), Timeout = new IntervalInfo(interval),
                    Image = "", Pull = PullStrategy.Always, Volumes = [], Command = [], Environments = []
                }
            },
            new SavedInfo()
            {
                Id = id,
                Status = status,
                LastSyncAt = lastSyncAt,
            }
        );
    }

    private string ToTimeString(DateTime time)
    {
        return $"{time:HH:mm:ss}";
    }

    private void PrintMirrorStatus(IStateStore store)
    {
        foreach (var kv in store.GetMirrorItemInfos())
            Console.WriteLine($"  {kv.Key}: {kv.Value.SavedInfo.Status} LastSyncAt:{ToTimeString(kv.Value.SavedInfo.LastSyncAt)} NextSyncAt:{ToTimeString(kv.Value.NextSyncAt())}");
    }

    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void TestWorkflow()
    {
        List<MirrorItemInfo> itemInfos =
        [
            GenMirrorItem("foo1", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue),
            GenMirrorItem("foo2", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue),
            GenMirrorItem("foo3", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue)
        ];
        var conf = new MockConfiguration();
        var logger = new MockLogger<JobQueue>();
        var stateStore = new MockStateStore(itemInfos);
        var jobQueue = new JobQueue(conf, logger, stateStore);
        jobQueue.CoolDown = TimeSpan.FromMilliseconds(1);

        PrintMirrorStatus(stateStore);

        logger.LogInformation("Get All jobs");
        List<SyncJob> jobs = [];
        bool gotJob;
        SyncJob? job;
        for (var i = 0; i < 3; ++i)
        {
            gotJob = jobQueue.TryGetNewJob("", out job);
            Assert.That(gotJob, Is.True);
            jobs.Add(job!);
            logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
        }

        gotJob = jobQueue.TryGetNewJob("", out _);
        Assert.That(gotJob, Is.False);
        PrintMirrorStatus(stateStore);

        logger.LogInformation("Finish All jobs");
        foreach (var j in jobs) jobQueue.UpdateJobStatus(j.Guid, MirrorStatus.Succeeded);
        PrintMirrorStatus(stateStore);

        Thread.Sleep(1000);
        logger.LogInformation("Before interval, no job should be available");
        // should get no job here
        gotJob = jobQueue.TryGetNewJob("", out _);
        Assert.That(gotJob, Is.False);

        Thread.Sleep(1100);
        // get a job and fail it
        logger.LogInformation("Get a job and fail it");
        gotJob = jobQueue.TryGetNewJob("", out job);
        Assert.That(gotJob, Is.True);
        logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
        PrintMirrorStatus(stateStore);
        logger.LogInformation("Fail the job");
        jobQueue.UpdateJobStatus(job.Guid, MirrorStatus.Failed);
        PrintMirrorStatus(stateStore);

        // make a lost job
        logger.LogInformation("Get a job");
        gotJob = jobQueue.TryGetNewJob("", out job);
        logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
        Assert.That(gotJob, Is.True);
        PrintMirrorStatus(stateStore);

        Thread.Sleep(2100);
        logger.LogInformation("Made it lost");
        PrintMirrorStatus(stateStore);

        // finish all jobs
        jobs.Clear();
        for (var i = 0; i < 3; ++i)
        {
            gotJob = jobQueue.TryGetNewJob("", out job);
            Assert.That(gotJob, Is.True);
            jobs.Add(job!);
            logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
            PrintMirrorStatus(stateStore);
        }

        foreach (var j in jobs) jobQueue.UpdateJobStatus(j.Guid, MirrorStatus.Succeeded);
        PrintMirrorStatus(stateStore);

        // should get no job here
        gotJob = jobQueue.TryGetNewJob("", out _);
        Assert.That(gotJob, Is.False);

        Assert.Pass();
    }

    [Test]
    public void TestJobReload()
    {
        List<MirrorItemInfo> itemInfos =
        [
            GenMirrorItem("foo1", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue),
            GenMirrorItem("foo2", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue),
            GenMirrorItem("foo3", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
                DateTime.MinValue)
        ];
        var conf = new MockConfiguration();
        var logger = new MockLogger<JobQueue>();
        var stateStore = new MockStateStore(itemInfos);
        var jobQueue = new JobQueue(conf, logger, stateStore);
        jobQueue.CoolDown = TimeSpan.FromSeconds(1);

        PrintMirrorStatus(stateStore);

        logger.LogInformation("Get All jobs");
        List<SyncJob> jobs = [];
        bool gotJob;
        SyncJob? job;
        for (var i = 0; i < 3; ++i)
        {
            gotJob = jobQueue.TryGetNewJob("", out job);
            Assert.That(gotJob, Is.True);
            jobs.Add(job!);
            logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
        }

        gotJob = jobQueue.TryGetNewJob("", out _);
        Assert.That(gotJob, Is.False);
        PrintMirrorStatus(stateStore);

        logger.LogInformation("remove foo1, add bar");
        itemInfos[0] = GenMirrorItem("bar", TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(1), MirrorStatus.Unknown,
            DateTime.MinValue);
        stateStore.SetMirrorItems(itemInfos);
        jobQueue.Reload();

        logger.LogInformation("Finish All jobs");
        foreach (var j in jobs) jobQueue.UpdateJobStatus(j.Guid, MirrorStatus.Succeeded);
        PrintMirrorStatus(stateStore);

        Thread.Sleep(2100);
        logger.LogInformation("Get All jobs");
        for (var i = 0; i < 3; ++i)
        {
            gotJob = jobQueue.TryGetNewJob("", out job);
            PrintMirrorStatus(stateStore);
            Assert.That(gotJob, Is.True);
            jobs.Add(job!);
            logger.LogInformation("Got job {jobId} for {mirrorId}", job!.Guid, job.MirrorItem.Config.Id);
        }

        gotJob = jobQueue.TryGetNewJob("", out _);
        Assert.That(gotJob, Is.False);
        PrintMirrorStatus(stateStore);
    }
}
