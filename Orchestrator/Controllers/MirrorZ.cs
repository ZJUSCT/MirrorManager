using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Orchestrator.DataModels;
using Orchestrator.Services;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

[ApiController]
[Route("/mirrorz.json")]
[Produces("application/json")]
public class MirrorZInfo(IConfiguration conf, ILogger<MirrorZInfo> log, JobQueue jobQueue) : CustomControllerBase(conf)
{
    /// <summary>
    /// Transforms the status of a mirror item into a string format for MirrorZ.
    /// </summary>
    /// <param name="item">MirrorItemInfo to be transformed.</param>
    /// <returns>MirrorZ status.</returns>
    private string TransformStatus(MirrorItemInfo item)
    {
        if (item.Status == MirrorStatus.Cached) return "C";
        if (item.Status == MirrorStatus.Succeeded)
            return $"S{item.LastSuccessAt.ToUnixTimeSeconds()}X{item.NextSyncAt().ToUnixTimeSeconds()}";
        if (item.Status == MirrorStatus.Syncing)
            return $"Y{item.LastSyncAt.ToUnixTimeSeconds()}O{item.LastSuccessAt.ToUnixTimeSeconds()}";
        if (item.Status == MirrorStatus.Failed)
            return $"F{item.LastSyncAt.ToUnixTimeSeconds()}O{item.LastSuccessAt.ToUnixTimeSeconds()}";
        return "U";
    }

    /// <summary>
    /// Return mirrors' status in mirrorz's format.
    /// </summary>
    /// <returns></returns>
    [HttpGet("")]
    [OutputCache(Duration = 30)]
    public ActionResult<MirrorZData> GetMirrorZData()
    {
        var mirrors = jobQueue.GetMirrorItems();
        var transformedItems = mirrors
            .Select(x => x.Value)
            .Select(x => new MirrorZMirrorItem(
                x.Config.Id,
                x.Config.Info.Description.Zh,
                x.Config.Info.Url,
                TransformStatus(x),
                $"/docs/{x.Config.Id}",
                x.Config.Info.Upstream,
                "0"
            )).ToList();

        return new MirrorZData(
            1.7,
            MirrorZStatic.SiteInfo,
            new List<MirrorZCatItem>(),
            transformedItems,
            "D",
            MirrorZStatic.EndpointInfos);
    }
}
