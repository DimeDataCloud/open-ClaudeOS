using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using Anthropic;
using ClaudeOS.Claude;
using ClaudeOS.Core.Actions;
using ClaudeOS.Core.Planning;
using ClaudeOS.Core.Safety;

namespace ClaudeOS.Core.Tests.Planning;

/// <summary>
/// Exercises the real SDK against a local HTTP stand-in for the Messages API, so the adapter's
/// request mapping and response parsing are tested end to end without a key or a network. (It is
/// not a substitute for a live smoke test; see docs/testing.md.)
/// </summary>
public sealed class AnthropicAdapterTests : IDisposable
{
    private readonly HttpListener _listener = new();
    private readonly List<JsonDocument> _bodies = [];
    private readonly Queue<string> _responses = new();
    private readonly string _url;
    private readonly Task _server;
    private readonly TestWorkspace _ws = new();

    public AnthropicAdapterTests()
    {
        var port = Random.Shared.Next(20_000, 40_000);
        _url = $"http://127.0.0.1:{port}/";
        _listener.Prefixes.Add(_url);
        _listener.Start();
        _server = Task.Run(async () =>
        {
            while (_listener.IsListening)
            {
                try
                {
                    var ctx = await _listener.GetContextAsync();
                    using var reader = new StreamReader(ctx.Request.InputStream, Encoding.UTF8);
                    _bodies.Add(JsonDocument.Parse(await reader.ReadToEndAsync()));
                    var payload = Encoding.UTF8.GetBytes(_responses.Dequeue());
                    ctx.Response.ContentType = "application/json";
                    await ctx.Response.OutputStream.WriteAsync(payload);
                    ctx.Response.Close();
                }
                catch (HttpListenerException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        });
    }

    public void Dispose()
    {
        _listener.Stop();
        _listener.Close();
        _ws.Dispose();
    }

    private static string Message(string content, string stop, long input = 1200, long output = 80) => """
        {"id":"msg_1","type":"message","role":"assistant","model":"claude-opus-5-5","content":__CONTENT__,
         "stop_reason":"__STOP__","stop_sequence":null,
         "usage":{"input_tokens":__IN__,"output_tokens":__OUT__,"cache_read_input_tokens":900,"cache_creation_input_tokens":0}}
        """.Replace("__CONTENT__", content).Replace("__STOP__", stop).Replace("__IN__", input.ToString()).Replace("__OUT__", output.ToString());

    private AnthropicModelClient Client() => new(new AnthropicClient { ApiKey = "test-key", BaseUrl = _url });

    [Fact]
    public async Task Requests_carry_the_tools_effort_and_cache_control_and_responses_are_parsed()
    {
        _responses.Enqueue(Message("""[{"type":"thinking","thinking":"","signature":"sig-abc"},{"type":"tool_use","id":"toolu_1","name":"list_dir","input":{"path":"invoices"}}]""", "tool_use"));
        _responses.Enqueue(Message("""[{"type":"tool_use","id":"toolu_2","name":"submit_plan","input":{"summary":"s","actions":[{"type":"write_file","path":"x.txt","content":"hi"}]}}]""", "tool_use"));

        var ledger = new TokenLedger(Path.Combine(_ws.Base, "tokens.jsonl"));
        var plan = await new ClaudePlanner(Client(), ledger: ledger).PlanAsync("make x", _ws.Overlay(), new Policy());

        Assert.Equal("write x.txt (2 bytes)", plan.Actions[0].Describe());

        var first = _bodies[0].RootElement;
        Assert.Equal("claude-opus-5-5", first.GetProperty("model").GetString());
        Assert.Equal("high", first.GetProperty("output_config").GetProperty("effort").GetString());
        Assert.Equal("ephemeral", first.GetProperty("cache_control").GetProperty("type").GetString());
        Assert.Contains("planner", first.GetProperty("system").GetString());
        var tools = first.GetProperty("tools").EnumerateArray().ToList();
        Assert.Equal(["list_dir", "read_file", "submit_plan"], tools.Select(t => t.GetProperty("name").GetString()));
        Assert.Equal("path", tools[0].GetProperty("input_schema").GetProperty("required")[0].GetString());
        Assert.Equal("object", tools[2].GetProperty("input_schema").GetProperty("type").GetString());
        Assert.True(tools[2].GetProperty("input_schema").GetProperty("properties").TryGetProperty("actions", out _));

        // Second turn: the assistant turn is echoed with its thinking signature intact, and every
        // tool_use is answered by one tool_result in a single user message.
        var messages = _bodies[1].RootElement.GetProperty("messages").EnumerateArray().ToList();
        Assert.Equal(["user", "assistant", "user"], messages.Select(m => m.GetProperty("role").GetString()));
        var echoed = messages[1].GetProperty("content").EnumerateArray().ToList();
        Assert.Equal("thinking", echoed[0].GetProperty("type").GetString());
        Assert.Equal("sig-abc", echoed[0].GetProperty("signature").GetString());
        Assert.Equal("tool_use", echoed[1].GetProperty("type").GetString());
        var result = messages[2].GetProperty("content")[0];
        Assert.Equal("tool_result", result.GetProperty("type").GetString());
        Assert.Equal("toolu_1", result.GetProperty("tool_use_id").GetString());
        Assert.Contains("a.txt", result.GetProperty("content").GetString());

        var spent = Assert.Single(ledger.Entries().Take(1));
        Assert.Equal(1200, spent.Usage.Input);
        Assert.Equal(900, spent.Usage.CacheRead);
        Assert.Equal(2, ledger.Entries().Count);
    }

    [Fact]
    public async Task A_refusal_is_reported_with_its_category()
    {
        _responses.Enqueue("""
            {"id":"msg_2","type":"message","role":"assistant","model":"claude-opus-5-5","content":[],
             "stop_reason":"refusal","stop_sequence":null,"stop_details":{"type":"refusal","category":"cyber","explanation":null},
             "usage":{"input_tokens":10,"output_tokens":0}}
            """);
        var e = await Assert.ThrowsAsync<PlannerException>(() => new ClaudePlanner(Client()).PlanAsync("x", _ws.Overlay(), new Policy()));
        Assert.Contains("declined", e.Message);
        Assert.Contains("cyber", e.Message);
    }

    [Fact]
    public async Task A_missing_or_bad_key_is_explained_in_plain_words()
    {
        var client = new AnthropicModelClient(new AnthropicClient { ApiKey = "bad", BaseUrl = "http://127.0.0.1:1/", MaxRetries = 0 });
        var e = await Assert.ThrowsAsync<PlannerException>(() => client.CompleteAsync(new ModelRequest("claude-opus-5-5", "s", [], [ModelMessage.User("hi")], Effort.Low, 100)));
        Assert.Contains("cannot reach Claude", e.Message);
    }
}
