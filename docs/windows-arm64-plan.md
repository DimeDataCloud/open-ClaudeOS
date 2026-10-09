# Build 1: Windows on ARM test build

The first build you can install and use runs on **Windows 11 ARM64**, tested on
a Surface Pro. It is an app layer on top of Windows, not a new operating
system: Windows keeps handling drivers, the desktop and apps, and this build
adds the intent layer on top. Linux follows later using the same core.

This is a plan. Nothing in it is built yet.

**The three goals that decide every choice below**, in priority order:
1. It must feel native, seamless and part of Windows.
2. It must have the lowest latency we can achieve.
3. It must spend the fewest model tokens.

## What Build 1 should feel like

You press a key and say what you want. The system works out whether you asked
to *see* something that exists or to *make* something new, and puts the
result on screen neatly, without disturbing what you were doing.

| You say | What happens |
|---|---|
| "open the Q3 budget" | The file opens straight away, in the space that is free on screen. No model call is needed, and no approval is asked for: opening a file changes nothing. |
| "show me spending by month from the Q3 budget as a graph" | A small window appears where there is room, holding only the graph: no title bar, no toolbar, no borders, no tags. |
| "write a one-page report on these invoices" | A report window streams in: only the report. |
| "make me a widget with battery and my next meeting, top right" | Claude writes a mod. You see what it shows and what it is allowed to access, approve once, and it appears. |
| "rename these files by date" | This changes your files, so it goes through the approval card from Phase 0, and Undo works afterwards. |

**The rule behind it.** Retrieving and viewing is instant and needs no
approval. Making something new produces new files only, so it applies straight
away with Undo available. Anything that changes existing files or leaves the
machine goes through the approval card.

## Stack: native Windows

**C# on .NET 10, with WinUI 3 (Windows App SDK) for the interface, and the
Win32 and DWM APIs for windows and placement.** Most content is drawn with
native controls. A web view (WebView2) is used only where web content is the
point: custom HTML artifacts and web-based mods.

| Goal | Why this stack wins |
|---|---|
| **Native feel** | Real Fluent controls, Windows' own text rendering, Mica and Acrylic materials, system rounded corners and shadows. Touch, pen and Windows Ink, screen readers (UI Automation), and per-monitor scaling all come built in, which matters on a Surface. |
| **Latency** | Native windows and controls show in milliseconds. A web view needs its own process and engine before it can draw, so most artifacts avoid one entirely. Windows are pre-created and reused. Native AOT compilation, if the spike confirms our dependencies support it, makes startup faster again. |
| **Token use** | The stack doesn't change tokens much; the architecture does (see "Token efficiency"). It does help indirectly: native renderers take compact specs that Claude writes in a few hundred tokens, instead of whole HTML pages. |
| **Integration** | Direct access to the Windows features that make it feel part of the system (see "Windows integration"). |
| **Claude** | The official Anthropic C# SDK. |
| **Portability** | The core library (planner, policy, consent, undo, layout maths) is plain .NET and runs on Linux and macOS too. Only the shell is Windows-specific. |

**Options considered and rejected:**

| Option | Why not |
|---|---|
| Electron + TypeScript | Ships a second browser engine. Every window is a web page, which is the slowest to appear and the least native to touch, pen and text. It is also the heaviest on memory and battery. |
| Tauri 2 + Rust | Lighter, but the interface is still web-rendered, so it never quite feels native. There is also no official Claude SDK for Rust. |

**The Python prototype** stays as the reference design. Its tests are ported
to C#, and the C# core must pass them.

## Two kinds of window

### Opening existing files: "just open it"

- **Files we can show natively**, in our own content-only viewer:

  | Type | Native renderer |
  |---|---|
  | Images, including HEIC | Windows' own codecs |
  | PDF | Windows' built-in PDF renderer (`Windows.Data.Pdf`), pages only |
  | Text and code | Native text with syntax colouring |
  | Markdown | Native Markdown renderer |
  | CSV | Native table |
  | Audio and video | The system media player element, controls hidden until touched |

- **Office and other documents, for viewing.** Windows *preview handlers* are
  the same components that draw File Explorer's preview pane. Where one is
  installed (Office installs them), we can show the document content-only
  inside our own window.
- **Editing.** Opening for editing goes to the default app, and then the
  layout engine moves the app's window into the free space. This is best
  effort: some apps resist being moved (see Risks).
- **Finding the file** goes to the Windows Search index first (milliseconds),
  then a scoped scan of Desktop, Documents and Downloads. If several files
  match, a small picker appears.

### Things that are made, not retrieved: subject-only windows

A subject-only window shows exactly what was asked for and nothing else.

- **No visible chrome:** no title bar, no borders, no scroll bars unless the
  content overflows, and no headers, tags or toolbars.
- **How it is built.** Each window is a plain Win32 popup holding native WinUI
  content (a XAML island). That gives full control over the window itself:
  Windows 11's rounded corners and shadow, an optional Mica or Acrylic
  backdrop, and no frame.
- **Sized to the content.** A wide chart gets a wide window; a portrait report
  gets a portrait one.
- **Controls without chrome:**
  - Drag anywhere on the content to move; drag the edges to resize.
  - Esc or Ctrl+W closes; double-click switches between fit-to-content and
    larger.
  - Right-click, or press and hold on touch, opens the only menu: Pin on top,
    Save as, Copy, Open in app, Annotate with pen, Edit with Claude ("make the
    bars blue"), Close.
- **Accessible anyway.** Each window has a name for screen readers ("Chart:
  spending by month"), is reachable by keyboard, and is listed in the Intent
  Bar's window list.
- **Assembles live.** A pre-created window appears at its final position as
  soon as the request is understood. Report text streams in as it is written;
  a chart appears the moment its data is complete.

**Native renderers by content type.** Most artifacts are data drawn by code we
control. Claude writes a compact spec, not a program:

| Type | Claude produces | Drawn by |
|---|---|---|
| Chart | A chart spec (a subset of Vega-Lite) plus a data reference | Native chart renderer (Win2D, or an existing MIT library such as ScottPlot) |
| Table | A column spec plus a data reference | Native table |
| Report | Markdown | Native Markdown renderer |
| Diagram | Nodes and edges | Native layout (Microsoft's MSAGL graph-layout library) |
| Image | SVG | Native SVG rendering |
| Custom | An HTML/JS app | WebView2, sandboxed: no network and no file access unless granted |

**Artifacts are files.** Each one is saved under
`Documents\ClaudeOS\Artifacts\<date>\` (for example `spending.chart.json`
next to its data), and reopens exactly as it was.

## The layout engine: "understand where everything else is"

Placement is plain geometry, with no AI involved. It runs in milliseconds.

1. **Look.** For the monitor you are working on (the one with the cursor or
   the active window), read its usable area, which excludes the taskbar.
   Then list visible windows with their true on-screen bounds, skipping
   minimized windows and windows on other virtual desktops.
2. **Find free space.** Compute the largest empty rectangles left in that
   area.
3. **Score each place** where the content fits:
   - Never cover the active window.
   - Prefer the content's natural size and shape.
   - Prefer places near what you are working on.
   - Line up with screen edges and other windows, using the same gaps as
     Windows' Snap layouts.
4. **If nothing fits, fall back in order** (each step is configurable):
   1. Float over a background window, never the active one.
   2. Make room: snap the active window to half the screen and use the other
      half. "Put it back" restores the old layout exactly.
   3. On a small screen or in tablet posture, show the content centred in
      focus mode; dismissing it returns everything as it was.
5. **Adapt to the device:**
   - Laptop or tablet posture (keyboard attached or not).
   - Portrait or landscape orientation.
   - An external monitor, if one is attached.
   - Display scaling: Surface screens usually run at 150–200%.
6. **Learn by suggestion only.** If you keep moving PDFs to the right half,
   the system offers a rule ("open PDFs on the right half?"). It never
   changes its behaviour silently.

Moving or resizing your other windows changes nothing in your files, so it
needs no approval card. Every layout change is still recorded, so it can be
put back.

## Latency budget

| Step | Target | How |
|---|---|---|
| Hotkey to Intent Bar visible | under 50 ms | The bar is created at startup and only shown |
| Deciding "open" or "make" for simple phrasings | under 50 ms | Local grammar; for unclear phrasing, a small on-device model (see below) |
| "open X" to content on screen, in our viewer | under 300 ms for typical files | Search index plus native rendering |
| "open X" in another app | that app's own start time; placed within 100 ms of its window appearing | Window events from Windows |
| Request understood to window placeholder on screen | under 100 ms | Pre-created window pool and the layout engine |
| First content from Claude | about 1–2 s | Streaming, prompt caching |

The model is never between your input and the screen. Typing, dragging and
drawing are all handled by Windows itself.

## Token efficiency

The cheapest token is the one never sent. In order of impact:

1. **Zero-token paths.** Opening, finding, launching, window commands and
   layout rules are handled by local code. They are the most common requests
   and use no model at all.
2. **Intent routing on the device's own chip.** The Surface Pro's Snapdragon
   chip has an NPU, which makes it a Copilot+ PC. Windows offers an on-device
   small language model (Phi Silica) through its Windows AI APIs, and ONNX
   Runtime can run our own small classifier on the NPU. Either one can decide
   between "open", "make" and "change" and fill in simple details, with no
   cloud call. Which APIs are available on your Surface is a spike item.
3. **Data stays local; Claude writes the recipe.** For "spending by month
   from the Q3 budget", Claude sees the column names and a few sample rows,
   and returns a chart spec that includes the grouping and sums. Local code
   runs that over the full file. A 5,000-row spreadsheet costs about the same
   as a 5-row one.
4. **Compact outputs.** A chart spec is a few hundred tokens; the same chart
   as a web page with code would be thousands.
5. **Prompt caching.** The system prompt and tool definitions stay the same
   between requests and are cached, so repeat requests pay only for what is
   new.
6. **Effort per task.** Simple specs run at low effort; multi-step plans at
   higher effort. The planner uses `claude-opus-5-5`, Anthropic's current
   default. Whether any route should move to a cheaper model is your call,
   after we measure quality.
7. **Remember what was read.** File summaries are cached locally and keyed by
   the file's content hash, so asking about the same file again does not
   re-read it.
8. **Measure it.** Tokens per request are logged locally and shown on demand,
   and the daily and monthly budget caps from `challenges.md` apply.

**What the Jev research means here.** TypeSafe's Jev model is a fast,
cheap router (about 100 ms) and could fill the routing role in item 2. But it
is a cloud service: it adds a second company that sees your data, and it
doesn't work offline. Local routing on the NPU is preferred. Jev, if used at
all, is an opt-in alternative, and it never approves actions (see
`reports/Jev decision model and Claude.md`).

## Windows integration

These are what make it feel part of Windows rather than an app sitting on top:

- **Summon key.** Windows 11 lets you assign the Copilot key to an app; we
  will confirm in the spike that ours qualifies once packaged. A global
  hotkey is the fallback.
- **Always there.** Starts at sign-in and lives in the system tray.
- **Native notifications** for finished tasks and approvals waiting.
- **File Explorer.** "Show with ClaudeOS" and "Ask about this file" in the
  right-click menu, plus a Share target.
- **Recycle Bin for deletes**, which is Windows' own undo.
- **Snap-aware placement** that uses the same gaps and zones as Snap layouts.
- **Respects Do Not Disturb** and focus sessions.
- **Pen.** Annotate any artifact with Windows Ink; annotations are saved
  alongside it.
- **On-device text recognition** (Windows' OCR) for reading an image or a
  window on request, with no tokens spent.
- **API key** encrypted with Windows' own per-user data protection.

## Mods: customize everything

Mods build on the idea of Claude mods (panes, status lines, hooks) and go
further: a mod can add to or change almost any surface the system draws.

**Kinds of mod**

| Kind | Example |
|---|---|
| Widget | A pinned live panel: battery and next meeting, top right |
| Artifact type | Adds a "kanban" type and its renderer; Claude can then produce kanbans |
| Command | "standup" opens three files in a set layout |
| Layout rule | Charts always bottom right at 420×280; PDFs on the right half |
| Theme | Fonts, colours, corner radius, Mica or Acrylic, for every window and the Intent Bar |
| Hook | Runs when a file opens, an artifact is created, or a plan is approved |
| Connector | An MCP server that adds a data source or an action |

**Three levels, from fastest and safest to most flexible:**
1. **Declarative (the default).** The mod is pure JSON: a layout of native
   elements (text, number, chart, list, image, button) bound to data the mod
   has permission for. It runs no code, draws natively at full speed, follows
   the theme automatically, and Claude can write one in very few tokens.
2. **Scripted.** Adds logic in JavaScript, run by a sandboxed interpreter
   inside the app (Jint). The script can only call the host functions its
   capabilities allow, and it has time and memory limits.
3. **Web.** A full HTML/JS interface in a sandboxed WebView2, for anything the
   first two cannot express.

**What a mod is.** A folder holding a `mod.json` manifest and its files, in
`%APPDATA%\ClaudeOS\mods\`:

```json
{
  "id": "battery-and-next-meeting",
  "name": "Battery and next meeting",
  "version": "0.1.0",
  "kind": "widget",
  "level": "declarative",
  "placement": { "anchor": "top-right", "size": [220, 90] },
  "capabilities": ["system.battery", "calendar.read.next"],
  "view": {
    "stack": [
      { "metric": "{system.battery.percent}%", "label": "Battery" },
      { "text": "{calendar.next.title}", "subtext": "{calendar.next.startsIn}" }
    ]
  },
  "settings": {
    "accent": { "type": "color", "default": "#7aa2f7" },
    "showSeconds": { "type": "boolean", "default": false }
  }
}
```

**Making a mod by asking.** "Make me a widget that…" leads Claude to write
the folder. Before anything is installed, the approval card shows:

- a live preview;
- the capabilities it asks for, in plain words;
- any code, if it is a scripted or web mod.

Approve once and it loads. "Make the clock bigger" edits the mod the same
way.

**How mods are kept safe:**

- Capabilities are declared up front and approved by you, and the core checks
  them on every call.
- No mod can reach the approval, policy or undo code.
- A web mod gets no network and no file access unless granted.

**Built to be changed:**

- A mod's `settings` block generates its settings screen automatically.
- Mods reload live when edited.
- A mod can be shared as a folder, zip or Git repository.
- Disabling a mod is one click; removing it is undoable.

## Getting the build onto the Surface

1. **Building.** WinUI apps must be built on Windows, so every push builds an
   ARM64 package on a GitHub Actions Windows runner. This also means our
   development container cannot build or run it; testing happens on your
   Surface.
2. **Packaging as MSIX.** This gives the app a Windows identity, which several
   features need: the Copilot key, the Windows AI APIs, the Explorer
   right-click menu, start at sign-in, and notifications.
3. **Signing test builds.** Builds are signed with a project test certificate.
   You install that certificate once on the Surface (into "Trusted People"),
   after which each new build installs with a double-click. Wider testing
   later needs proper signing: a code-signing certificate or Microsoft's
   cloud signing service.
4. **Smart App Control.** If it blocks a build, don't turn it off; we fix the
   signing instead.
5. **First run.** The app asks for your Claude API key and stores it
   encrypted.

## Milestones

| Milestone | Delivers | Done when |
|---|---|---|
| **M0: spike** | Packaged ARM64 app installs with the test certificate. A Win32 popup with native content, rounded corners, shadow and no frame shows a native chart. Lists windows and moves one. Hotkey, and the Copilot key if allowed. Checks which on-device AI APIs the Surface offers. Checks whether Native AOT works. | Each item works, or has a recorded fallback, on your Surface |
| **M1: open anything** | Intent Bar, local intent grammar, Search-index lookup, native viewers, preview handlers, default-app opening with placement, layout engine, "put it back" | "open X" lands in free space in every test arrangement below, within the latency budget |
| **M2: make things** | C# core with the Claude planner (official C# SDK), scoped read-only tools and a `create_artifact` tool; native renderers; local data execution; subject-only windows; artifacts saved as files; on-device routing if available | Each content type streams in with no chrome; the token log shows spreadsheet charts cost about the same at any row count |
| **M3: mods** | Manifest format; declarative, scripted and web levels; capabilities; widget, artifact-type, command, layout-rule and theme mods; "make me a mod" flow | You can create, tweak, disable and remove a mod by asking |
| **M4: safe changes** | The Phase 0 core in C#: approval cards, plan-bound grants, undo journal, Recycle Bin deletes, dry-run outbox | The invoice injection demo is contained on Windows |

Build 1 is M0 through M4. The protected-path and scoped-read rules from Phase
0 are in from M2, because that is when Claude starts reading files.

## Test checklist for the Surface

- **Install:** the certificate and package install cleanly; check Smart App
  Control; the first-run key setup works.
- **Summon:** the hotkey (and the Copilot key, if allowed) shows the Intent
  Bar within the budget.
- **"open <file>"**, tried with:
  - an empty desktop;
  - one maximized app;
  - two apps snapped side by side;
  - an external monitor attached;
  - the keyboard detached in portrait.
- **Generated content:** "graph of…", "report on…", "diagram of…". Check that
  there is no chrome, and that moving, resizing and closing work with mouse,
  touch, pen and keyboard.
- **Mods:** create one, change a setting, disable it, remove it.
- **Safety:** run the invoice demo, with its planted injection.
- **Tokens:** compare the token log for the same chart on a small and a large
  spreadsheet.
- **Battery:** 30 minutes idle with the app running compared with not
  running.
- **Offline:** "open X" and on-device routing still work; generating content
  says clearly that it needs a connection.

## Risks and how we'll check them

| Risk | Check |
|---|---|
| Frameless native windows (Win32 popups holding XAML islands) behave differently than expected | M0 spike; fallback is a WinUI window with its title bar and border turned off |
| On-device AI APIs not available, or restricted, on this Surface | M0 check; fallback is our own small classifier on ONNX Runtime, then the local grammar alone |
| Some apps resist being moved: store apps, single-instance apps like Office, apps running as administrator (Windows blocks moving those) | Best effort; leave the window alone rather than fight it, and log it |
| Pixel and scaling mismatches | All coordinates go through one conversion layer; test at 150% and 200% |
| The app can't be built or run in our Linux development container | Windows CI builds every push; you test on the Surface; core logic is unit-tested on any OS |
| Unsigned or test-signed builds blocked | Test certificate now; proper signing before wider testing |
| Preview handlers missing or slow for some types | Fall back to opening in the default app |
| Native chart renderer can't cover a requested chart | Fall back to rendering it in the sandboxed web view |

## How this changes the wider plan

- **Platform order:** Windows 11 ARM64 first, because that is the hardware
  being tested on. Linux follows with the same .NET core; only the shell and
  window-management layer are rewritten for it.
- **Windows equivalents for the Linux plans:**
  - UI Automation instead of AT-SPI, for legacy apps (later phases).
  - Windows Graphics Capture for screen vision, on demand only.
  - Windows Sandbox or AppContainer instead of bubblewrap for running
    commands (Phase 1, not in Build 1).
- **The trade-off we accept:** on Windows this is an app layer within
  Microsoft's rules, and it cannot replace the Windows shell. That suits a
  test build, and the portable core keeps the Linux path open.
