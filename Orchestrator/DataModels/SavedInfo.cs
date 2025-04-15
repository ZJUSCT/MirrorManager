using System.ComponentModel.DataAnnotations;
using Orchestrator.Utils;

namespace Orchestrator.DataModels;

/// <summary>
/// A saved info is a record of the last sync status.
/// Currently, we only store what is necessary for a refresh startup.
/// Job queue is not saved for simplicity.
/// </summary>
public class SavedInfo
{
    [Key] [MaxLength(42)] public required string Id { get; set; }
    public MirrorStatus Status { get; set; }
    public DateTime LastSyncAt { get; set; }
    public DateTime LastSuccessAt { get; set; }
    public long Size { get; set; } = -1;
    public DbList<MirrorArtifact> Artifacts { get; set; } = new();
}

/// <summary>
/// Mirror artifact item. (e.g. ISO file, EXE file, etc.)
/// </summary>
/// <param name="Priority">Small priority items should be displayed first.</param>
/// <param name="Name">Friendly name.</param>
/// <param name="Url">URL Path starting with "/".</param>
/// <param name="Description">Artifact description.</param>
public record MirrorArtifact(int Priority, string Name, string Url, string Description) : IComparable<MirrorArtifact>
{
    public bool Regularize()
    {
        if (string.IsNullOrWhiteSpace(Url) || !Url.StartsWith('/') || Url.Contains('?') || Url.Contains('#'))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name)) return false;

        return true;
    }

    public int CompareTo(MirrorArtifact? other)
    {
        if (other is null) return 1;

        var r = Priority.CompareTo(other.Priority);
        return r != 0 ? r : string.Compare(Name, other.Name, StringComparison.Ordinal);
    }
}
