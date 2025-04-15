namespace Orchestrator.Utils;

public class DbList<T>
{
    public List<T> Value { get; set; } = [];
    public static implicit operator List<T>(DbList<T> db) => db.Value;
    public static implicit operator DbList<T>(List<T> db) => new() { Value = db };
}
