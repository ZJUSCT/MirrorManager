using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.DataModels;
using Orchestrator.Services;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

/// <summary>
/// Worker API.
/// Request to APIs in this controller should only be allowed in trusted zones.
/// </summary>
[ApiController]
[Route("/jobs")]
public partial class Jobs(IConfiguration conf, ILogger<Jobs> log, JobQueue jobQueue) : CustomControllerBase(conf)
{
    private bool CheckWorkerToken() =>
        Request.Headers.TryGetValue("X-Worker-Token", out var values)
        && values.Contains(Conf["WorkerToken"]);

    /// <summary>
    /// Get all (both pending and syncing) jobs' information.
    /// </summary>
    [HttpGet("")]
    public ActionResult<GetJobsRes> GetJobs()
    {
        if (!CheckWorkerToken())
        {
            log.LogInformation("Unauthorized request to /jobs from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        var (pendingJobs, syncingJobs) = jobQueue.GetJobs();
        return Ok(new GetJobsRes(pendingJobs, syncingJobs));
    }

    /// <summary>
    /// A fetch is a request from a worker to get a new job.
    /// </summary>
    /// <param name="req">For worker ID.</param>
    /// <returns>
    /// If the job queue is not empty, returns the job info and updates the job queue.
    /// Otherwise, an error "NO_PENDING_JOB" is returned.
    /// </returns>
    [HttpPost("fetch")]
    public ActionResult<ResponseDto<FetchRes>> FetchNewJob([FromBody] FetchReq req)
    {
        if (!CheckWorkerToken())
        {
            log.LogInformation("Unauthorized request to /jobs/fetch from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        var hasJob = jobQueue.TryGetNewJob(req.WorkerId, out var job);
        if (!hasJob) return Ok(Error("NO_PENDING_JOB"));

        var syncConfig = job!.MirrorItem.Config.Sync!;
        return Ok(Success(new FetchRes(
            job.Guid,
            syncConfig.JobName,
            syncConfig.Image,
            $"{syncConfig.Timeout.IntervalFree!.Value.Minutes}m",
            syncConfig.Volumes,
            syncConfig.Command,
            syncConfig.Environments
        )));
    }

    /// <summary>
    /// Updates the job status from worker report. See <see cref="JobQueue.UpdateJobStatus"/>.
    /// We intentionally do not check if worker id is valid here for maintenance convenience.
    /// </summary>
    /// <param name="jobId">Job ID in GUID format.</param>
    /// <param name="req">New mirror status.</param>
    [HttpPut("{jobId}")]
    public ActionResult<ResponseDto<object>> UpdateJobStatus([FromRoute] Guid jobId, [FromBody] UpdateReq req)
    {
        if (!CheckWorkerToken())
        {
            log.LogInformation("Unauthorized request to /jobs/fetch from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        jobQueue.UpdateJobStatus(jobId, req.Status);
        return Ok(Success<object>(null));
    }

    /// <summary>
    /// Force refresh a mirror item's status. See <see cref="JobQueue.ForceRefresh"/>.
    /// </summary>
    /// <param name="req">Mirror item ID.</param>
    [HttpPost("forceRefresh")]
    public ActionResult<ResponseDto<object>> ForceRefresh([FromBody] ForceRefreshReq req)
    {
        if (!CheckWorkerToken())
        {
            log.LogInformation("Unauthorized request to /jobs/forceRefresh from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        jobQueue.ForceRefresh(req.MirrorId);
        return Ok(Success<object>(null));
    }

    public record GetJobsRes(
        List<SyncJob> Pending,
        List<SyncJob> Syncing);

    public record FetchReq(string WorkerId);

    public record FetchRes(
        Guid Guid,
        string JobName,
        string Image,
        string Timeout,
        List<VolumeInfo> Volumes,
        List<string> Command,
        List<string> Environments);

    public record UpdateReq(MirrorStatus Status);

    public record ForceRefreshReq(string MirrorId);
}
