using System.ComponentModel.DataAnnotations;

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
    public ulong Size { get; set; }
}
