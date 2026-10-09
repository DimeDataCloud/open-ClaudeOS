# Stack decision for Build 1

**Decision:** C# on .NET 10 (Native AOT), WinUI 3 (Windows App SDK) for
structure, Windows Composition and Win2D for motion and drawing, Win32 and DWM
for windows, Windows ML on the NPU for on-device models, and the official
Anthropic C# SDK for Claude. WebView2 is used only for custom HTML artifacts
and web mods.

**Status:** decided on 2026-10-09, subject to the measurements in M0 (see
"What would change this decision").

## Criteria

These are the owner's criteria, in priority order:

1. **Native, seamless, integrated feel**: looks, moves and behaves like part
   of Windows 11, including on a touch and pen Surface.
2. **Lowest latency**: from a key press to something on screen, and in
   animation smoothness on a 120 Hz display.
3. **Best token efficiency**: fewest model tokens per completed task.

Secondary: an official Claude SDK, how easy it is for open-source contributors,
and a path to Linux.

## What the evidence says

| Finding | Source | What it means for us |
|---|---|---|
| Microsoft is moving Windows' own shell to WinUI 3: File Explorer's Properties dialog has already been rewritten, and the Start menu is expected to follow | [Windows Latest, 2026-05-04](https://windowslatest.com/2026/05/04/windows-11s-windows-95-era-file-explorer-properties-dialog-is-getting-replaced-with-modern-version-and-dark-mode); [Guru3D](https://guru3d.com/story/microsoft-optimizes-winui-3-for-faster-windows-11-interface-response/) | WinUI 3 is what "native" means on Windows 11 from here on |
| WinUI 3 has a reputation for being slower than WPF and UWP. Microsoft's May 2026 work cut File Explorer's WinUI allocations by 41% and function calls by 45%, with the gains opt-in at first | [DevClass, 2026-05-14](https://www.devclass.com/development/2026/05/14/microsoft-aims-to-speed-windows-with-leap-forward-in-winui-3-perf/5239721); [WinUI GitHub discussion #11096](https://github.com/microsoft/microsoft-ui-xaml/discussions/11096) | Opt in to the new optimizations, and keep XAML out of the paths that must be fastest |
| WinUI 3 supports Native AOT (since Windows App SDK 1.6). Microsoft measured a 50% faster start in a sample app and still recommends it in 2026 | [Windows Developer blog](https://blogs.windows.com/windowsdeveloper/2024/11/07/so-whats-new-with-microsoft-native-ux-technologies/); [WinUI landing page](https://developer.microsoft.com/en-us/windows/apps/build-a-windows-app) | Publish with Native AOT: faster start and no just-in-time compilation pauses |
| Windows ML has been generally available since September 2025 (Windows App SDK 1.8.1). Microsoft distributes and updates ONNX Runtime and the Qualcomm NPU execution provider | [Windows Developer blog, 2025-09-23](https://blogs.windows.com/windowsdeveloper/2025/09/23/windows-ml-is-generally-available-empowering-developers-to-scale-local-ai-across-windows-devices); [Qualcomm developer blog](https://www.qualcomm.com/developer/blog/2025/09/accelerate-ai-apps-windowsml-on-snapdragon-x-elite-devices) | Run on-device models through Windows ML, not a bundled ONNX Runtime: a smaller app and system-managed NPU drivers |
| The NuGet package `Anthropic` (version 10 and later) is the official Claude C# SDK. It is still in beta | [Claude C# SDK docs](https://platform.claude.com/docs/en/cli-sdks-libraries/sdks/csharp); [NuGet](https://www.nuget.org/packages/Anthropic/) | Use it, pin the version, and keep it behind one interface |
| The Surface Pro 11th Edition (Snapdragon) has a dynamic refresh rate up to 120 Hz, a 45 TOPS NPU, and a 2880×1920 touch display | [Retail spec listing](https://www.store.utah.edu/Microsoft-Surface-Pro-11-Snapdragon) | Motion must hold 120 Hz; layout must handle about 200% scaling and touch |

## The decision, layer by layer

| Layer | Choice | Why |
|---|---|---|
| Language and runtime | C# on .NET 10 LTS, published with Native AOT | Official Claude SDK, a large contributor pool, and no startup or JIT pauses with AOT |
| Structure (settings, approval cards, lists, text) | WinUI 3, with the 2026 performance optimizations turned on | Real Fluent controls, text, input, accessibility, Mica and Acrylic |
| Motion and the hot surfaces (Intent Bar, Claude's presence, window entrances, live charts) | Windows Composition visuals and animations, plus Win2D for custom drawing | Composition animations run on the system compositor, not the app's UI thread, so motion stays smooth at 120 Hz even while the app is busy. This also keeps WinUI's XAML layout out of the fastest paths |
| Windows | Win32 popups via CsWin32, with DWM for corners, shadow and backdrop, hosting WinUI content as XAML islands | Full control of the window: no frame, sized to content, placed exactly |
| On-device models | Windows ML with the Qualcomm NPU execution provider: an intent classifier, Whisper for speech, and Aion Instruct once it ships | No tokens, private, works offline, and the NPU leaves the CPU free |
| Claude | Official `Anthropic` C# SDK (pinned), behind one interface | A supported SDK, streaming, and tool use |
| Web content | WebView2, only for custom HTML artifacts and web mods | Keeps a browser engine off every normal path |
| Mod scripting | Jint (a JavaScript interpreter written in .NET), sandboxed | No browser engine needed for mod logic; time and memory limits |
| Packaging | MSIX with a project test certificate | Package identity unlocks the Copilot key, Windows' agent APIs and Explorer menus |

## Rejected, and why

| Option | Native feel | Latency | Why rejected |
|---|---|---|---|
| Electron + TypeScript | Low: every window is a web page | Slowest windows; heaviest | Fails the first criterion |
| Tauri 2 + Rust | Medium: interface is web-rendered | Good | Not native-feeling; no official Claude SDK |
| WPF with the Fluent theme | Medium to high | Faster framework today | The older rendering stack, not where Windows is heading; weaker composition effects; awkward web-view hosting |
| C++/WinRT for the whole app | Same as C# WinUI | Slightly lower overhead per call | No official Claude SDK, far fewer contributors, slower development. The framework's own speed, not the app language, is the limit, and Composition already takes motion off the UI thread. We keep C++ in reserve for any component that profiling shows needs it |
| Rust with Win32 and Composition by hand | Could be high, eventually | Excellent | No UI framework, so we would rebuild controls, text input and accessibility ourselves; no official Claude SDK |
| React Native for Windows | High (renders WinUI) | Adds a JavaScript runtime between input and screen | Extra layer, extra latency |
| Flutter, Avalonia | Medium: their own rendering, not Windows controls | Good | Not native text, input or accessibility on Windows |
| Uno Platform | High on Windows (it uses WinUI 3 there) | Same as WinUI | Not needed for Build 1. Kept as the likely Linux shell, because it can reuse WinUI views on Linux |

## Token efficiency is decided by architecture, not language

No stack choice changes token use much. The architecture in
`windows-arm64-plan.md` does:

- zero-token local paths for common requests;
- NPU routing;
- Claude writing a recipe that local code runs over the full data;
- compact specs;
- prompt caching;
- effort set per task.

The stack only needs to make those cheap to build. On-device models through
Windows ML and native renderers that take compact specs both do that.

## What would change this decision

M0 measures these on the Surface. Any failure triggers the listed fallback,
not a restart.

| Check | Target | Fallback if it fails |
|---|---|---|
| Hotkey to Intent Bar visible | under 50 ms | Draw the bar entirely with Composition, without XAML |
| A pre-created subject-only window shown with content | under 100 ms | Pure Composition and Win2D for the window, without a XAML island |
| Intent Bar and presence animations at 120 Hz | no dropped frames, measured with Windows' frame-timing tools | Move the animation to Composition expression animations; remove per-frame UI-thread work |
| Idle memory of the resident app | under 150 MB | Trim; load web views only on demand; unload idle models |
| NPU intent classifier | under 20 ms per query | Use a smaller model, or the local grammar plus Claude for unclear phrasing |
| Claude SDK beta breaks on an upgrade | pinned version keeps working | Upgrade on our schedule; the interface isolates the change |
| XAML island in a Win32 popup misbehaves (focus, input, scaling) | works on the Surface at 200% | A WinUI window with its title bar and border turned off |

## As built (2026-10-09)

What changed between the decision and the first code, and why. None of it
reverses the decision.

| Decision said | Built | Why |
|---|---|---|
| Windows App SDK with Native AOT, Windows ML | **Windows App SDK 2.5.1**, single-project MSIX, self-contained, **not Native AOT**. | Measured, not assumed: see "Native AOT" below. |
| Win32 through CsWin32 | **Hand-written `LibraryImport` and `[UnmanagedCallersOnly]` function pointers** (`Interop/Native.cs`) | About 40 calls. No generator to version, and it is AOT-safe by construction. |
| Subject-only windows are Win32 popups hosting XAML islands | **Frameless WinUI windows** (`OverlappedPresenter.SetBorderAndTitleBar(true, false)`), DWM rounded corners, border color none, dragged with `WM_NCLBUTTONDOWN` | This is the listed fallback for islands misbehaving, taken first because it is the same look with less risk. Islands stay available if focus or scaling problems appear. |
| Win2D for charts | **SVG generated by the core** (`ChartRenderer`), drawn in the shell with native XAML shapes and text (`SvgScene`) | The renderer is portable, testable on Linux, and used by the prototype and the CLI, so one drawing path serves all three. The system SVG image control (`SvgImageSource`) was tried first and ignores `<text>` and 8-digit hex colours, so charts lost every label; native shapes also give sharper text and accessibility. Win2D remains an option for live, high-frequency charts. |
| Windows ML intent classifier on the NPU | **Grammar first, then a CPU classifier** (`LocalIntentClassifier`, naive Bayes, under a millisecond, measured on held-out phrases), then the cloud; `IIntentClassifier` is where an NPU model plugs in | The grammar answers common requests instantly; the classifier catches paraphrases without tokens or a model file. A learned NPU model should beat it on recall; it is not built, and M0 is where to measure whether it is worth it. |
| Phi Silica | Not used | Needs a limited-access token and is being removed. |
| API key in DPAPI | **Credential Locker** (`PasswordVault`), with `ANTHROPIC_API_KEY` as a fallback for developers | It is the per-user encrypted store with an official API, and it needs no key management of our own. |
| Presence drawn with Composition | **Done** (`PresenceOrb`): halo, core and ring as Composition visuals; animation runs on the compositor | As decided. |
| WebView2 for custom HTML artifacts, Jint for mod scripts | **Scripted: built, but not with Jint.** A small total formula language in the core (no loops, no assignment, no calls out) with a step budget; **web: not built**, refused with a clear message | A sandbox you do not have to build is the safest one: formulas can only calculate, so there is nothing to escape into, and the core stays dependency-free and AOT-clean. Web mods still need their WebView2 sandbox first. |

### Native AOT: measured, and the answer is "not with this SDK"

The decision assumed the shell would publish with Native AOT. It was measured on
2026-10-09 by publishing the CLI (which has the same core and the same SDK
adapter) ahead of time on Linux and running it:

- **`ClaudeOS.Core` is AOT-clean.** The CLI's `route`, `chart`, `apply`, `undo`
  and `theme` commands run correctly as a native binary (17 MB; routing a request
  takes about 0.2 ms). CI keeps this true (`native-aot` job).
- **The adapter is clean** under the AOT analyzers (no reflection-based JSON in our
  code).
- **The official Anthropic C# SDK (12.54.1) is not AOT-compatible.** The publish
  warns at the assembly level, and at run time the first request fails: the SDK's
  enum and content types are serialized with reflection-based `System.Text.Json`,
  which Native AOT disables. Turning reflection back on does not fix it either:
  the SDK's converters for its immutable collections are trimmed away.

(A Native AOT *publish* of the shell itself does compile in CI. Re-run on 2026-10-09 from
the *Package (Windows)* workflow's on-demand probe, at `4647a75` for `win-x64`: it published in
about three minutes with no errors. The warnings were the SDK's own (`IL2104` and `IL3053`
on `Anthropic.dll`, the same ones that predict the runtime failure above), `CsWinRT1028`
(`PresenceOrb` is not marked `partial`), and two ordinary compiler warnings, `CS0252` in
`IntentBarWindow.xaml.cs` and `CS8602` in `SelfTest.cs`. The result was not run: it would
hit the same SDK failure at the first Claude request, so there is no point until the SDK or
the adapter changes. "It builds" is not "it works".)

So the shell ships as a self-contained MSIX on the regular .NET runtime. The
Intent Bar is created once at start-up and only shown and hidden afterwards,
which is what keeps hotkey-to-visible short; ReadyToRun compilation is the next
lever for cold-start time if M0 shows it is needed. If M0 shows start-up time
matters more than the SDK, the escape hatch already exists: `IModelClient` is the only seam, so `ClaudeOS.Claude` can be replaced by
a small source-generated HTTP client for the Messages API, and the whole app
can then go ahead-of-time. Until then the project uses the supported SDK.
