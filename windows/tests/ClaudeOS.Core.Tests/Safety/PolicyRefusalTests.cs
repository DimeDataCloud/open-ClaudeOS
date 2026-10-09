using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Safety;

/// <summary>The refusals that keep a plan from doing something nonsensical or oversized to the workspace.</summary>
public sealed class PolicyRefusalTests : IDisposable
{
    private readonly TestWorkspace _ws = new();
    private readonly Policy _policy = new() { MaxWriteBytes = 1000 };

    public void Dispose() => _ws.Dispose();

    private Assessment Assess(PlanAction action) => _policy.Assess(action, new Overlay(_ws.Root));

    [Fact]
    public void A_write_over_the_size_limit_is_refused_and_one_at_the_limit_is_not()
    {
        Assert.Contains("write limit", Assess(new WriteFile("big.txt", new string('x', 1001))).Denied);
        Assert.Null(Assess(new WriteFile("ok.txt", new string('x', 1000))).Denied);
    }

    [Fact]
    public void The_limit_counts_bytes_not_characters()
    {
        Assert.Contains("write limit", Assess(new WriteFile("wide.txt", new string('é', 501))).Denied);
    }

    [Fact]
    public void A_file_cannot_be_written_onto_a_folder()
    {
        Assert.Contains("is a directory", Assess(new WriteFile("invoices", "x")).Denied);
    }

    [Fact]
    public void Deleting_or_moving_what_is_not_there_is_refused()
    {
        Assert.Contains("does not exist", Assess(new DeleteFile("ghost.txt")).Denied);
        Assert.Contains("does not exist", Assess(new MoveFile("ghost.txt", "new.txt")).Denied);
    }

    [Fact]
    public void A_move_must_name_a_file_destination_not_a_folder()
    {
        Assert.Contains("is a directory", Assess(new MoveFile("notes.md", "invoices")).Denied);
    }

    [Fact]
    public void Overwriting_deleting_and_moving_existing_files_are_medium_risk_with_the_reason_stated()
    {
        var overwrite = Assess(new WriteFile("notes.md", "new"));
        Assert.Equal(Risk.Medium, overwrite.Risk);
        Assert.Contains("overwrites existing file", overwrite.Notes);

        Assert.Equal(Risk.Medium, Assess(new DeleteFile("notes.md")).Risk);

        var move = Assess(new MoveFile("notes.md", "renamed.md"));
        Assert.Equal((Risk.Medium, "moves existing file"), (move.Risk, move.Notes[0]));

        var clobber = Assess(new MoveFile("notes.md", "invoices/a.txt"));
        Assert.Contains("overwrites existing file at destination", clobber.Notes);
    }

    [Fact]
    public void Creating_a_new_file_is_low_risk()
    {
        var created = Assess(new WriteFile("fresh.txt", "hello"));
        Assert.Equal(Risk.Low, created.Risk);
        Assert.Contains("new file", created.Notes);
    }
}
