# Claude-specific Jev integrations, plugins and tutorials: verification of five claimed items

Research date: 2026-10-09. Method: direct fetches of GitHub pages, `git clone` (read-only, no code executed) of public repos, npm and PyPI registry JSON, the official MCP registry API (registry.modelcontextprotocol.io), Smithery and Glama search, TypeSafe's docs (docs.typesafe.ai, including llms.txt), YouTube oEmbed, and web search. No plugins or packages were installed and no third-party code was run.

Context: Jev is TypeSafe AI's "System One" model, which returns typed decisions (choice / score / noul yes-no probability) rather than prose ([DigitalOcean](https://www.digitalocean.com/resources/articles/what-is-jev); [TypeSafe docs, "Jev with coding agents"](https://docs.typesafe.ai/introduction/coding-agents.md)). The official TypeScript SDK `@typesafe-ai/sdk` was first published on npm 2026-09-12, maintained by `alliesafe` and `diogo149` ([npm registry](https://registry.npmjs.org/@typesafe-ai/sdk)). A dev.to article dates Jev's release to 15 September 2026 ([dev.to, Sebastian Bennis](https://dev.to/sebastianbennis/how-to-use-jev-with-claude-code-codex-and-cursor-free-reference-file-54a3)). Every Claude integration below therefore dates from mid-September 2026 or later, so all of them are less than four weeks old.

## Overall verdict: which claimed items exist, and does the pasted description match reality?

### Takeaway
All five items correspond to something real, but the description gets details wrong for most of them. (1) jev-gateway exists and is popular, but it selects tools; it does not "approve" them, and for Claude Code it can only add a hint. (2) The `typesafe@typesafe-ai` plugin is official and real, but it is TypeSafe's own marketplace, needs a `marketplace add` step first, and is a documentation skill rather than runtime scoring tools. (3) "jev-mcp" is a set of community MCP servers, not an official TypeSafe product. (4) The OpenTweet guide exists, but there is no OpenTweet guide on dev.to and the page never says "boundary scripts". (5) Ray Amjad's video exists, but the quoted title belongs to a third-party blog summary of it.

### Cited Findings
| # | Item | Status | Primary source(s) |
|---|---|---|---|
| 1 | jev-gateway (vinilana) | VERIFIED exists; description PARTIALLY accurate | [GitHub repo](https://github.com/vinilana/jev-gateway), [npm](https://www.npmjs.com/package/jev-gateway) |
| 2 | TypeSafe Claude plugin `typesafe@typesafe-ai` | VERIFIED exists and is official; description PARTIALLY accurate | [typesafe-ai/skills](https://github.com/typesafe-ai/skills), [TypeSafe docs: Agent skill](https://docs.typesafe.ai/agent-skill.md) |
| 3 | "Jev MCP server (jev-mcp)" | VERIFIED as several community projects; no official TypeSafe MCP server found | [npm jev-mcp](https://www.npmjs.com/package/jev-mcp), [MCP registry search](https://registry.modelcontextprotocol.io/v0/servers?search=jev) |
| 4 | OpenTweet / Dev.to "How to Use Jev in Claude Code" | OpenTweet page VERIFIED; dev.to version NOT FOUND; "boundary scripts" wording NOT FOUND in it | [opentweet.io/jev/claude-code](https://opentweet.io/jev/claude-code) |
| 5 | Ray Amjad, "Jev + Claude Code: A Faster Agentic Coding Review Loop" | Video VERIFIED; exact title is MISATTRIBUTED (it is João Queirós's blog title) | [YouTube ScvXFi4MUSc](https://www.youtube.com/watch?v=ScvXFi4MUSc), [João Queirós blog](https://www.ai.joaoqueiros.com/blog/jev-claude-code-agentic-coding-review-loop-ray-amjad) |

- TypeSafe's own docs say Jev "is **not** a drop-in replacement for the LLM behind Claude Code, Cursor, opencode..." and that "There is no `model: "jev-latest"` setting that turns your coding agent into a Jev-powered agent." For coding agents, the docs recommend the TypeSafe agent skill — [TypeSafe docs, "Jev with coding agents"](https://docs.typesafe.ai/introduction/coding-agents.md)
- TypeSafe's docs index (llms.txt, 117 lines) lists an "Agent skill" page ("Drop-in skill for Claude Code, Codex, and other agent environments") and a "Jev with coding agents" page, but no page about an MCP server — [docs.typesafe.ai/llms.txt](https://docs.typesafe.ai/llms.txt)

### Inferences
- The pasted description reads like a merge of several real sources: jev-gateway's README, TypeSafe's skill docs, npm/MCP-registry listings, the OpenTweet page plus a different dev.to post, and João Queirós's write-up of Ray Amjad's video. Titles and mechanisms got blended along the way, which is consistent with AI-generated summarisation.

### Gaps
- I could not use the GitHub REST API (`api.github.com` returned HTTP 403 to both `gh` and WebFetch in this sandbox), so star and fork counts come from the rendered GitHub web pages fetched on 2026-10-09, and commit data comes from shallow clones.

## 1. Does github.com/vinilana/jev-gateway exist, and does it do what is claimed?

### Takeaway
It exists. It is an MIT-licensed TypeScript local proxy by Vinicius Lana: 76 commits from 2026-09-18 to 2026-10-06, v0.5.1, about 308 stars. Claude Code reaches it through `ANTHROPIC_BASE_URL`, and it asks Jev's API to pick a tool. It is independent of TypeSafe. It does not "approve" tools or use hooks or MCP, and with Claude Code it only adds a one-line hint that the model may ignore.

### Cited Findings
- **Existence and metrics (fetched 2026-10-09):** public repo, description "An easy way to use jev with your coding agent for tool calling reasoning". It has 308 stars, 43 forks, 4 open issues, 14 open PRs, an MIT license and 76 commits on `main` — [GitHub](https://github.com/vinilana/jev-gateway)
- **Authorship and history (from clone):** first commit 2026-09-18 15:30 (-0300) by Vinicius Lana, titled "Add jev-router: LLM gateway that routes tool selection to Jev". Latest commit 2026-10-06, "chore(main): release 0.5.1". 54 of 76 commits are by Vinicius Lana; other contributors include Helio Pinheiro, VIctor, Prometheus, Pedro Moraes and Can H. Tartanoglu. Tags run v0.4.1 to v0.5.1 — [commit 748cb3b](https://github.com/vinilana/jev-gateway/commit/748cb3b), [CHANGELOG](https://github.com/vinilana/jev-gateway/blob/main/CHANGELOG.md)
- **Release cadence:** 0.4.0 and 0.4.1 on 2026-09-20 (first-run setup wizard; OpenRouter and Vercel AI Gateway providers). 0.4.2 and 0.4.3 on 2026-09-23. 0.5.0 on 2026-09-25 (Devin and Kilo launchers, OpenCode Zen key, fix "claude: never answer a thinking conversation by itself"). 0.5.1 on 2026-10-06 — [CHANGELOG](https://github.com/vinilana/jev-gateway/blob/main/CHANGELOG.md)
- **npm package:** `jev-gateway`, created 2026-09-18T20:09Z, latest 0.5.1 (modified 2026-10-06), maintainer `vinilana`. Description: "Local LLM gateway that lets TypeSafe's Jev model pick the tool for coding agents. Launchers for Codex, Claude Code, OpenCode, Kilo, Gemini and Devin" — [npm registry](https://registry.npmjs.org/jev-gateway)
- **Affiliation:** "Independent project, not affiliated with or endorsed by TypeSafe. 'Jev' is TypeSafe's model and this gateway is a client of its public API." — [README](https://github.com/vinilana/jev-gateway/blob/main/README.md)
- **Claude Code mechanism (proxy, not hooks or MCP):** "`jev-claude` runs `claude` with only `ANTHROPIC_BASE_URL` set. Claude Code keeps using its saved login, so a claude.ai subscription keeps working." The README also says nothing in `~/.claude` is changed. The Claude Code gateway listens on port 8789, on 127.0.0.1 only — [README](https://github.com/vinilana/jev-gateway/blob/main/README.md); [bin/jev-claude.mjs](https://github.com/vinilana/jev-gateway/blob/main/bin/jev-claude.mjs)
- **What it does per request:** a request that carries tools triggers one Jev call. The conversation is sent as the state, and Jev is asked which tool (or none), whether a tool is needed, and the value of each closed-set argument. The answer picks a mode: `direct` (gateway builds the tool call itself, no LLM call), `forced` (sets `tool_choice`), `hint` (adds a one-line suggestion), `none` (`tool_choice: "none"`) or `passthrough`. If Jev is slow, down, or the key is wrong, the request goes straight to the LLM — [README](https://github.com/vinilana/jev-gateway/blob/main/README.md)
- **Claude Code limitation (key nuance):** "Jev can do less here than with Codex... Claude Code runs with extended thinking, and the API rejects a forced tool while thinking is on. It also rereads a cached conversation on every turn, and changing `tool_choice` would invalidate that cache. So for Claude Code the gateway adds a short suggestion to the request instead (`hint` mode), which the model is free to ignore. Expect better tool picks on large tool lists, not lower cost or latency." — [README §Using it with Claude Code](https://github.com/vinilana/jev-gateway/blob/main/README.md)
- **Scale detail:** "Tool lists longer than 120 entries (Claude Code sends about 280) take two Jev calls." — [README](https://github.com/vinilana/jev-gateway/blob/main/README.md)
- **Calls TypeSafe's API:** yes. The default endpoint is `https://api.typesafe.ai/v1/systemone` with `TYPESAFE_API_KEY` and model `jev-latest`, using `@typesafe-ai/sdk` types. Alternative resellers are OpenRouter (`openrouter.ai/api/alpha/decisions`, model `typesafe/jev-1.13`), Vercel AI Gateway (`ai-gateway.vercel.sh/typesafe/v1/systemone`) and OpenCode Zen (`opencode.ai/zen/v1/systemone`) — [src/providers.json](https://github.com/vinilana/jev-gateway/blob/main/src/providers.json), [src/jev.ts](https://github.com/vinilana/jev-gateway/blob/main/src/jev.ts)
- **Self-reported benchmark (small sample):** medians with routing on vs off, as output / input tokens / time. Bug-fixing: Fable 5.1 (Claude Code) -13% / -19% / +6%; Opus 5 (Claude Code) -7% / -22% / +2%; Sonnet 5 (Claude Code) -41% / -48% / -25%. Feature tasks: Opus 5 +22% / +61% / +83% and Sonnet 5 +9% / +16% / +37%. The README says "Routing pays off when debugging, for every model. On the feature task it helped some models and made Opus 5 and Sonnet 5 clearly worse," and "Five runs per cell is a small sample." — [README benchmarks](https://github.com/vinilana/jev-gateway/blob/main/README.md)
- **Stated trade-offs:** roughly 0.5–1 s of added latency per tool-bearing turn; one tool chosen per turn; Jev reads text only, with a 32k-token window — [GitHub README (via page fetch)](https://github.com/vinilana/jev-gateway)

### Inferences
- **"Lightweight proxy gateway built for Claude Code CLI and Codex":** accurate. It also covers OpenCode, Kilo, Gemini and Devin.
- **"Intercepts tool calls":** imprecise. It intercepts outgoing API requests that carry tool *definitions*, before the model picks a tool. It does not intercept tool *executions*.
- **"Select or approve tools":** "select" is accurate. "Approve" is not: there is no permission or approval gating, no PreToolUse hook and no MCP component. For Claude Code specifically, the selection is only an ignorable hint.
- **"Before sending requests to Claude":** accurate in that it sits in the request path to the Anthropic API.
- **Maturity:** a young (3-week-old) but active project with CI, release-please, tests and multiple contributors. Its own data shows mixed benefit on Claude Code.
- The Claude Code plugin that actually gates tools with Jev looks like `@jev-harness/claude-code` (see section 3), not jev-gateway.

### Gaps
- The GitHub user profile for `vinilana` (other repos, bio) could not be fetched because the API was blocked. Authorship rests on git commit metadata and npm.
- npm download counts were not retrieved.
- I did not independently test the benchmark claims, and could not have, since the constraint was not to run third-party code.

## 2. Does a `typesafe-ai` plugin marketplace / `typesafe` plugin exist, and would `claude plugin install typesafe@typesafe-ai` work as written?

### Takeaway
Yes, it exists and it is official (TypeSafe AI). The marketplace is named `typesafe-ai` and hosted at github.com/typesafe-ai/skills, and the plugin is named `typesafe`. The install command only works after `claude plugin marketplace add typesafe-ai/skills`. It is not in Anthropic's official plugin directory. It is a single documentation skill that helps Claude *write code* against the TypeSafe API; it does not give Claude runtime Jev scoring tools.

### Cited Findings
- **Manifest:** `.claude-plugin/marketplace.json` sets `"name": "typesafe-ai"`, owner "TypeSafe AI" (https://typesafe.ai), with one plugin `"name": "typesafe"`, description "Full context on the TypeSafe API: question types, architectural patterns, and best practices", `"source": "./"` — [typesafe-ai/skills marketplace.json](https://github.com/typesafe-ai/skills/blob/main/.claude-plugin/marketplace.json)
- **Plugin manifest:** `plugin.json` has name `typesafe`, version 0.5.7, MIT license. It declares no `mcpServers` and no hooks. The repo contains a single skill at `skills/typesafe-ai/SKILL.md` — [typesafe-ai/skills plugin.json](https://github.com/typesafe-ai/skills/blob/main/.claude-plugin/plugin.json)
- **Official install instructions (two steps):** "claude plugin marketplace add typesafe-ai/skills" then "claude plugin install typesafe@typesafe-ai". For other agents: `npx skills add typesafe-ai/skills --skill typesafe-ai`. In Claude Code it is invoked with `/typesafe:typesafe-ai` — [TypeSafe docs: Agent skill](https://docs.typesafe.ai/agent-skill.md); [repo README](https://github.com/typesafe-ai/skills/blob/main/README.md)
- **Purpose, per TypeSafe:** "gives your AI coding agent full context on the TypeSafe API: the three question types, the architectural patterns, and best practices for structuring evaluations." The coding-agents page says the skill makes the agent "better at *writing code that uses TypeSafe*" — [Agent skill](https://docs.typesafe.ai/agent-skill.md); [Jev with coding agents](https://docs.typesafe.ai/introduction/coding-agents.md)
- **Repo history:** public repo with 2 commits: "Initial commit" 2026-08-24 by Allie Laabs, and "Release v0.5.7" on 2026-09-12 by typesafe-public-bot[bot]. It shows about 2,633 stars on the org page (fetched 2026-10-09) — [GitHub typesafe-ai org](https://github.com/typesafe-ai); [repo](https://github.com/typesafe-ai/skills)
- **TypeSafe org (12 public repos, about 1.6k followers):** typesafe-sdk-python (276★), typesafe-sdk-js (271★), system-one-adapter-python (384★), skills (2,633★), n8n-nodes-typesafe-ai, WorkflowEvals, daggerverse, typesafe-public-examples and others. None is an MCP server or a separate Claude plugin — [GitHub typesafe-ai](https://github.com/typesafe-ai)
- **Not in Anthropic's official directory:** `anthropics/claude-plugins-official` marketplace.json lists 315 plugins as of 2026-10-09, with zero entries matching "typesafe" or "jev" — [claude-plugins-official marketplace.json](https://raw.githubusercontent.com/anthropics/claude-plugins-official/main/.claude-plugin/marketplace.json)
- **Third-party guides repeat the correct two-step command:** e.g. Remy's 2026-09-23 article lists "claude plugin marketplace add typesafe-ai/skills" before "claude plugin install typesafe@typesafe-ai" — [redreamality.com](https://redreamality.com/blog/jev-claude-code-10x-and-25-lines/)
- **Candidate repos that do not resolve** (git ls-remote asks for credentials, which typically means a non-existent or private repo): typesafe-ai/typesafe, typesafe-ai/claude-plugins, typesafe-ai/plugins, typesafe-ai/mcp, typesafe-ai/jev-mcp, typesafe-ai/jev, typesafe-ai/agent-skills, typesafe-ai/claude-code-plugin — (this researcher's ls-remote checks, 2026-10-09; no URL)

### Inferences
- The exact command `claude plugin install typesafe@typesafe-ai` is correct syntax, but run alone on a fresh install it would fail because the `typesafe-ai` marketplace is not known yet. The pasted description omits the required `claude plugin marketplace add typesafe-ai/skills` step.
- "Installable via the Claude Plugin Marketplace" is misleading. It is TypeSafe's own GitHub-hosted marketplace, not Anthropic's official one.
- "Granting Claude awareness of Jev scoring utilities" is roughly right for API *knowledge* (question types, patterns, cookbooks). It does not add callable tools; Claude would still have to write and run code with `TYPESAFE_API_KEY` to get a score.

### Gaps
- I did not read the full SKILL.md body or its reference files beyond the README and docs descriptions.
- Two of the org's 12 repos were not shown on the org page.

## 3. Does a "Jev MCP server (jev-mcp)" exist that exposes Jev primitives as MCP tools in Claude Desktop or Cursor?

### Takeaway
There is no official TypeSafe MCP server, but many community ones exist. They appeared from 2026-09-17 onward, including an npm package literally named `jev-mcp` (rashed parvez) and a PyPI package named `jev-mcp` (Aitejiu). All are third-party, a few weeks old, and use the user's own TypeSafe (or reseller) key. The description is accurate in spirit if read as "community implementations", wrong if read as an official product.

### Cited Findings
- **npm `jev-mcp`:** by rashed parvez (`rashedInt32/jev-mcp`). Created 2026-09-17T11:51Z; versions 0.3.0 and 0.4.0 (2026-09-17), 0.5.0 (2026-09-20), 0.5.1 (2026-09-26). Description: "MCP server exposing TypeSafe Jev typed judgments: classify, score, check, a batched multi-question ask, a many-item triage with server-side file reads, and model discovery." — [npm registry](https://registry.npmjs.org/jev-mcp)
  - It also ships as a Claude Code plugin: "/plugin marketplace add rashedInt32/jev-mcp" then "/plugin install jev@jev-mcp". The plugin declares an inline stdio server `npx -y jev-mcp@0.5.1`. The README also gives `claude mcp add --scope user jev -- npx -y jev-mcp` and a generic `mcpServers` JSON block for "any MCP client". The key is read from `TYPESAFE_API_KEY`, `JEV_API_KEY` or `~/.config/typesafe/key`, never from a tool argument — [npm README](https://www.npmjs.com/package/jev-mcp); [plugin.json](https://github.com/rashedInt32/jev-mcp/blob/main/.claude-plugin/plugin.json)
  - Repo: 10 commits, 2026-09-17 to 2026-09-28. It imports `@typesafe-ai/sdk`, so it calls TypeSafe's API. The README does not name Cursor or Claude Desktop explicitly — [GitHub](https://github.com/rashedInt32/jev-mcp)
- **PyPI `jev-mcp`:** by "Aitejiu", 0.1.0 to 0.1.2, all on 2026-09-21. "MCP server exposing TypeSafe Jev judgments for agent harnesses: injection scanning, shell risk gating, candidate ranking." Listed in the official MCP registry as `io.github.Aitejiu/jev` — [PyPI](https://pypi.org/project/jev-mcp/); [repo](https://github.com/Aitejiu/jev-harness-lab)
- **Official MCP registry:** a search for "jev" returns 80 entries, including old versions and unrelated hits such as "tz-europe-sarajevo". A search for "typesafe" returns one entry. Jev-related latest entries:
  - `io.github.freepik-company/jev-mcp`: "Typed decisions with Jev / System One via OpenRouter or TypeSafe: classify, verify, rerank, decide", OCI image v0.3.1, published 2026-09-28
  - `io.github.Renwang-Huang/typesafe-mcp`: PyPI 0.5.2, needs `TYPESAFE_API_KEY`, 2026-09-21
  - `io.github.amidabuddha/jev-decision-mcp` (2026-09-24)
  - `io.github.PerryLink/jevcore` (2026-09-20)
  - `io.github.shitianfang/jev-use` (2026-09-22)
  - `io.github.wangkuangkuang/jev-mcp-server` (2026-09-22)
  - `io.github.NomenAK/jev-agent-tools` (2026-10-07)
  - `io.github.pollinations/ask-jev` (2026-09-29)
  - `com.jev-agent/jagent`, a remote server with its own API key, published 2026-10-09

  — [MCP registry search "jev"](https://registry.modelcontextprotocol.io/v0/servers?search=jev&limit=100); [search "typesafe"](https://registry.modelcontextprotocol.io/v0/servers?search=typesafe&limit=100)
- **Other npm MCP packages:**
  - `@jkudish/jev-mcp` (updated 2026-10-06): "verify claims, screen for injection, find, rerank, classify, decide, compare passages, extract fields, review patches, and gate completion claims"
  - `@maximem/jev-mcp` (created 2026-09-20)
  - `typesafe-jev-mcp` (anasbekheit, 2026-09-20)
  - `jev-mcp-server` (weeeeekdy, 2026-09-21)
  - `typesafe-mcp` (MarkChu-git, created 2026-09-21, v0.2.0 on 2026-10-08): "Choice / Score / Noul as typed tools"
  - `agent-fastpath` (2026-09-22): "a decision layer for Claude Code, Codex, and Cursor"

  — [npm search "jev mcp"](https://registry.npmjs.org/-/v1/search?text=jev%20mcp&size=20); [npm typesafe-mcp](https://registry.npmjs.org/typesafe-mcp); [npm agent-fastpath](https://registry.npmjs.org/agent-fastpath)
- **Related Claude Code plugin that does gate tools:** `@jev-harness/claude-code` (talya1412, created 2026-09-20, v0.7.0): "Claude Code plugin: TypeSafe Jev (System One) destructive-tool gating and skill routing" — [npm registry](https://registry.npmjs.org/@jev-harness/claude-code)
- **Smithery:** a search for "jev" returned only unrelated servers (e.g. "Ontario Protocol", "Arguslog"), so no Jev server was found there. Glama's API returned no parseable results — [Smithery registry search](https://registry.smithery.ai/servers?q=jev)
- **No official server:** TypeSafe's org repos and docs index include no MCP server. TypeSafe's recommended coding-agent integration is the agent skill — [GitHub typesafe-ai](https://github.com/typesafe-ai); [docs llms.txt](https://docs.typesafe.ai/llms.txt)

### Inferences
- Any stdio MCP server listed here can be registered in Claude Desktop, Cursor or Claude Code through standard `mcpServers` JSON, so "in Claude Desktop or Cursor" is plausible. The best-known `jev-mcp` package documents Claude Code primarily, though.
- Users should treat these as unvetted community code holding their API key. Ages run from 0 to 3 weeks and most versions are below 1.0.

### Gaps
- No star counts for most of these repos (GitHub API blocked; the rashedInt32/jev-mcp web page timed out with HTTP 504).
- mcp.so was not checked directly. Glama returned nothing usable.

## 4. Does the OpenTweet / Dev.to guide "How to Use Jev in Claude Code" exist, and does it use "Jev boundary scripts" to grade Claude drafts before posting?

### Takeaway
The OpenTweet page exists at opentweet.io/jev/claude-code ("Last updated: September 2026", no author). It shows a script that grades Claude-written drafts with Jev before posting, gated by the script's exit code. The page never uses the phrase "boundary scripts". I found no dev.to copy of the OpenTweet guide. The dev.to article with a similar title is a different piece by Sebastian Bennis.

### Cited Findings
- **OpenTweet page:** title "How to Use Jev in Claude Code | OpenTweet", "Last updated: September 2026", no author named. It offers three routes (a script, TypeSafe's agent skill, or a community MCP server) and recommends the script. The script, `scripts/score-draft.mjs`, imports `@typesafe-ai/sdk`, calls `client.systemOne(...)` with model `jev-1.13.0`, a "strength" score question and a "slop" yes/no question, and exits 0 or 1. A CLAUDE.md rule says "Never publish a draft without scoring it first" with a three-attempt cap. Publishing goes through OpenTweet's own MCP (`claude mcp add --transport http opentweet https://mcp.opentweet.io/mcp`). No hooks are used. The page states "Claude Code cannot call Jev on its own" and "A passing score is not proof." — [opentweet.io](https://opentweet.io/jev/claude-code)
- **The dev.to article** "How to Use Jev With Claude Code, Codex and Cursor (Free Reference File)" by Sebastian Bennis (published Sep 24, year inferred as 2026 from its reference to Jev's 15 September 2026 release) offers a Markdown reference file for coding assistants. It does not mention OpenTweet, boundary scripts, grading drafts before posting, MCP, the plugin command or jev-gateway — [dev.to](https://dev.to/sebastianbennis/how-to-use-jev-with-claude-code-codex-and-cursor-free-reference-file-54a3)
- **Where "boundary" wording comes from:** Remy's article "Jev × Claude Code: Four Real Integration Paths and a 25-Line Minimal Build" (2026-09-23) uses "Boundary hooks (PreToolUse / compaction / stop)" as one of four paths. The others are the official skill, MCP tools (e.g. `@jkudish/jev-mcp`) and per-turn model routing via `jev-router` — [redreamality.com](https://redreamality.com/blog/jev-claude-code-10x-and-25-lines/)
- **Other same-topic guides surfaced by search** (titles only, not fetched): "How to Use Jev in Claude Code, OpenCode and Codex" ([blog.edersonfernandes.com.br](https://blog.edersonfernandes.com.br/how-to-use-jev-claude-code-opencode-codex/)) and "How to Use Jev in Claude Code and Codex: Four Routes That Work" ([apimaster.ai](https://apimaster.ai/blog/jev-claude-code-codex)) — [web search results](https://opentweet.io/jev/claude-code)

### Inferences
- "OpenTweet / Dev.to guide" conflates two different articles. "Jev boundary scripts" looks like a blend of OpenTweet's "script" route and Remy's "boundary hooks" heading.
- OpenTweet is the publisher and its own MCP server does the posting, so the guide doubles as product marketing. Its method (SDK script plus CLAUDE.md rule) is a legitimate, low-tech pattern.

### Gaps
- No exact publication day for the OpenTweet page, only "Last updated: September 2026".
- I searched for a dev.to repost of the OpenTweet guide with one exact-title search and found none. A broader dev.to crawl was not done.

## 5. Does Ray Amjad's walkthrough "Jev + Claude Code: A Faster Agentic Coding Review Loop" exist?

### Takeaway
A Ray Amjad YouTube video on Jev + Claude Code exists (youtube.com/watch?v=ScvXFi4MUSc, channel @RAmjad). Its current title is "Jev + Claude Code = The Cheapest Agentic Coding Loop Yet". The exact title in the description is the title of João Queirós's blog analysis of the video, published 2026-09-18. The content claim (Jev as a fast screening layer for diffs and browser observations) matches that analysis.

### Cited Findings
- **YouTube oEmbed (fetched 2026-10-09):** title "Jev + Claude Code = The Cheapest Agentic Coding Loop Yet", author_name "Ray Amjad", author_url https://www.youtube.com/@RAmjad — [YouTube](https://www.youtube.com/watch?v=ScvXFi4MUSc)
- **João Queirós, "Jev + Claude Code: A Faster Agentic Coding Review Loop"** (published 18 Sep 2026, reviewed 19 Sep 2026): credits Ray Amjad's video and cites it under the title "Jev + Claude Code = The New Agentic Coding Loop" at the same URL. It links Ray's X account (x.com/theramjad) and site (rayamjad.com) — [ai.joaoqueiros.com](https://www.ai.joaoqueiros.com/blog/jev-claude-code-agentic-coding-review-loop-ray-amjad)
- **Workflow described:** Claude Code or Codex plans, implements and repairs. Jev answers narrow typed questions about "skill fit, diff risk, and whether a comment or browser observation merits attention". The loop is: build → deterministic checks (types, tests, static analysis, scripted browser paths) → screen remaining evidence with Jev → agent investigates shortlist → fix and verify. It references the official TypeSafe skill, TypeSafe's Skill Suggestion cookbook, the `devagrawal09/jev-review` repo and Vercel AI Gateway. Key quote: "Jev can act as a cheap, fast screening layer around a coding agent, but it cannot build or certify software on its own." The article notes that browser demos "show promise but don't prove full coverage" and that cost figures are partly estimates. It describes no hooks, MCP servers or shell commands — [ai.joaoqueiros.com](https://www.ai.joaoqueiros.com/blog/jev-claude-code-agentic-coding-review-loop-ray-amjad)
- A general web search for "Ray Amjad Jev Claude Code video" did not surface the video; it was found through the blog article and confirmed via oEmbed — [web search](https://redreamality.com/cn/tags/claude-code/)

### Inferences
- The video was retitled at least once: "The New Agentic Coding Loop", as cited on 2026-09-18, and "The Cheapest Agentic Coding Loop Yet" on 2026-10-09. A title change like this is common on YouTube. The pasted description attributed a blog post's headline to the video.
- "Test browser logs" overstates it slightly. The source talks about "browser observations" and scripted browser paths.

### Gaps
- Upload date, view count and description of the video could not be retrieved, because the YouTube watch page served no metadata to the fetcher. The video was public by 2026-09-18 at the latest, based on the blog date.
- I did not watch the video or read its transcript, so the claims come from João Queirós's secondary analysis.
- I did not check Ray Amjad's own website for a companion post.
