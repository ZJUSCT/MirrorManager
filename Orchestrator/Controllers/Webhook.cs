using Microsoft.AspNetCore.Mvc;
using Orchestrator.Services;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

[ApiController]
[Route("/webhook")]
public class Webhook(IConfiguration conf, ILogger<Webhook> log, JobQueue jobQueue) : CustomControllerBase(conf)
{
    private bool CheckWebhookToken() =>
        Request.Headers.TryGetValue("X-Webhook-Token", out var values)
        && values.Contains(Conf["WebhookToken"]);

    /// <summary>
    /// Reload job queue. See <see cref="JobQueue.Reload"/>.
    /// </summary>
    /// <returns>count for pending jobs, syncing jobs and mirror item infos.</returns>
    [HttpPost("reload")]
    public IActionResult Reload()
    {
        if (!CheckWebhookToken())
        {
            log.LogInformation("Unauthorized request to /webhook/reload from {ip}", GetRequestIp());
            return Unauthorized(null);
        }

        jobQueue.Reload();
        var mirrors = jobQueue.GetMirrorItems().Select(x => x.Value);
        var (pending, syncing) = jobQueue.GetQueueStatus();
        log.LogInformation("Configuration reloaded");
        return Ok(Success(new
        {
            pending,
            syncing,
            data = mirrors
        }));
    }
}
