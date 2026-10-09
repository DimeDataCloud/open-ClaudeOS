# Build 1: Windows on ARM test build

The first build you can install and use runs on **Windows 11 ARM64**, tested on
a Surface Pro. It is an app layer on top of Windows, not a new operating
system: Windows keeps handling drivers, the desktop and apps, and this build
adds the intent layer on top. Linux follows later using the same core.

This is a plan. Nothing in it is built yet.

## What Build 1 should feel like

You press a key and say what you want. The system works out whether you asked
to *see* something that exists or to *make* something new, and puts the
result on screen neatly, without disturbing what you were doing.

| You say | What happens |
|---|---|
| "open the Q3 budget" | The file opens straight away, in the space that is free on screen. No model call is needed, and no approval is asked for: opening a file changes nothing. |
| "show me spending by month from the Q3 budget as a graph" | A small window appears where there is room, holding only the graph: no title bar, no toolbar, no borders, no tags. |
| "write a one-page report on these invoices" | A report window streams in: only the report. |
| "make me a widget with battery and my next meeting, top right" | Claude writes a mod. You see its code and what it is allowed to access, approve once, and it appears. |
| "rename these files by date" | This changes your files, so it goes through the approval card from Phase 0, and Undo works afterwards. |

**The rule behind it.** Retrieving and viewing is instant and needs no
approval. Making something new produces new files only, so it applies straight
away with Undo available. Anything that changes existing files or leaves the
machine goes through the approval card.

## Two kinds of window

### Opening existing files: "just open it"

- **Files we can show ourselves** (images, PDF, text, Markdown, code, CSV,
  audio, video) open in our own content-only viewer, so placement and
  appearance are fully under our control.
- **Everything else** (Word, Excel, design files) opens in its default app,
  and then the layout engine moves the app's window into the free space.
  This is best effort: some apps resist being moved (see Risks).
- **Finding the file** uses the Windows Search index first, then a scoped
  scan of Desktop, Documents and Downloads. If several files match, a small
  picker appears.

### Things that are made, not retrieved: subject-only windows

A subject-only window shows exactly what was asked for and nothing else.

- **No visible chrome**: no title bar, no borders, no scroll bars unless the
  content overflows, and no headers, tags or toolbars. Rounded corners and a
  soft shadow come from the window itself.
- **Sized to the content.** A wide chart gets a wide window; a portrait report
  gets a portrait one.
- **Controls without chrome:**
  - Drag anywhere on the content to move; drag the edges to resize.
  - Esc or Ctrl+W closes; double-click switches between fit-to-content and
    larger.
  - Right-click, or press and hold on touch, opens the only menu: Pin on top,
    Save as, Copy, Open in app, Edit with Claude ("make the bars blue"),
    Close.
  - Pen and touch work the same way.
- **Accessible anyway.** Each window still has a name for screen readers
  ("Chart: spending by month"), and is reachable by keyboard and listed in
  the Intent Bar's window list.
- **Assembles live.** The window appears at its final position as soon as the
  request is understood. Report text streams in as it is written; a chart
  appears the moment its data is complete.

**Trusted renderers by content type**, so most artifacts are data rendered by
code we control, not arbitrary scripts:

| Type | Claude produces | Rendered by |
|---|---|---|
| Chart | Vega-Lite spec plus data | Bundled Vega renderer, with its menus turned off |
| Table | CSV or JSON rows | Built-in table view |
| Report | Markdown | Sanitized HTML |
| Diagram | Mermaid | Bundled Mermaid renderer |
| Image | SVG | Sanitized SVG |
| Custom | HTML/JS app | Sandboxed view: no network and no file access unless granted |

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
   - Line up with screen edges and other windows, with a consistent gap.
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
| Theme | Fonts, colours, corner radius, translucency for every window and the Intent Bar |
| Hook | Runs when a file opens, an artifact is created, or a plan is approved |
| Connector | An MCP server that adds a data source or an action |

**What a mod is.** A folder holding a `mod.json` manifest and its files, in
`%APPDATA%\ClaudeOS\mods\`:

```json
{
  "id": "battery-and-next-meeting",
  "name": "Battery and next meeting",
  "version": "0.1.0",
  "kind": "widget",
  "entry": "index.html",
  "placement": { "anchor": "top-right", "size": [220, 90] },
  "capabilities": ["system.battery", "calendar.read.next"],
  "settings": {
    "accent": { "type": "color", "default": "#7aa2f7" },
    "showSeconds": { "type": "boolean", "default": false }
  }
}
```

**Making a mod by asking.** "Make me a widget that…" leads Claude to write
the folder. Before anything is installed, the approval card shows:

- the code;
- the capabilities it asks for, in plain words;
- where it will appear.

Approve once and it loads. "Make the clock bigger" edits the mod the same
way.

**How mods are kept safe:**

- Each mod runs in its own sandboxed view, with no network and no file access
  unless its manifest declares it and you approved it.
- The core enforces capabilities on every call a mod makes.
- No mod can reach the approval, policy or undo code.

**Built to be changed:**

- A mod's `settings` block generates its settings screen automatically.
- Mods reload live when edited.
- A mod can be shared as a folder, zip or Git repository.
- Disabling a mod is one click; removing it is undoable.

## Recommended stack

All three options run natively on ARM64. The deciding factors are speed to a
first build, the official Claude SDK, and how much control there is over
frameless windows.

| | Electron + TypeScript (recommended for Build 1) | Tauri 2 + Rust | .NET + WinUI 3 |
|---|---|---|---|
| Claude SDK | Official TypeScript SDK | No official Rust SDK; we would write the HTTP client ourselves | Official C# SDK |
| Frameless, transparent, content-sized windows | Mature and easy | Supported | Workable, but fiddly with embedded web content |
| Mod sandboxing | Isolated views with a narrow, checked bridge | Per-window permission lists built in, a strong fit | Must build our own |
| Footprint and battery | Heaviest: ships its own browser engine | Lightest: uses the Windows web runtime | Light |
| Contributors | Largest pool | Smaller (Rust) | Windows developers |

**Recommendation.** Use Electron + TypeScript for Build 1, to get a working
build onto the Surface fastest. Keep the core (planner, policy, consent, undo,
layout engine) in its own TypeScript package with no UI code, so the shell
could move to Tauri later if battery life calls for it. The Python Phase 0
prototype becomes the reference: its tests are ported to TypeScript and must
keep passing.

**Windows pieces under this stack:**

- **Window management:** Windows APIs called from the main process, to list
  windows, read their bounds and move them. A small native helper program is
  the fallback.
- **Rendering:** a pool of frameless windows, reused to save memory and
  battery.
- **Summoning the Intent Bar:** a global hotkey. We will also test whether the
  Surface's Copilot key can be used.
- **API key storage:** encrypted with Windows' own per-user data protection,
  through Electron's `safeStorage`.
- **Deleting your files:** deletes go to the Recycle Bin, which is Windows'
  own undo.
- **Model:** `claude-opus-5-5` for planning and generation. Simple requests
  ("open X") never call a model.

## Getting the build onto the Surface

1. Every push builds a Windows ARM64 package on GitHub Actions, as an
   installer and a portable zip.
2. You download it from the Actions run (later, from Releases) and run it.
3. The early builds are unsigned, so Windows SmartScreen will warn; choose
   "More info" then "Run anyway". If Smart App Control blocks the build
   outright, don't turn Smart App Control off. We should sign the build
   instead, with a code-signing certificate or Microsoft's cloud signing
   service.
4. On first run, the app asks for your Claude API key and stores it encrypted.

## Milestones

| Milestone | Delivers | Done when |
|---|---|---|
| **M0: spike** | App starts natively on ARM64; lists windows and moves one; a frameless window shows a chart; the hotkey works | All four work on your Surface |
| **M1: open anything** | Intent Bar, file search, content-only viewer, default-app opening with placement, layout engine, "put it back" | "open X" lands in free space in the test arrangements below |
| **M2: make things** | Claude planner with scoped read-only tools and a `create_artifact` tool; subject-only windows for charts, tables, reports, diagrams, images; artifacts saved as files | Each content type renders with no chrome and streams in live |
| **M3: mods** | Manifest format, loader, sandbox, capabilities; widget, artifact-type, command, layout-rule and theme mods; "make me a mod" flow | You can create, tweak, disable and remove a mod by asking |
| **M4: safe changes** | The Phase 0 core ported: approval cards, plan-bound grants, undo journal, Recycle Bin deletes, dry-run outbox | The invoice injection demo is contained on Windows |

Build 1 is M0 through M4. The protected-path and scoped-read rules from Phase
0 are in from M2, because that is when Claude starts reading files.

## Test checklist for the Surface

- **Install and run:** check SmartScreen and Smart App Control behaviour, and
  that the first-run key setup works.
- **Hotkey:** the Intent Bar appears in under 100 ms.
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
- **Battery:** 30 minutes idle with the app running compared with not
  running.
- **Offline:** "open X" still works; generating content says clearly that
  it needs a connection.

## Risks and how we'll check them

| Risk | Check |
|---|---|
| Native calls from Electron on ARM64 | M0 spike; fallback is a small native helper program |
| Some apps resist being moved: store apps, single-instance apps like Office, apps running as administrator (Windows blocks moving those) | Best effort; leave the window alone rather than fight it, and log it |
| Pixel and scaling mismatches between Windows APIs and the app | Convert all coordinates in one place; test at 150% and 200% |
| Unsigned builds blocked | Plan for signing before wider testing |
| Battery cost of many windows | Reuse windows; close idle ones; measure in the checklist |
| Search index missing folders | Fall back to scanning Desktop, Documents and Downloads |
| Copilot key not available to third-party apps | Use a normal global hotkey |

## How this changes the wider plan

- **Platform order:** Windows 11 ARM64 first, because that is the hardware
  being tested on. Linux follows with the same core; only the shell and the
  window-management layer differ.
- **Windows equivalents for the Linux plans:**
  - UI Automation instead of AT-SPI, for legacy apps (later phases).
  - Windows Graphics Capture for screen vision, on demand only.
  - Windows Sandbox or AppContainer instead of bubblewrap for running
    commands (Phase 1, not in Build 1).
- **The trade-off we accept:** on Windows this is an app layer within
  Microsoft's rules, and it cannot replace the Windows shell. That suits a
  test build, and keeping the core portable keeps the Linux path open.
