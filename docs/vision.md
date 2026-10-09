# Vision: the original concept

> This is the founding concept for the project, kept as it was first written.
> The design has since been revised in places; see [architecture.md](architecture.md)
> for the current design and the reasons for each change.

Building a "Claude OS" shifts the fundamental computing paradigm from an
**App-Centric** model (Windows and macOS) to an **Intent-Centric** model.

Instead of opening individual applications, managing window layouts, and
manually copying data between software silos, Claude OS would treat the desktop
interface as a unified workspace where natural language, visual context, and
autonomous agents drive the operating system.

## 1. Core operating system features

| Feature | Traditional OS (Windows / macOS) | Claude OS concept |
|---|---|---|
| User interface | Fixed launcher, taskbar/dock, static file directories. | Dynamic "Artifact Canvas" where apps, code, and documents construct themselves around your active task. |
| App execution | API calls or manual human mouse/keyboard clicks. | Native Computer Use Engine where Claude directly navigates desktop interfaces and legacy software. |
| System security | Binary permissions (Allow/Deny application access). | Constitutional System Guardrails that evaluate semantic intent before executing file edits or network requests. |
| File indexing | Drive paths and file name search (`C:\Docs\file.pdf`). | Unified Knowledge Context: system-wide vector indexing connecting local files, past conversations, and project state. |

### Native Computer Use Kernel

Rather than requiring every app developer to publish a modern REST API, Claude
OS would feature a system-level vision driver built on computer use
capabilities. The OS can read pixels, interpret screen layouts, and dispatch
low-level mouse and keyboard events safely inside isolated user sandboxes.

### Living Artifacts Workspace

The desktop surface replaces traditional app windows with dynamic Artifacts.
When you ask Claude OS to analyze a dataset, it doesn't just open Excel; it
dynamically generates an interactive dashboard widget, a Python processing
script, and a summary document live on your screen as native UI elements.

### Constitutional Permissions Manager

Traditional OS security prompts ask zero-context questions like "Allow App X to
access your camera?" Claude OS provides semantic context: "Claude wants to open
14 invoices in Acrobat, summarize the amounts into a spreadsheet, and send a
draft email. [Approve Actions]"

## 2. Technical and architectural stack

```
 ┌─────────────────────────────────────────────────────────┐
 │               Claude OS User Interface                  │
 │       (Intent Bar, Dynamic Artifacts, Agent Canvas)     │
 └────────────────────────────┬────────────────────────────┘
                              │
 ┌────────────────────────────▼────────────────────────────┐
 │               System Orchestration Engine               │
 │  (Intent Parser • Local/Cloud Model Router • Guardrails)│
 └───────┬────────────────────┬────────────────────┬───────┘
         │                    │                    │
 ┌───────▼────────┐  ┌────────▼────────┐  ┌────────▼────────┐
 │ Vision/Control │  │ Semantic Memory │  │ MicroVM Sandbox │
 │ (Screen Capture│  │ (RAG Vector OS  │  │ (Agent Workflow │
 │ & HID Drivers) │  │  Drive Index)   │  │  Execution)     │
 └────────────────┘  └─────────────────┘  └─────────────────┘
```

- **Base layer (lightweight Linux/microkernel):** handles basic hardware
  abstraction, memory allocation, display protocols (Wayland), and device
  drivers.
- **Hybrid model routing:** a small, ultra-fast model running locally on an
  NPU/GPU manages sub-100ms UI actions, window positioning, and simple system
  commands. Complex reasoning, deep coding, and multithreaded workflows
  automatically route to cloud models.
- **MicroVM agent sandboxing:** whenever an automated agent performs
  system-wide modifications (writing code, organizing file systems, running
  bash scripts), it executes inside an isolated MicroVM before applying changes
  to the main system state.

## 3. Key engineering challenges

- **Latency expectations:** computer UIs require near-instant response times
  (<16ms for rendering, <100ms for inputs). Generative AI tokens currently
  operate on 200ms–2s latencies, requiring heavy local predictive caching.
- **Offline execution:** if internet connectivity drops, cloud-dependent
  operating systems lose core functionality. A viable Claude OS needs a
  capable, quantized local LLM for offline fallback.
- **Token and compute costs:** running continuous vision-based screen recording
  loops to monitor user context generates high token consumption. The system
  must rely on event triggers rather than constant polling.
