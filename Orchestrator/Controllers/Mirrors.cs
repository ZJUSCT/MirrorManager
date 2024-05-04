﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Orchestrator.DataModels;
using Orchestrator.Services;
using Orchestrator.Utils;

namespace Orchestrator.Controllers;

[ApiController]
[Route("/mirrors")]
public partial class Mirrors(IConfiguration conf, JobQueue jobQueue) : CustomControllerBase(conf)
{
    private static string StatusToString(MirrorStatus status)
    {
        return status switch
        {
            MirrorStatus.Succeeded => "succeeded",
            MirrorStatus.Syncing => "syncing",
            MirrorStatus.Failed => "failed",
            MirrorStatus.Cached => "cached",
            _ => "unknown"
        };
    }

    [HttpGet("")]
    [OutputCache(Duration = 30)]
    public ActionResult<IList<MirrorItemDto>> GetAllMirrors()
    {
        var mirrors = jobQueue.GetMirrorItems();
        return Ok(mirrors
            .Select(x => x.Value)
            .Select(x => new MirrorItemDto(x)));
    }

    [HttpGet("{id}")]
    [OutputCache(Duration = 30)]
    public ActionResult<MirrorItemDto> GetMirrorById([FromRoute] string id)
    {
        var mirror = jobQueue.GetMirrorItemById(id);
        return mirror == null ? NotFound() : Ok(new MirrorItemDto(mirror));
    }

    [HttpGet("lastActive")]
    public ActionResult<long> GetLastActiveTime() => Ok(jobQueue.LastActive.ToUnixTimeSeconds());

    public record MirrorItemDto(
        string Id,
        string Url,
        I18NField Name,
        I18NField Desc,
        string Upstream,
        string Size,
        string Status,
        string LastUpdated,
        string NextScheduled,
        string LastSuccess)
    {
        public MirrorItemDto(MirrorItemInfo item) : this(
            item.Config.Id,
            item.Config.Info.Url,
            item.Config.Info.Name,
            item.Config.Info.Description,
            item.Config.Info.Upstream,
            FileSizeUtil.ToString(item.Size),
            StatusToString(item.Status),
            item.LastSyncAt.ToUnixTimeSeconds().ToString(),
            item.NextSyncAt().ToUnixTimeSeconds().ToString(),
            item.LastSuccessAt.ToUnixTimeSeconds().ToString()
            )
        {
        }
    }
}