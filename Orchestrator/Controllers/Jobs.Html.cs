using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Orchestrator.DataModels;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

using static MiniHtmlGenerator;

public partial class Jobs
{
    /// <summary>
    /// Job details in human-readable format.
    /// </summary>
    [HttpGet("html")]
    [Produces("text/html")]
    public ActionResult<string> GetJobsHtml([FromQuery] string token)
    {
        if (token != Conf["WorkerToken"])
        {
            log.LogInformation("Unauthorized request to /jobs from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        var (pendingJobs, syncingJobs) = jobQueue.GetJobs();
        var jobs = (pendingJobs as IEnumerable<SyncJob>).Concat(syncingJobs);

        return Ok(PlainPage(
            "Jobs overview", [
                Table([
                    THead([
                        Th("Job ID"),
                        Th("Worker ID"),
                        Th("Started"),
                        Th("Schedule"),
                        Th("Stale?"),
                        Th("Mirror Item")
                    ]),
                    TBody(jobs.OrderBy(x => x.TaskShouldStartAt).Select(x => Tr([
                        Th(x.Guid.ToString()),
                        Th(x.WorkerId),
                        Th(x.TaskStartedAt.ToString(CultureInfo.InvariantCulture)),
                        Th(x.TaskShouldStartAt.ToString(CultureInfo.InvariantCulture)),
                        Th(x.Stale.ToString()),
                        Th(x.MirrorItem.Config.Id)
                    ])).ToList())
                ]),
            ],
            css: TableCss).ToString());
    }
}
