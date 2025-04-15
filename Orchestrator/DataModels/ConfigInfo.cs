using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Orchestrator.Utils;

namespace Orchestrator.DataModels;

public enum SyncType
{
    Sync,
    Cached,
    Other
}

public enum PullStrategy
{
    Always,
    IfNotExists,
    Never
}

public enum IntervalType
{
    /// <summary>
    /// Calculate based on the last sync time.
    /// </summary>
    Free,
    /// <summary>
    /// Sync after the specified time.
    /// TODO: Not implemented
    /// </summary>
    Fixed
}

[method: JsonConstructor]
public record I18NField(string En, string Zh);

/// <summary>
/// Raw JSON object for mirror info.
/// </summary>
[method: JsonConstructor]
public record MirrorInfoRaw(I18NField Name, I18NField Description, string Type, string Upstream, string Url);

/// <summary>
/// Mirror info.
/// All fields are unrelated to sync, except <c>Type</c> which controls
/// if the mirror should be added to sync queue.
/// <seealso cref="SyncType"/>
/// </summary>
public class MirrorInfo
{
    public MirrorInfo()
    {
    }

    [SetsRequiredMembers]
    public MirrorInfo(MirrorInfoRaw raw)
    {
        Name = raw.Name;
        Description = raw.Description;
        Type = raw.Type switch
        {
            "sync" => SyncType.Sync,
            "cached" => SyncType.Cached,
            _ => SyncType.Other
        };
        Upstream = raw.Upstream;
        Url = raw.Url;
    }

    public required I18NField Name { get; init; }
    public required I18NField Description { get; init; }
    public SyncType Type { get; init; }

    public required string Upstream { get; init; }
    public required string Url { get; init; }
}

[method: JsonConstructor]
public record VolumeInfo(string Src, string Dst, bool ReadOnly);

public record IntervalInfoRaw(string Type, string Value);

/// <summary>
/// Calculates the next sync time based on the last sync time.
/// <seealso cref="IntervalType"/>
/// </summary>
public partial class IntervalInfo
{
    [GeneratedRegex(@"(\d+)s")]
    private static partial Regex SecRegex();

    [GeneratedRegex(@"(\d+)m")]
    private static partial Regex MinRegex();

    [GeneratedRegex(@"(\d+)h")]
    private static partial Regex HourRegex();

    [GeneratedRegex(@"(\d+)d")]
    private static partial Regex DayRegex();

    [GeneratedRegex(@"(\d+)w")]
    private static partial Regex WeekRegex();

    [GeneratedRegex(@"(\d+)M")]
    private static partial Regex MonthRegex();

    [GeneratedRegex(@"(\d+)y")]
    private static partial Regex YearRegex();

    private static readonly List<(string Abbr, Regex Regex, TimeSpan Duration)> _durationMap =
    [
        ("s", SecRegex(), TimeSpan.FromSeconds(1)),
        ("m", MinRegex(), TimeSpan.FromMinutes(1)),
        ("h", HourRegex(), TimeSpan.FromHours(1)),
        ("d", DayRegex(), TimeSpan.FromDays(1)),
        ("w", WeekRegex(), TimeSpan.FromDays(7)),
        ("M", MonthRegex(), TimeSpan.FromDays(30)),
        ("y", YearRegex(), TimeSpan.FromDays(365))
    ];

    public IntervalType Type { get; init; }
    public TimeSpan? IntervalFree { get; }

    public IntervalInfo(string freeInterval)
    {
        Type = IntervalType.Free;
        IntervalFree = _durationMap
            .Where(x => freeInterval.Contains(x.Abbr))
            .Select(x => x.Duration * int.Parse(x.Regex.Match(freeInterval).Groups[1].Value))
            .Aggregate((acc, sum) => acc + sum);
    }

    public IntervalInfo(TimeSpan freeInterval)
    {
        Type = IntervalType.Free;
        IntervalFree = freeInterval;
    }
    
    public IntervalInfo(IntervalInfoRaw raw)
    {
        if (raw.Type == "free")
        {
            Type = IntervalType.Free;
            IntervalFree = _durationMap
                .Where(x => raw.Value.Contains(x.Abbr))
                .Select(x => x.Duration * int.Parse(x.Regex.Match(raw.Value).Groups[1].Value))
                .Aggregate((acc, sum) => acc + sum);
        }
        else
        {
            throw new NotImplementedException();
        }
    }

    public DateTime GetNextSyncTime(DateTime lastSyncAt)
    {
        if (Type == IntervalType.Free)
        {
            return lastSyncAt.Add(IntervalFree!.Value);
        }

        throw new NotImplementedException();
    }
}

/// <summary>
/// Raw JSON object for sync info.
/// </summary>
[method: JsonConstructor]
public record SyncInfoRaw(
    string JobName,
    IntervalInfoRaw Interval,
    string Timeout,
    string Image,
    string Pull,
    List<VolumeInfo> Volumes,
    List<string> Command,
    List<string> Environments);

/// <summary>
/// Sync info for a mirror.
/// </summary>
public class SyncInfo
{
    public SyncInfo()
    {
    }

    [SetsRequiredMembers]
    public SyncInfo(SyncInfoRaw raw)
    {
        JobName = raw.JobName;
        Interval = new IntervalInfo(raw.Interval);
        Timeout = new IntervalInfo(raw.Timeout);
        Image = raw.Image;
        Pull = raw.Pull switch
        {
            "always" => PullStrategy.Always,
            "ifNotExists" => PullStrategy.IfNotExists,
            "never" => PullStrategy.Never,
            _ => PullStrategy.Never
        };
        Volumes = raw.Volumes;
        Command = raw.Command;
        Environments = raw.Environments;
    }

    public required string JobName { get; init; }
    public required IntervalInfo Interval { get; init; }
    public required IntervalInfo Timeout { get; init; }
    public required string Image { get; init; }
    public PullStrategy Pull { get; init; }
    public required List<VolumeInfo> Volumes { get; init; }
    public required List<string> Command { get; init; }
    public required List<string> Environments { get; init; }
}

/// <summary>
/// Raw JSON object for config info.
/// </summary>
[method: JsonConstructor]
public record ConfigInfoRaw(string Id, MirrorInfoRaw Info, SyncInfoRaw? Sync);

/// <summary>
/// A config info is a mirror info with an optional sync info.
/// </summary>
public class ConfigInfo
{
    public ConfigInfo()
    {
    }

    [SetsRequiredMembers]
    public ConfigInfo(ConfigInfoRaw rawConf)
    {
        Id = rawConf.Id;
        Info = new MirrorInfo(rawConf.Info);
        if (rawConf.Sync != null) Sync = new SyncInfo(rawConf.Sync);
    }

    [SetsRequiredMembers]
    public ConfigInfo(ConfigInfo conf)
    {
        Id = conf.Id;
        Info = conf.Info;
        Sync = conf.Sync;
    }

    public required string Id { get; init; }
    public required MirrorInfo Info { get; init; }
    public SyncInfo? Sync { get; init; }

}
