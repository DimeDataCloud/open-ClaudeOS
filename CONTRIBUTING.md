# Contributing

Thanks for looking. This project is small enough that one well-aimed change
matters, and opinionated enough that it helps to know the rules first.

## The rule that shapes everything

**The model proposes; deterministic code decides.** Anything that grants,
refuses, places, routes or undoes is plain code with tests. A model may write a
plan, a chart recipe or a widget manifest; it never approves anything, and no
text it reads (a file, a web page, an email) can change what policy allows. If a
change makes a model's output more trusted than it was, expect a hard review.

## Layout

| Path | What | Builds on |
|---|---|---|
| `windows/src/ClaudeOS.Core` | Actions, policy, consent, undo, intent routing, layout engine, charts, mods, presence, planner. No UI, no dependencies. | any OS |
| `windows/src/ClaudeOS.Claude` | The only project that references the Anthropic SDK, behind `IModelClient` | any OS |
| `windows/src/ClaudeOS.Cli` | `claudeos` on a terminal; also generates the prototype's data from the real core | any OS |
| `windows/src/ClaudeOS.Shell` | The WinUI 3 app: Intent Bar, windows, tray | Windows (CI builds it) |
| `windows/tests` | xUnit | any OS |
| `design/` | Design tokens, the interactive prototype, doc screenshots | Node |
| `src/claudeos`, `tests/` | The Python Phase 0 reference | Python 3.10+ |
| `docs/` | Architecture, design, threat model, stack decision, roadmap | |

Put logic in Core and keep the Shell thin. If it can be tested without a window,
it belongs in Core.

## Building and testing

```sh
# core, CLI and tests (need the .NET 10 SDK)
cd windows
dotnet build ClaudeOS.slnx
dotnet test tests/ClaudeOS.Core.Tests

# the Python reference
pip install -e ".[dev]" && pytest

# design tokens: edit design/tokens.json, then
cd design && npm ci && node build-tokens.mjs        # regenerates CSS, XAML and C#
node build-tokens.mjs --check                        # what CI runs
```

The Shell only builds on Windows. You do not need Windows to work on the core,
the design system or the docs; the Shell's compile result comes from CI. To
iterate on Shell code from another OS, push a branch and read the "Shell
(Windows)" run.

## Conventions

- C#: nullable on, warnings are errors (except in generated XAML code), file-scoped
  namespaces, records for data. Match the surrounding comments: explain *why*,
  keep them short, no restating the code.
- Core stays free of dependencies and compatible with trimming and Native AOT:
  no reflection-based serialization (use `Utf8JsonWriter`/`JsonDocument`), no
  `dynamic`.
- Win32 calls use `LibraryImport` (source-generated marshalling) in
  `Interop/Native.cs`. No code generators to version.
- Every change to behaviour comes with a test that fails without it. If it is a
  bug fix, the test is the bug.
- Anything the person sees goes through design tokens. No literal colors,
  fonts or durations in XAML or C#; add a token.
- Do not add a model call where a rule would do. Tokens spent are a cost the
  person pays; the zero-token paths (grammar, file index, layout) are a feature.

## Pull requests

Describe what changes for the person using it, then how you checked. Keep one
idea per PR. Do not include secrets, API keys, signing certificates or a
`.pfx`; CI signs with its own secrets and never reads them from the repo.

By contributing you agree your work is licensed under Apache-2.0 (see
[LICENSE](LICENSE)). There is no CLA.

## Security

Please read [SECURITY.md](SECURITY.md) before reporting anything that lets text
the model reads cause an action, or that bypasses policy or consent.
