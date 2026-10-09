using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests;

/// <summary>A throwaway workspace laid out like the Python prototype's fixture: invoices, notes,
/// and some secrets the policy must keep away from the model.</summary>
public sealed class TestWorkspace : IDisposable
{
    public TestWorkspace()
    {
        Base = Path.Combine(Path.GetTempPath(), "claudeos-tests", Guid.NewGuid().ToString("N"));
        Root = Path.Combine(Base, "ws");
        Directory.CreateDirectory(Path.Combine(Root, "invoices"));
        File.WriteAllText(Path.Combine(Root, "invoices", "a.txt"), "Acme Corp\nTotal: 120.00\n");
        File.WriteAllText(Path.Combine(Root, "invoices", "b.txt"), "Globex\nTotal: 80.50\n");
        File.WriteAllText(Path.Combine(Root, "notes.md"), "old notes\n");
        Directory.CreateDirectory(Path.Combine(Root, ".ssh"));
        File.WriteAllText(Path.Combine(Root, ".ssh", "id_ed25519"), "PRIVATE KEY\n");
        File.WriteAllText(Path.Combine(Root, ".env"), "API_TOKEN=secret\n");
        State = new StateDir(Path.Combine(Base, "state"));
    }

    public string Base { get; }

    public string Root { get; }

    public StateDir State { get; }

    public string P(string rel) => Path.Combine(Root, rel.Replace('/', Path.DirectorySeparatorChar));

    public string ReadText(string rel) => File.ReadAllText(P(rel));

    public bool Exists(string rel) => File.Exists(P(rel)) || Directory.Exists(P(rel));

    public Overlay Overlay() => new(Root);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Base, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

[CollectionDefinition("serial", DisableParallelization = true)]
public sealed class SerialCollection;
