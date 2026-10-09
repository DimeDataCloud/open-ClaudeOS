using ClaudeOS.Core.Safety;

namespace ClaudeOS.Cli;

/// <summary>`claudeos do`: plan with Claude, then the same review loop as `apply`.</summary>
internal static class Do
{
    public static Task<int> RunAsync(Options opts, StateDir state) =>
        Task.FromResult(Cli.Fail("`do` needs the Claude planner, which is wired in the next step."));
}
