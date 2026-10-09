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
