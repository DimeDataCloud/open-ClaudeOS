# Competitive landscape for AI-native OS layers (as of 2026-10-09) and implications for open-ClaudeOS

Status labels used throughout: **SHIPPED** (generally available to the public), **PREVIEW** (Insider, beta, developer or public preview, or experimental), **ANNOUNCED** (stated by the vendor but not yet available), **UNCONFIRMED** (secondary reporting only, or code findings). Every date is the date of the source or event named. Project context comes from `docs/windows-arm64-plan.md`, `docs/architecture.md` and `docs/challenges.md`.

## 1. Platform comparison: natural-language creation, how agents act, consent, on-device vs cloud

### Takeaway
By October 2026 every platform owner has some natural-language creation feature, but they differ in what they build. Google builds desktop widgets (Googlebook "Create your Widget"). Apple builds automations and browser extensions ("Describe a Shortcut", "Describe an Extension" in macOS 27). Microsoft's builder is in Microsoft 365 Copilot (App Builder, Workflows, Copilot Cowork app-building), not the Windows shell. Agents act through two channels. The structured channel is MCP on Windows and App Intents on Apple. The pixel channel is computer use in Copilot Actions, which clicks, types and scrolls. Windows' agent pieces (agent workspace, Copilot Actions, MCP registry, Execution Containers) are still preview or experimental. Consent is coarse on every platform: per agent, per folder, per host app. No platform binds consent to the exact actions it covers.

### Cited Findings

#### Google: Googlebook (brief; another researcher covers it in depth)
- ANNOUNCED on 2026-05-12. Googlebook is a new laptop category "designed for Gemini Intelligence" that combines Android and ChromeOS, described as "the first laptops designed from the ground up for Gemini Intelligence". The OEM partners are Acer, ASUS, Dell, HP and Lenovo, with devices expected "this fall". — [Google blog, "Introducing Googlebook"](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- Natural-language creation: "Create your Widget" has Gemini build a personalised desktop dashboard, combining web search with Gmail and Calendar. — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- Magic Pointer, built with Google DeepMind, gives "quick, contextual suggestions every time you point at something on your screen", for example setting up a meeting from a date in an email. — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- Google's post does not say which features run on the device and which in the cloud, and it gives no specific privacy or consent terms for Magic Pointer or widgets. — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- UNCONFIRMED (secondary): press coverage gives examples of generated widgets (a sports-score tracker, a trip countdown that pulls flight and hotel details from Gmail). It also describes a "Rambler" voice-to-structured-notes tool and says Magic Pointer activates only on request and can be turned off. — [TechRepublic](https://www.techrepublic.com/article/news-googlebook-gemini-ai-laptops/), [Digital Trends](https://www.digitaltrends.com/computing/google-just-announced-a-new-kind-of-laptop-and-it-puts-gemini-everywhere/), [Neowin](https://www.neowin.net/news/google-unveils-googlebook-a-new-gemini-first-laptop-category-powered-by-a-new-modern-os/). Exactly which outlet said what could not be checked from search snippets.
- UNCONFIRMED: a Business Standard headline (URL dated about 2026-09-22) reads "Googlebook laptops debut with Gemini, Android apps support". The page returned 403, so the launch date and which features shipped at launch were not checked. — [Business Standard](https://www.business-standard.com/amp/technology/tech-news/googlebook-android-laptops-gemini-hp-dell-asus-acer-lenovo-launch-126092200274_1.html)

#### Microsoft: Windows 11 (Copilot, Copilot+ PCs, agentic features)
**Natural-language creation**
- No Windows-shell feature for building widgets or mini-apps from natural language was found. Microsoft's app builders live in Microsoft 365. **App Builder** (a Microsoft 365 Copilot agent, announced in October 2025 for Frontier-programme customers) builds business apps with dashboards, charts, calculators and lists, and stores their data in Microsoft Lists. **Workflows** turns plain-language descriptions into multi-step automations across Outlook, SharePoint, Teams and Planner. — [Redmondmag, 2025-10-31](https://redmondmag.com/articles/2025/10/31/microsoft-365-copilot-gains-app-builder.aspx); [Neowin](https://www.neowin.net/news/new-microsoft-365-copilot-agent-builds-apps-from-simple-prompts/)
- PREVIEW: Message Center entry MC1469329 announces a preview of app-building in Copilot Cowork from 2026-09-08, with Copilot Studio to follow. — [MC1469329 mirror](https://mc.merill.net/message/MC1469329)
- The Settings agent, announced June 2025, maps natural-language requests ("my mouse cursor is too small") to Settings function calls. It runs on **Mu**, a 330M-parameter encoder-decoder model fully offloaded to the NPU (over 100 tokens/s), with no data sent to the cloud. Coverage at the time said it was in Insider preview, English only, on Snapdragon Copilot+ PCs first. — [Neowin](https://www.neowin.net/news/microsoft-reveals-mu-an-on-device-small-language-model-built-into-windows-11/), [Computerworld](https://www.computerworld.com/article/4011232/microsofts-new-genai-model-to-power-agents-in-windows-11.html), [Thurrott](https://www.thurrott.com/windows/windows-11/322465/the-settings-agent-)

**How agents act**
- Copilot Actions (PREVIEW, experimental, Copilot Labs for Windows Insiders) is "an AI agent that completes tasks for you by interacting with your apps and files, using vision and advanced reasoning to click, type, and scroll like a human would". This is computer use. — [Windows 11 security book: Agentic security (Microsoft Learn, 2025-11-13)](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security)
- Agent workspace (PREVIEW): "A contained environment where agents can work in parallel with a human user… This environment provides the agent with capabilities like its own desktop while limiting the visibility and access the agent has to the user's desktop activity." Actions run under a separate standard "agent account". — [Security book](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security)
- Timeline of the support page: first published 2025-10-16; updated 2025-11-17 (Agent Workspace and Copilot Actions roll out to Insiders); updated 2025-12-05 (agent connectors supported in the agent workspace). The workspace is "available in a private preview for Windows Insiders", and Microsoft says more granular controls will come "before it is made generally available". — [Microsoft Support: Experimental agentic features](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- MCP on Windows / On-device Agent Registry (ODR), PREVIEW: "a secure, manageable interface to discover and use agent connectors from local apps and remote servers using MCP". It includes a File Explorer MCP server and a Windows Settings connector, MCP servers "contained in a separate environment by default", control per agent through Windows Settings and Intune, logging and auditability, and the `odr.exe` command-line tool. The page carries Microsoft's "prerelease product" notice (page updated 2026-06-04). — [Microsoft Learn: MCP on Windows overview](https://learn.microsoft.com/en-us/windows/ai/mcp/overview)
- Native MCP support entered public preview at Ignite 2025 (2025-11-18), with built-in File Explorer and System Settings connectors, and Windows will accept third-party local and remote connectors. — [Windows Developer Blog, Ignite 2025](https://blogs.windows.com/windowsdeveloper/2025/11/18/ignite-2025-furthering-windows-as-the-premier-platform-for-developers-governed-by-security/); [Neowin](https://www.neowin.net/news/microsoft-is-making-ai-agents-a-first-class-component-of-windows/)
- Agent Launchers (PREVIEW; doc updated 2025-12-12) let an app register an AI agent once so it can be found "from the Start menu, search, or within applications". They are built on App Actions: an agent definition JSON plus an App Action with required `agentName` and `prompt` inputs, registered through the ODR, statically at install or dynamically at runtime. — [Microsoft Learn: Agent Launchers overview](https://learn.microsoft.com/en-us/windows/ai/agent-launchers/)
- Taskbar agents: a Release Preview build on 2026-04-17 added agents to the taskbar, with Microsoft 365 Researcher as the first adopter. Microsoft hopes third parties will join through the Windows Agent API. Microsoft documentation quoted by the outlet says "Ask Copilot" on the taskbar is "not yet generally available" and expected mid-2026. It is off by default, and it is reported not to be a default on consumer PCs. — [Windows Latest, 2026-04-18](https://www.windowslatest.com/2026/04/18/microsoft-confirms-ai-agents-are-still-coming-to-the-windows-11-taskbar-as-it-prepares-for-public-rollout/)
- Click to Do (SHIPPED on Copilot+ PCs) uses the local Phi Silica model to "connect actions to the content (text or images) on the screen". "The analysis of the screen is always performed locally on the device. Content is only shared if the user chooses to complete an action." — [Microsoft Learn: Click to Do overview](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/click-to-do)
- Build 2026 (2026-06-02/03) announcements and their status:
  - **Microsoft Execution Containers (MXC) SDK**, early preview: "a cross-platform, policy-driven execution layer for agents across Windows and WSL".
  - Process and session isolation: early preview for Insiders "shortly after Build".
  - Micro-VMs and Linux containers: roadmap, no dates.
  - Windows 365 for Agents: GA within Agent 365.
  - Agent 365 native integration: preview in July.
  - **Aion 1.0 Instruct**: preview in Edge Insider channels, with open weights on Hugging Face in July.
  - **Aion 1.0 Plan**: "in the coming months". The post says elsewhere that it "ships in-box as part of Windows on capable devices", so it is internally inconsistent.
  - Windows AI APIs expanding to CPU and GPU: public preview.
  - Intelligent Terminal: experimental; it gives context to agents over ACP.
  - Windows Development Skills: GA.
  - OpenClaw "runs its node and gateway on Windows via MXC" and is "available in open-source".

  — [Windows Developer Blog: Build 2026](https://blogs.windows.com/windowsdeveloper/2026/06/02/build-2026-furthering-windows-as-the-trusted-platform-for-development/)
- The Build 2026 security post says session isolation separates an agent from "the user's interactive desktop, clipboard, UI and input devices". "Windows assigns a local ID or a cloud provisioned identity backed by Entra". Governance is administrative: "Teams can use Intune policies to require MXC isolation with guardrails such as filesystem rules." The post says nothing about end-user consent. — [Windows Developer Blog: Windows platform security for AI agents](https://blogs.windows.com/windowsdeveloper/2026/06/02/windows-platform-security-for-ai-agents/)
- UNCONFIRMED (secondary): the MXC repository reportedly says no MXC profile should currently be treated as a security boundary. — [windowsforum summary](https://windowsforum.com/news/microsoft-execution-containers-securing-agentic-ai-on-windows-and-wsl.421682/) (repository not checked directly)

**Permission and consent model**
- The experimental agentic features toggle is off by default, can only be turned on by an administrator, and applies device-wide. When on, agents can ask for read/write access to six known folders (Documents, Downloads, Desktop, Music, Pictures, Videos). Each agent gets "Allow Always / Ask every time / Never allow", with "Ask" as the default (builds 26100.7344+). The stated principle is "Users approve all queries for user data as well as actions taken". Permissions should be "granular, specific and time bound", and logs should ideally be tamper-evident. — [Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- "Agents that integrate with Windows must be signed by a trusted source." Copilot Actions "might request additional user approval" for sensitive steps, and users can "monitor, and take over agent actions in agent workspace". — [Security book](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security)
- MCP file permissions are granted **per host, not per server**: "Once the user grants access, all MCP servers used by that host app will have access to the resource." A contained server **can access the internet**. — [Microsoft Learn: Securely containing MCP servers](https://learn.microsoft.com/en-us/windows/ai/mcp/servers/mcp-containment)

**On-device vs cloud**
- On-device: Phi Silica, OCR, imaging and the Mu Settings model run on the NPU of Copilot+ PCs (see Q2). Click to Do analyses the screen locally. Copilot itself, Copilot Actions planning and M365 App Builder are cloud services. The cloud side is inferred from these being Copilot or M365 services; no source states it in one sentence.

#### Apple: Apple Intelligence on macOS and iPadOS
- WWDC26 keynote on 2026-06-08. macOS 27 "Golden Gate" was **SHIPPED** on 2026-09-14, and 27.0.1 on 2026-09-28. — [Neoteo](https://www.neoteo.com/en/apple-released-macos-27-golden-gate-on-september-14-2026), [Macworld](https://www.macworld.com/article/3139330/macos-27-mac-features-siri-apple-intelligence-release-date-compatibility.html)
- From the macOS 27 release notes (SHIPPED, 2026-09-09/14):
  - Siri AI "can get things done across your apps, just by asking".
  - "Siri can answer questions or take action with what's onscreen".
  - Visual Intelligence comes to the Mac (Cmd-Shift-Space).
  - Shortcuts lets you "automate daily tasks with a simple description" (**Describe a Shortcut**), and Safari adds **Describe an Extension**.
  - "Siri AI is rolling out to users… turning it on in Settings".
  - "Usage limits may apply" to Siri AI, Image Playground and "AFM 3 Cloud models in Shortcuts".

  — [9to5Mac: macOS 27 full release notes](https://9to5mac.com/2026/09/09/macos-27-golden-gate-here-are-apples-full-release-notes/)
- UNCONFIRMED (secondary): Siri AI shipped labelled beta, English-only, with daily usage limits. — [Gadget Hacks](https://apple.gadgethacks.com/news/macos-27-siri-ai-features-whats-new-and-who-can-use-it/)
- The new Apple Intelligence models were "custom-built in collaboration with Google and its Gemini models", as press quotes Apple's materials. They run on-device or on Private Cloud Compute. — [Business Standard](https://www.business-standard.com/amp/technology/tech-news/wwdc-2026-apple-unveils-siri-ai-gemini-powered-apple-intelligence-more-126060900042_1.html?isa=yes), [CNBC live blog](https://www.cnbc.com/2026/06/08/apple-wwdc-2026-live-updates.html)
- Agentic example: the Passwords app will use Apple Intelligence and Safari to "agentically take action on your behalf", going to each site to change insecure passwords. — [CNBC](https://www.cnbc.com/2026/06/08/apple-wwdc-2026-live-updates.html)
- Developer surface (WWDC26):
  - The Foundation Models framework now works with "any language model, including Apple Foundation Models, cloud models like Claude and Gemini, or any other provider that conforms to the Language Model protocol".
  - Multimodal prompts, with Vision tools (OCR, barcodes) callable on-device.
  - "Dynamic Profiles" to swap models and tools.
  - Free Private Cloud Compute access for Small Business Program apps with fewer than 2M downloads.
  - App Intents **schemas**: entity schemas feed the Spotlight semantic index, and intent schemas let people act "with no specific phrases to define".
  - A **View Annotations API** maps views to entities for on-screen actions.
  - An App Intents Testing framework.

  — [Apple Developer: WWDC26 Apple Intelligence guide](https://developer.apple.com/wwdc26/guides/apple-intelligence/)
- UNCONFIRMED (search summary of an Apple Q&A session): there is "no API for a third-party app to invoke another app's intents directly". Siri and Shortcuts are the orchestrators. — [Apple Developer: Apple Intelligence Group Lab, WWDC26](https://developer.apple.com/videos/play/wwdc2026/8011/)
- Shortcuts "Use Model" (SHIPPED with macOS 26 Tahoe in 2025) sends prompts to On-Device, Cloud (Private Cloud Compute), Cloud Pro, or an Extension Model (ChatGPT). Inputs can include variables, calendar events, reminders and photos. — [Apple Support: Use Apple Intelligence in Shortcuts on Mac](https://support.apple.com/guide/shortcuts-mac/mchl91750563/mac). Reviewers found results "neither reliable nor necessarily repeatable". — [Michael Tsai, 2025-07-16](https://mjtsai.com/blog/2025/07/16/shortcuts-in-macos-tahoe/)
- MCP: code in the iOS/iPadOS/macOS 26.1 betas (September 2025) suggested MCP support was being built into App Intents. This was UNCONFIRMED and never announced. — [AppleInsider, 2025-09-22](https://appleinsider.com/articles/25/09/22/ios-26-could-get-a-major-ai-boost-with-the-model-context-protocol), [Computerworld](https://www.computerworld.com/article/4061614/apple-to-deploy-mcp-support-for-powerful-ai-experiences.html). No official source confirming native MCP in macOS 27 was found.
- Third-party bridge: `claude-siri-ai` is an "experimental macOS 27 App Intents model delegation provider backed by Claude Code". It needs SIP and AMFI disabled. — [GitHub: mpociot/claude-siri-ai](https://github.com/mpociot/claude-siri-ai)

#### Startups and open-source projects
- **Nothing Playground / Essential Apps** (beta launched 2025-09-30): users generate home-screen widgets from prompts (flight tracker, next-meeting brief) and can edit the code. Full-screen apps are not supported. Only three permissions are supported (Location, Calendar read-only, Contacts), with camera, microphone, notifications and networking promised. Beta on Nothing Phone (3). — [TechCrunch, 2025-09-30](https://techcrunch.com/2025/09/30/nothing-launches-ai-tool-for-building-mini-apps-using-prompts/), [GSMArena](https://www.gsmarena.com/nothings_personalized_aigenerated_essential_apps_now_in_beta-news-71496.php)
- **Wandesk** (Sider-ai, ISC licence; Electron builds for macOS and Windows): "One shell, one AI kernel… one workerd. Everything else is an app." The built-in agent writes each app as a Cloudflare Worker site with its own origin and SQLite database. It is bring-your-own-model ("any Responses- or Chat-Completions-compatible endpoint"). It warns that the agent has an "unsandboxed shell" and every binding is "wide open". 68 stars when checked. — [GitHub: Sider-ai/wandesk](https://github.com/Sider-ai/wandesk)
- **VibeOS**: a browser-based OS where every app's UI is generated live. Seen only in a GitHub topic listing, not inspected. — [GitHub topic: ai-desktop](https://github.com/topics/ai-desktop)
- **Agent S** (Simular): "an open agentic framework that uses computers like a human" (computer use). — [GitHub: simular-ai/Agent-S](https://github.com/simular-ai/agent-s)
- **OpenClaw**: open source, and named at Build 2026 as running its node and gateway on Windows through MXC. — [Windows Developer Blog: Build 2026](https://blogs.windows.com/windowsdeveloper/2026/06/02/build-2026-furthering-windows-as-the-trusted-platform-for-development/)
- Other open desktop agent projects appear in listings: AionUi (multi-agent desktop over 20+ AI CLIs), PawWork (positioned as an open alternative to Codex App and Claude Cowork), and Goose (Apache-2.0 desktop and CLI agent). None was inspected. — [GitHub topic: desktop-ai-agent](https://github.com/topics/desktop-ai-agent), [OpenHands blog](https://www.openhands.dev/blog/open-source-ai-coding-agents)

### Inferences
- Comparison at a glance (synthesised from the findings above):

  | | Googlebook | Windows 11 | macOS 27 | Open source / startups |
  |---|---|---|---|---|
  | NL creation | Desktop widgets from Gemini + Gmail/Calendar/web (announced May 2026) | None in the shell; M365 App Builder, Workflows, Cowork app-building (cloud, business) | Describe a Shortcut, Describe an Extension (shipped Sept 2026) | Nothing widgets (beta); Wandesk apps (alpha-quality, unsandboxed) |
  | Agent channel | Not documented in Google's post | MCP/ODR + App Actions (structured) and Copilot Actions (computer use); all preview | App Intents schemas + View Annotations; Siri/Shortcuts orchestrate; no MCP | Mostly computer use or unsandboxed shell |
  | Consent unit | Not documented | Per agent, per known folder (Always/Ask/Never); per host for MCP files; admin-wide toggle | Per app via App Intents (developer-defined) | Mostly none |
  | Model | Gemini | Microsoft (Copilot cloud; Phi Silica/Aion/Mu on device) | Apple FMs built with Gemini; on-device + PCC | BYO model (Wandesk) |

- The "structured first, pixels last" order in `architecture.md` matches where Microsoft and Apple are heading: MCP and App Intents schemas first, with computer use reserved for Copilot Actions.
- Microsoft's consent model is built for IT and enterprise governance (Intune, Entra, Agent 365). It is not built for a person reviewing exact effects. That difference is open-ClaudeOS's opening (see Q3).
- Natural-language widget creation is becoming a standard feature (Google, Nothing; Apple for automations). Windows is the outlier without one in the shell, which makes open-ClaudeOS's mods both an opportunity and a likely future collision.

### Gaps
- Could not read the Build 2026 keynote transcript (the Visual Studio Magazine summary returned 403). I found no Microsoft source giving September or October 2026 status (GA or not) for Copilot Actions, the agent workspace or the taskbar's Ask Copilot. The latest dated coverage is April 2026.
- Not checked: whether the Mu Settings agent is GA on Snapdragon Copilot+ PCs today.
- Googlebook: no source says what is on-device and what is cloud, what agent or computer-use features exist, or what shipped at the autumn launch. This is left to the dedicated researcher.
- Apple: the exact consent flow for Siri AI cross-app actions and the "Describe a Shortcut" review step were not found. MCP status in macOS 27 is unconfirmed (no evidence it shipped).
- Microsoft's Settings MCP connector page and the MCP host quickstart were not fetched.

## 2. What a third-party app on Windows 11 ARM64 (Copilot+ PC, Surface Pro) can use today, and the requirements

### Takeaway
A packaged (MSIX, signed) app on a Snapdragon Copilot+ Surface can use the on-NPU Windows AI APIs today. Text Recognition and imaging are in the stable Windows App SDK 1.7.1+, and Phi Silica is in 1.8.0, but Phi Silica needs a Limited Access Feature token and is being **replaced by Aion Instruct**: Insider devices from November 2026, retail from January 2027, after which Phi Silica is removed and no token is needed. The app can also register MCP servers, App Actions and an Agent Launcher with the ODR, and act as an MCP host. All of those are preview, need package identity, and run contained only with MSIX identity. Click to Do can only be launched (`ms-clicktodo://`, no parameters, no data back). Semantic Search is private preview. The agent workspace and MXC isolation are preview behind an admin-only experimental toggle, so Build 1 cannot depend on them.

### Cited Findings
- **Windows AI APIs, hardware and status** (page updated 2026-10-08). On a Copilot+ PC, supported APIs "always run on the NPU".

  | API | NPU (Copilot+) | Notes |
  |---|---|---|
  | Phi Silica | Available | Also on NVIDIA and AMD GPUs |
  | Text Recognition (OCR) | Available | NPU only |
  | Speech Recognition | Experimental | |
  | Image Super Resolution | Available | |
  | Image Description | Available | |
  | Image Segmentation | Available | |
  | Object Erase | Available | |
  | Image Generation | Available | Optional download, removable |
  | Video Super Resolution | Available | Also runs on CPU |

  Release vehicles: Windows App SDK 1.7.1 for "all other APIs"; 1.8.0 for Phi Silica (Limited Access Feature), Conversation Summarization and Object Erase; 1.8 Preview for LoRA for Phi Silica; "Private preview" for **Semantic Search**. — [Microsoft Learn: What are Windows AI APIs?](https://learn.microsoft.com/en-us/windows/ai/apis/)
- **Phi Silica requirements** (page updated 2026-10-02):
  - "The Phi Silica APIs are part of a Limited Access Feature… to request an unlock token, please use the LAF Access Token Request Form."
  - "Phi Silica features are not available in China."
  - On Copilot+ PCs the model is preinstalled on the NPU.
  - On the NPU it uses speculative decoding and prompt compression (neither available on GPU).
  - LoRA adapters must be trained in the cloud with the Fine-Tuning Kit.

  — [Microsoft Learn: Get started with Phi Silica](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica)
- **Phi Silica to Aion Instruct transition**: "Phi Silica is being replaced by Aion Instruct… Unlike Phi Silica, LAF tokens are no longer needed with Aion Instruct."
  - **Early October 2026**: a standalone sideloadable package for testing and LoRA retraining.
  - **November 2026**: rollout to Windows Insider devices, controlled by a Controlled Feature Rollout, with a registry key for side-by-side testing.
  - **January 2027**: rollout to retail devices, and "Phi Silica is removed".

  — [Microsoft Learn: Phi Silica](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica)
- Aion 1.0 Instruct was announced at Build 2026, in preview through Edge Insider channels, with open weights promised on Hugging Face in July. Windows inbox models are "only acquired when an application on the device requests them". — [Windows Developer Blog: Build 2026](https://blogs.windows.com/windowsdeveloper/2026/06/02/build-2026-furthering-windows-as-the-trusted-platform-for-development/)
- **Registering an MCP server with the ODR** (preview): "Applications with package identity can register with Windows by including required metadata in your app package. The OS will automatically register and unregister your server". Identity comes from MSIX or "packaging with external location". Without identity (plain .exe, MSI, or an MCP bundle), servers "can't run in the securely contained agent process and will not be accessible from the Windows on-device agent registry unless users explicitly enable the option to Reduce protections for agent connectors". — [Microsoft Learn: MCP servers on Windows overview](https://learn.microsoft.com/en-us/windows/ai/mcp/servers/mcp-server-overview)
- **Containment requirements** (preview):
  - The server must be a binary (.exe), have package identity, be registered through an MSIX package extension, and provide a `manifest.json` with `_meta` → `com.microsoft.windows` (`static_responses`, `tools/list`).
  - Contained servers run "in a separate Windows session using a separate agent user account". They have no access to user files (unless granted), user settings, registry, credentials, or the user's apps and windows, but they *can* access the internet.
  - File access is granted per host app and then shared by every server that host uses.
  - "Any packaged apps with identity will always run in a contained session in this preview."

  — [Microsoft Learn: Securely containing MCP servers](https://learn.microsoft.com/en-us/windows/ai/mcp/servers/mcp-containment)
- **Acting as an MCP host**: Windows documents a "Quickstart: MCP host on Windows" for apps that list, connect to and call ODR-registered servers. The Microsoft Agent Framework is named as a way to build agents that use the ODR. The agents listed today are the Windows Settings connector and GitHub Copilot agent mode in Visual Studio and VS Code. — [Microsoft Learn: MCP on Windows overview](https://learn.microsoft.com/en-us/windows/ai/mcp/overview)
- **App Actions** (preview): "Apps must have package identity in order to register an app action." Actions are implemented through URI activation or COM (`IActionProvider`) and declared in a JSON definition with typed entities (Document, Photo, Text…). The API is the `Windows.AI.Actions` namespace. — [Microsoft Learn: App Actions on Windows overview](https://learn.microsoft.com/en-us/windows/ai/app-actions/). Users can turn each app's actions on or off at Settings > Apps > Actions. — [ElevenForum](https://www.elevenforum.com/t/enable-or-disable-recommended-actions-from-apps-in-windows-11.34473/)
- **Agent Launchers** (preview): built on App Actions and registered through the ODR. They surface the agent in Start, search and other apps. — [Microsoft Learn: Agent Launchers](https://learn.microsoft.com/en-us/windows/ai/agent-launchers/)
- **Click to Do**: on a Copilot+ PC, an app can open the overlay with `ms-clicktodo://`. "This URI does not accept any additional parameters." Content "is not saved, nor is it automatically passed back to the app used to open the overlay." — [Microsoft Learn: Click to Do](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/click-to-do). App Actions can appear as Click to Do suggestions (see the App Actions developer walkthrough). — [Nick's .NET Travels](https://nicksnettravels.builttoroam.com/?p=3509)
- **Agent workspace and agent accounts**: admin-only, device-wide experimental toggle (Settings > System > AI Components > Experimental agentic features). Known issues on build 26220.7262+ include Windows not sleeping while Copilot conversations are active. — [Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features). Agents "must be signed by a trusted source". The platform controls were to be "available for other developers in preview soon" (as of 2025-11-13). — [Security book](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security)
- **MXC SDK** (early preview on GitHub, 2026-06-02): process isolation (adopted by GitHub Copilot CLI) and session isolation (non-interactive sessions only at first). — [Windows Developer Blog: platform security for AI agents](https://blogs.windows.com/windowsdeveloper/2026/06/02/windows-platform-security-for-ai-agents/)
- **Copilot key remapping** (Insider, 2024): the key can launch an app that is "MSIX packaged and signed". — [Windows Central](https://windowscentral.com/software-apps/windows-11/windows-11-is-finally-letting-you-change-what-the-copilot-key-does). The current shipped rule was not checked.

### Inferences
- **Change the plan's on-device routing target.** `windows-arm64-plan.md` (Token efficiency, item 2) names Phi Silica for intent routing. Phi Silica needs a Limited Access Feature token that a small open-source project may not get, and it disappears from retail devices in January 2027. Build 1 should target **Aion Instruct** (no token) once it reaches the Surface, with the plan's own ONNX Runtime classifier as the default until then. The M0 spike should check whether Aion Instruct keeps the same `Microsoft.Windows.AI.Text.LanguageModel` API (not checked).
- **OCR fits as planned.** The Windows AI Text Recognition API is available on the Snapdragon NPU from Windows App SDK 1.7.1 with no Limited Access Feature token listed. This supports the plan's "on-device text recognition… no tokens spent".
- **Semantic Search is private preview, so don't depend on it.** The plan's "Windows Search index first" approach is the right default.
- **MSIX identity is needed for everything, not just some features.** It gates App Actions, Agent Launchers, contained MCP registration and (per 2024 reporting) the Copilot key. The plan's MSIX plus test-certificate approach is a precondition for nearly every Windows integration.
- **Use the platform's MCP layer without trusting its consent unit.** open-ClaudeOS can be an ODR **host**, gaining File Explorer and Settings connectors and any third-party connectors the user installed, and can register its own artifacts MCP server. Every ODR tool call should still be wrapped as a typed action through open-ClaudeOS's own policy engine and approval card, because Windows grants file access per host and contained servers can reach the internet.
- **Expose open-ClaudeOS through the system's front doors.** Register an Agent Launcher so the Intent Bar can be found from Start, search and possibly the taskbar's Ask Copilot. Register App Actions ("Show with ClaudeOS", "Chart this table") so they appear in Click to Do. Both serve goal 1 of the plan (feel native) cheaply.
- **Agent workspace and MXC are for later.** They suit open-ClaudeOS's later UI-automation and command-execution phases (they line up with "Windows Sandbox or AppContainer instead of bubblewrap"). The admin-only toggle and preview status make them unsuitable for Build 1.

### Gaps
- Not found: whether the MCP host role (consuming ODR servers) needs package identity or any approval, or which user prompt appears when a host first uses a server. The host quickstart was not fetched.
- Not found: whether third-party agents (not Copilot) can create agent accounts or workspaces today, beyond MXC session isolation in early preview.
- Not found: whether Aion Instruct ships on ARM64 Copilot+ NPUs from day one, and its API shape.
- The current (2026) rule for which apps can be assigned to the Copilot key was not confirmed.
- The Windows Agent API for taskbar agents: no Microsoft developer documentation was found, only press mention.

## 3. Gaps an open-source, model-agnostic, consent-first project could fill

### Takeaway
None of the platform agent stacks in the documents reviewed offers **consent bound to the exact actions approved**, an **undo journal for agent effects**, a **full disclosure of outbound bytes**, or **user-owned, file-based generated tools that work across models**. Windows' consent is per agent, per known folder and per MCP host, and is driven by administrators. Apple's actions are limited to whatever each developer's App Intents expose. Googlebook's widgets are tied to Gemini and Google services. Open alternatives (Wandesk, Agent S, Nothing Playground) either have no sandbox or are locked to one vendor's hardware. open-ClaudeOS's Phase 0 design targets exactly this gap.

### Cited Findings
- Windows consent controls are coarse: per agent "Allow Always / Ask every time / Never allow" for six known folders, plus a device-wide admin toggle. — [Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- MCP file access is per host: "Permissions to user files are granted for the host and not per server. When the user grants access to their user files, any MCP server used in that session will have access to the user's files." Contained servers can access the internet. — [Microsoft Learn: MCP containment](https://learn.microsoft.com/en-us/windows/ai/mcp/servers/mcp-containment)
- Microsoft's own principles call for permissions that are "granular, specific and time bound" and tamper-evident logs, and admit that more granular controls are still to come "before it is made generally available". — [Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- At Build 2026, Microsoft's agent security is framed as developer-declared limits and IT policy ("Developers declare what an agent can access", Intune policies, Entra identity), not end-user review of effects. — [Windows Developer Blog: platform security for AI agents](https://blogs.windows.com/windowsdeveloper/2026/06/02/windows-platform-security-for-ai-agents/)
- Copilot Actions uses computer use ("click, type, and scroll like a human") and "might request additional user approval" for sensitive steps, which is model-judged and step-level, not plan-level. Microsoft names cross-prompt injection (XPIA) as a key risk. — [Security book](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security)
- Wandesk (open-source AI desktop) gives its agent an "unsandboxed shell", with every binding "wide open". — [GitHub: Sider-ai/wandesk](https://github.com/Sider-ai/wandesk)
- Nothing's generated widgets support only Location, read-only Calendar and Contacts permissions, and run only on Nothing hardware. — [TechCrunch](https://techcrunch.com/2025/09/30/nothing-launches-ai-tool-for-building-mini-apps-using-prompts/)
- Googlebook's widget creation is built on Gemini plus Gmail and Calendar. — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- On the Mac, Siri is the orchestrator. Apps cannot call each other's intents directly (UNCONFIRMED, see Q1), and Siri AI runs on Apple models built with Gemini, with usage limits. — [Apple Developer Group Lab](https://developer.apple.com/videos/play/wwdc2026/8011/), [9to5Mac release notes](https://9to5mac.com/2026/09/09/macos-27-golden-gate-here-are-apples-full-release-notes/)
- Counterpoint: Apple's Foundation Models framework now accepts "any language model, including… Claude and Gemini". Platform owners are opening up model choice at the developer-framework level. — [Apple Developer WWDC26 guide](https://developer.apple.com/wwdc26/guides/apple-intelligence/)
- Windows' MCP layer is an open standard (MCP). Third-party MCP clients (VS Code, Visual Studio) can already use it, so connectors are not locked to Copilot. — [Microsoft Learn: MCP on Windows](https://learn.microsoft.com/en-us/windows/ai/mcp/overview)

### Inferences
- **Gaps open-ClaudeOS fills well, in order of how hard they are to copy.** Only the last is a cited finding; the rest are inferences, and none of these was found in any platform's documentation.
  1. **Consent bound to exact actions.** Plan digests, invalidated if a byte or recipient changes. No platform describes this; Windows grants are per agent and per folder, and MCP grants per host.
  2. **An approval card built from typed actions, with the full diff and full outbound content.** This is the opposite of Copilot Actions' model-judged "sensitive step" prompts.
  3. **An undo journal and staged commit for agent changes.** No platform document reviewed mentions undo or rollback of agent effects (see Gaps).
  4. **Taint tracking and a dry-run outbox.** These matter because Windows-contained MCP servers can reach the internet.
  5. **User-owned mods as plain folders** (`mod.json`, shareable through Git), with capabilities checked on every call. Googlebook widgets, Nothing apps and M365 App Builder apps all live inside the vendor's ecosystem (M365 apps store data in Microsoft Lists).
  6. **Subject-only windows plus a deterministic layout engine.** No platform offers frameless generated content placed by free-space geometry. Googlebook widgets sit on the desktop, and Click to Do is an overlay.
  7. **Model-agnostic planning, local routing and a Linux path.** This is a differentiator at the OS-assistant level (Copilot, Siri and Gemini are each one vendor's model). It is weaker at the developer-API level, where Apple now accepts Claude and Gemini (cited above).
  8. **An injection test corpus in CI.** This is a public, testable safety property that no vendor publishes for its consumer agent.
- **Make open-ClaudeOS the consent layer on top of Windows' agent plumbing**, not a parallel plumbing. Consume ODR connectors, App Actions and, later, MXC containment, but put the typed-action, preview, grant and undo loop in front of every effect. This turns Microsoft's preview features into supply rather than competition.
- **The project's open-source value lies more in being auditable** (a small trusted core, deterministic policy, local logs) **than in features**, because every feature-level capability here is being built by a platform owner.

### Gaps
- Not checked: whether Copilot Actions keeps any undo or rollback journal, or whether Siri AI cross-app actions offer undo. I found no source either way, so "no platform offers it" is an absence of evidence from the documents reviewed, not a confirmed fact.
- Not checked: whether Googlebook's widgets can be exported, shared or edited as code, or which permissions they declare.
- No usability data was found comparing approval fatigue under per-agent versus per-plan consent.

## 4. Where open-ClaudeOS competes directly with a platform owner, and where Windows could make it redundant

### Takeaway
The riskiest areas are generic **computer-use agents** (Copilot Actions), **on-screen actions** (Click to Do), **natural-language settings changes** (the Mu Settings agent), **file operations by agent** (the File Explorer MCP connector), and the **summon surface** (Copilot key, Ask Copilot on the taskbar). Microsoft already has preview or shipped versions of each, with privileged OS integration. The safer areas are the **consent, undo and audit core**, **subject-only artifact windows with deterministic placement**, **user-owned mods** (Windows has no shell-level natural-language widget builder yet, but Google and Nothing show it is coming), and **model choice**. The ARM64 test build should lean on Windows' plumbing and compete only where Windows is structurally unlikely to follow.

### Cited Findings
- Computer use: Copilot Actions clicks, types and scrolls in apps, inside an OS-level agent workspace with a separate agent account. This is a privileged integration a third-party app layer cannot replicate (preview). — [Security book](https://learn.microsoft.com/en-us/windows/security/book/operating-system-agentic-security), [Microsoft Support](https://support.microsoft.com/en-us/windows/ai/ai-features/experimental-agentic-features)
- On-screen actions: Click to Do is built into Copilot+ PCs. Third-party apps can only launch it, with no data returned. — [Microsoft Learn: Click to Do](https://learn.microsoft.com/en-us/windows/apps/develop/windows-integration/click-to-do)
- Settings by natural language: the Mu-based Settings agent on the NPU (announced and previewed 2025). — [Neowin](https://www.neowin.net/news/microsoft-reveals-mu-an-on-device-small-language-model-built-into-windows-11/)
- File operations: Windows ships a File Explorer MCP connector "that provides a set of tools to access and modify files", and puts MCP tools into File Explorer context menus. — [Microsoft Learn: MCP on Windows](https://learn.microsoft.com/en-us/windows/ai/mcp/overview)
- Summon surface: taskbar agents in Release Preview (April 2026) and Ask Copilot expected mid-2026, off by default. — [Windows Latest](https://www.windowslatest.com/2026/04/18/microsoft-confirms-ai-agents-are-still-coming-to-the-windows-11-taskbar-as-it-prepares-for-public-rollout/). The Copilot key can be reassigned only to MSIX-packaged, signed apps (2024 Insider rule). — [Windows Central](https://windowscentral.com/software-apps/windows-11/windows-11-is-finally-letting-you-change-what-the-copilot-key-does)
- Natural-language app and automation building at Microsoft is in Microsoft 365 (App Builder, Workflows, Copilot Cowork app-building preview from 2026-09-08), not the Windows shell. — [Redmondmag](https://redmondmag.com/articles/2025/10/31/microsoft-365-copilot-gains-app-builder.aspx), [MC1469329](https://mc.merill.net/message/MC1469329)
- Natural-language widget creation is now a platform feature elsewhere: Googlebook "Create your Widget" (announced 2026-05-12), Nothing Playground (beta 2025-09-30), Apple "Describe a Shortcut" (shipped 2026-09). — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/), [TechCrunch](https://techcrunch.com/2025/09/30/nothing-launches-ai-tool-for-building-mini-apps-using-prompts/), [9to5Mac](https://9to5mac.com/2026/09/09/macos-27-golden-gate-here-are-apples-full-release-notes/)
- On-device models are platform-controlled and change: Phi Silica (Limited Access Feature) will be removed in January 2027 in favour of Aion Instruct. — [Microsoft Learn: Phi Silica](https://learn.microsoft.com/en-us/windows/ai/apis/phi-silica)
- Microsoft is courting open-source agent runtimes rather than excluding them (OpenClaw, NVIDIA OpenShell and Hermes Agent on MXC). — [Windows Developer Blog: Build 2026](https://blogs.windows.com/windowsdeveloper/2026/06/02/build-2026-furthering-windows-as-the-trusted-platform-for-development/)
- The project's own plan accepts that on Windows it is "an app layer within Microsoft's rules, and it cannot replace the Windows shell". — `docs/windows-arm64-plan.md` (How this changes the wider plan)

### Inferences
- **High redundancy risk; don't lead with these on Windows:**
  - A general "do it in any app for me" computer-use agent: Copilot Actions with an OS-level agent workspace.
  - "Change this setting": the Settings agent, plus the Settings MCP connector.
  - "Act on what's on screen": Click to Do.
  - Basic file organise, rename and move by agent: the File Explorer MCP connector.

  open-ClaudeOS should *use* these where they exist (call the Settings and File Explorer connectors through its own consent card), not rebuild them.
- **Direct competition with a platform owner, worth taking on deliberately:**
  - The **Intent Bar as the main summon surface**. It competes with Copilot and Ask Copilot for the Copilot key and taskbar. The ODR Agent Launcher route makes open-ClaudeOS a peer in Microsoft's discovery surfaces rather than a hijacker of them.
  - **Natural-language widgets and mods**. These compete with what Google and Nothing ship and what Microsoft has not yet put in the shell. The window is open on Windows today, but expect Microsoft to follow, given Copilot Cowork app-building and M365 App Builder. Keeping mods as user-owned files, declarative-first and portable to Linux is what makes them hard to absorb.
- **Low redundancy risk; the project's core:**
  - Deterministic, plan-bound consent with full diffs and outbound disclosure.
  - The undo journal and staged commits.
  - Subject-only windows placed by a free-space layout engine.
  - Token-cheap "Claude writes the recipe, local code runs it over the data".
  - Bring-your-own-model with a portable .NET core.

  None of these appears in Microsoft's, Apple's or Google's documented plans. Microsoft's commercial incentives (Copilot as the default agent, M365 as the app-builder, enterprise IT as the consent authority) point away from them.
- **Strategic hedge:** platform-owned on-device models (Phi Silica to Aion) and preview APIs (ODR, App Actions, agent workspace) can change or vanish on Microsoft's timeline. Keeping every Windows AI dependency behind an interface with a local fallback (ONNX classifier, local grammar) protects Build 1. The Linux path remains the place where no platform owner can make the project redundant.

### Gaps
- No public Microsoft roadmap item for natural-language widget or mini-app creation in the Windows shell was found. Absence here does not rule it out.
- Whether Microsoft will allow third-party agents to replace Copilot as the default handler for taskbar or Ask Copilot (beyond being listed) is not documented.
- No Microsoft policy statement was found on whether app layers that place or move other apps' windows (the open-ClaudeOS layout engine) will be restricted for agents. The plan already notes that some apps resist being moved.
