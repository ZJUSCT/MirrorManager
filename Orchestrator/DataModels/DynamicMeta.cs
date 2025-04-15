using System.Text.Json;

namespace Orchestrator.DataModels;

/// <summary>
/// Dynamic meta info for mirror item. (e.g. size, artifact links, etc.)
/// </summary>
public class DynamicMeta
{
    public long Size { get; set; } = -1;
    public List<MirrorArtifact> Artifacts = [];
    private bool _regularized = false;

    public void Regularize()
    {
        if (_regularized) return;
        _regularized = true;

        if (Size < 0) Size = -1;
    }

    public string ToJson()
    {
        Regularize();
        return JsonSerializer.Serialize(this);
    }

    public static DynamicMeta FromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<DynamicMeta>(json) ?? new DynamicMeta();
        }
        catch (JsonException e)
        {
            return new DynamicMeta();
        }
    }
}

