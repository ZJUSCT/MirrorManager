using Orchestrator.DataModels;
using Orchestrator.Services;

namespace Orchestrator.Tests;

public class MockStateStore : IStateStore
{
    private readonly Dictionary<string, MirrorItemInfo> _mirrorItems = [];
    private List<MirrorItemInfo> _newMirrorItems;

    public MockStateStore(IEnumerable<MirrorItemInfo> mirrorItems)
    {
        _newMirrorItems = mirrorItems.ToList();
    }

    public void Reload()
    {
        _mirrorItems.Clear();
        _newMirrorItems.ForEach(x => _mirrorItems[x.Config.Id] = new MirrorItemInfo(x));
    }

    public IEnumerable<KeyValuePair<string, MirrorItemInfo>> GetMirrorItemInfos()
    {
        return _mirrorItems.ToDictionary(kv => kv.Key, kv => new MirrorItemInfo(kv.Value));
    }

    public MirrorItemInfo? GetMirrorItemInfoById(string id)
    {
        throw new NotImplementedException();
    }

    public void SetMirrorInfo(SavedInfo info)
    {
        var item = _mirrorItems.FirstOrDefault(x => x.Key == info.Id);
        if (item.Key == null) return;

        item.Value.SavedInfo = info;
    }

    public void SetMirrorItems(IEnumerable<MirrorItemInfo> items)
    {
        _newMirrorItems = items.ToList();
    }
}
