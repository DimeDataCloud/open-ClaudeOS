# Natural-language creation on Googlebook OS (Create My Widget, Antigravity, and related Google products)

Research date: 2026-10-09. All dates below are publication dates of the cited source, unless the text says otherwise. Status labels: **SHIPPED** (available to users), **ANNOUNCED/DEMOED** (shown by Google but not confirmed available), **RUMOURED/UNVERIFIED** (third-party claim, no Google confirmation).

Headline finding: the user's description is partly accurate. Googlebook's built-in natural-language creation feature is **Create My Widget** (Google's May 2026 blog called it "Create your Widget"). It makes **desktop and home-screen widgets** only. It does not make full apps, mini-apps or automations. Full app creation from natural language on Googlebook goes through a separate developer tool, **Google Antigravity** (an agentic coding app shipped with every Googlebook), plus web tools such as AI Studio Build mode. I found **no evidence** of a Googlebook feature that lets ordinary users make "native apps" or "tools for Gemini to use" by prompting.

---

## 1. What is the feature called, and what can a user create with it?

### Takeaway
The feature is **Create My Widget** (first announced on 2026-05-12, when Google's Googlebook post called it "Create your Widget"). It makes **widgets only**: live, resizable dashboard cards built from a prompt or from curated templates, filled with data from the web and Google apps such as Gmail and Calendar. It ships on Googlebooks, which went on sale in the US on 2026-10-04. Separately, Googlebook includes **Google Antigravity**, an agentic coding tool for writing full apps and deploying them on the laptop. That is a developer tool, not a consumer "make me an app" feature.

### Cited Findings

**Naming and announcement**
- 2026-05-12: Google's Googlebook launch post says "We're also bringing Create your Widget to Googlebooks, which lets you create custom widgets just by prompting." Gemini "can search the internet or connect to Google apps such as Gmail and Calendar," and the widgets combine "into one personalized dashboard" on the desktop. — [Google blog, Introducing Googlebook](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- 2026-05-12: the same feature was announced for Android phones as **Create My Widget**, part of "Gemini Intelligence" at The Android Show. TechCrunch says Google calls it a way to "vibe-code" custom widgets. — [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
- 2026-09-21: Google's Googlebook intelligence post uses the name "Create My Widget" and says it needs "no coding experience." Its examples are a sports-score tracker and a visual countdown to a family trip. — [Google blog, Googlebook built-in intelligence](https://blog.google/products-and-platforms/devices/googlebook/googlebook-built-in-intelligence/)
- The Googlebook Help Center page "Create and edit custom AI widgets on your Googlebook" calls the creation UI the "Create My Widget studio." — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)

**What can be created: widgets only**
- Google's demo examples (2026-05-12):
  - A family-reunion dashboard for a Berlin trip, combining flight and hotel details, restaurant reservations and a countdown. — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/); [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
  - A meal-prep dashboard from the prompt "suggest three high-protein meal prep recipes every week." — [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
  - A cyclist's weather widget showing only wind speed and rain. — [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
  - "Countdown to my first marathon." — [TechRadar via Yahoo, 2026-05-12](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html)
- Help Center examples: a football match schedule, a holiday countdown and flight status. — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)
- Template categories in the Googlebook build of the app (2026-09-24): Weather, Sports & fitness, Events & travel, Daily habits, Important dates, Finance, and Tools & tips, each with several templates. — [9to5Google](https://9to5google.com/2026/09/24/google-create-my-widget-app/)
- Template categories in the phone build: Combo, Weather alerts, My world clock, Daily Brief, Important dates, and Markets. — [9to5Google](https://9to5google.com/2026/09/24/google-create-my-widget-app/)
- Hands-on (2026-09-21, pre-launch press event): a reviewer asked for a London Underground status widget. The device had "no problem finding the information" and put it into a custom widget. — [The Disconnekt, Chris Hall](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/)
- Google's Ben Greenwood (director, PM, Android Core Experiences): "This is like you asking your personal assistant a question, and having them just bring you the answer on repeat." — [TechCrunch, 2026-05-12](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)

**Full apps: Antigravity, a separate developer tool**
- 2026-09-21: Google says "Google Antigravity ships with every Googlebook" and that users can write apps and deploy them on the device. It also says developers get a full Linux terminal for tools such as Claude Code or the Antigravity CLI. — [Google blog, Googlebook built-in intelligence](https://blog.google/products-and-platforms/devices/googlebook/googlebook-built-in-intelligence/)
- 2026-09-30: an Antigravity Android app (package `com.google.android.apps.antigravity`, version 2026.09.23.988012583) appeared on the Play Store. Google calls Antigravity an agent-first development platform with a "mission control" for agents. It "works with the Linux environment" on Googlebooks, and Google says you can "Create and deploy apps right on your Googlebook." The app currently runs only on Googlebook OS. — [9to5Google](https://9to5google.com/2026/09/30/google-antigravity-play-store/)
- 9to5Google's hands-on said: "Google built an Antigravity app with native UI for Android … distributed via the Play Store and interacts with the Linux terminal." — [9to5Google Googlebook OS hands-on, 2026-09-21](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)

**Automations: Gemini Spark (adjacent, not a creation tool)**
- Google says Googlebook includes Gemini "task automations," Gemini Live and proactive suggestions. It also says you can "close your laptop while Gemini Spark works in the background." — [Google blog, 2026-09-21](https://blog.google/products-and-platforms/devices/googlebook/googlebook-built-in-intelligence/)
- TechCrunch says Gemini Spark and Gemini Live "also don't require special, new hardware." — [TechCrunch, 2026-09-21](https://techcrunch.com/2026/09/21/googles-899-googlebook-is-a-bet-that-youll-buy-a-new-laptop-for-gemini/)

**Status timeline**
| Date | Event | Status |
|---|---|---|
| 2026-05-12 | Create My Widget / "Create your Widget" announced for Android phones and Googlebook | ANNOUNCED — [Google blog](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/), [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/) |
| 2026-09-21 | Googlebook preorders open at $899; press hands-ons demo widget creation | DEMOED — [TechCrunch](https://techcrunch.com/2026/09/21/googles-899-googlebook-is-a-bet-that-youll-buy-a-new-laptop-for-gemini/), [The Disconnekt](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/) |
| 2026-09-24 | Create My Widget app listed on the Play Store (`com.google.android.apps.yourwidget`, version `1.27.983746769.0-desktop_release`) | LISTED — [9to5Google](https://9to5google.com/2026/09/24/google-create-my-widget-app/) |
| 2026-09-28 | Basic Tutorials says the app is available on Pixel and current Galaxy phones but not on the Galaxy Z Fold 8 | Phone availability contested — [Basic Tutorials](https://basic-tutorials.com/news/create-my-widget-googles-ai-app-for-custom-android-widgets-is-available-on-the-play-store/); Android Headlines said it was not sure the app worked for anyone yet ([Android Headlines](https://www.androidheadlines.com/2026/09/googles-create-my-widget-app-has-finally-appeared-on-the-play-store.html), search-result summary only, not fetched) |
| 2026-10-04 | Googlebooks ship in the US; the Help Center documents the feature (English only; US, UK, Canada, Australia, India) | SHIPPED on Googlebook — [TechCrunch](https://techcrunch.com/2026/09/21/googles-899-googlebook-is-a-bet-that-youll-buy-a-new-laptop-for-gemini/), [Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302) |
| 2026-05-12 (promise) | Gemini Intelligence features, including Create My Widget, to reach watch, car, glasses and laptops "later this year" | ANNOUNCED; only laptops (Googlebook) confirmed so far — [TechRadar via Yahoo](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html) |

### Inferences
- "Create native widgets, apps and tools from natural language" overstates the built-in feature. The consumer feature makes **widgets only**. "Apps" means using Antigravity (agentic coding, aimed at developers, runs through the Linux environment) or web tools like AI Studio Build. "Tools for Gemini to use" have no consumer equivalent. The developer route is Android AppFunctions (see section 3).
- The "desktop_release" version string and the Googlebook-only categories (Daily habits, Tools & tips) suggest Google built the Play Store app first for the Googlebook launch.

### Gaps
- No post-launch (after 2026-10-04) long-term review with systematic widget-creation testing was found. The Verge's review could not be fetched (domain blocked for this tool).
- Not confirmed whether Antigravity is preinstalled on Googlebook or only offered through the Play Store. Google's blog says it "ships with every Googlebook."

---

## 2. How does it work technically?

### Takeaway
Google calls Create My Widget "the first step in generative UI" and has **not published the technical mechanism**. No Google source says whether Gemini writes code (Kotlin/Compose, web or anything else), fills in templates, or emits a declarative UI spec for a system renderer. What is documented:
- a guided studio with curated templates plus a free-text prompt;
- a preview carousel of alternative layouts;
- editing by further prompts, with the widget regenerated;
- compact and expanded sizes;
- live data that refreshes "on repeat";
- Material 3 Expressive styling.

Sources conflict on where it runs. 9to5Google says Gemini Nano powers it through a "hybrid intelligence architecture." Another hands-on was told there is no local model.

### Cited Findings

**Framing**
- 2026-05-12: Google described Create My Widget as "taking the first step in generative UI" with widgets. — [TechRadar via Yahoo](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html)
- 2026-09-21: 9to5Google's Abner Li wrote: "This is the introduction of generative UI to Android, and I think the ramifications for the future are tremendous." — [9to5Google hands-on](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)
- TechCrunch uses Google's "vibe-code" framing but says nothing about the mechanism. — [TechCrunch, 2026-05-12](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)

**Creation flow (Help Center, current as of 2026-10)**
- Open the studio: right-click an empty area of the home screen → "Browse widgets" → "Create."
- Pick a curated suggestion and select "Try it" (the preview appears "within seconds"), or select "Describe your widget" and type a description.
- Swipe through the preview carousel to see "alternative styles and layouts."
- Give a thumbs up or down, with tags such as Useful, Accurate, Creative, Readable and Fun.
- Source: [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)

**Editing with further prompts: yes**
- In the studio, select Edit, type into "Describe a change," and the widget is "regenerated with the same core information."
- From the desktop, right-click the widget → Settings → "Describe a change." Google's example turns a single-team sports card into a two-team dashboard with new schedule data.
- Source: [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)

**Sizes and persistence**
- Compact size shows only the next event. Expanded size shows more lists, timelines and details.
- Removing a widget shows an "Item removed" toast. Undo within five seconds restores "the widget and its configuration."
- Source: [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)

**Visual system**
- Generated widgets can use Material 3 Expressive shapes for simpler, low-information designs. — [9to5Google, 2026-09-24](https://9to5google.com/2026/09/24/google-create-my-widget-app/)
- Users can adjust size, content and appearance before saving. — [Basic Tutorials, 2026-09-28](https://basic-tutorials.com/news/create-my-widget-googles-ai-app-for-custom-android-widgets-is-available-on-the-play-store/)

**Live data and refresh**
- Greenwood said the widget brings "you the answer on repeat," which implies a recurring data refresh. — [TechCrunch](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
- 9to5Google says widgets "pull from live data sources, like Search." — [9to5Google hands-on](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)

**Delivery vehicle**
- The feature ships as a standalone Play Store app, package `com.google.android.apps.yourwidget`. — [9to5Google, 2026-09-24](https://9to5google.com/2026/09/24/google-create-my-widget-app/)

**On-device or cloud: conflicting reports**
- 9to5Google, 2026-09-21: "The on-device AI model is Gemini Nano, and that's used to power AI descriptions of your screenshots that unlock search, as well as Magic Pointer, Rambler, and Create My Widget via a hybrid intelligence architecture." — [9to5Google hands-on](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)
- Google says the Googlebook intelligence system "brings Gemini directly on your device," but does not say which features run locally. — [Google blog, 2026-09-21](https://blog.google/products-and-platforms/devices/googlebook/googlebook-built-in-intelligence/)
- Contradicting this, The Disconnekt (2026-09-21) reports that Gemini on Googlebook needs an internet connection. Apart from a couple of prompts at setup, "There isn't a local model running this." The reviewer was also told regular Gemini terms apply. — [The Disconnekt](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/)
- TechRadar (2026-05-12) says the feature pulls personal data "privately" but does not explain how. — [TechRadar via Yahoo](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html)

**Candidate rendering technologies (not confirmed for Create My Widget)**
- **A2UI renderer.** At I/O 2026, Google announced an upcoming "A2UI Jetpack Compose Renderer." The A2UI library lets agents "speak UI," and the renderer turns those messages into native UI components. — [Android Developers Blog, I/O '26 AI recap](https://android-developers.googleblog.com/2026/05/android-ai-intelligence-system.html)
- **Hybrid on-device/cloud routing.** Firebase AI Logic Hybrid Inference routes requests between on-device and cloud models with modes PREFER_ON_DEVICE, PREFER_CLOUD, ONLY_ON_DEVICE and ONLY_CLOUD. — [Android Developers Blog, I/O '26](https://android-developers.googleblog.com/2026/05/android-ai-intelligence-system.html)
- **RemoteCompose.** This is an AndroidX library for server-driven UI. Compose drawing operations are captured into a serialized document and replayed by a lightweight on-device player. — [Nativeblocks blog](https://nativeblocks.io/blog/remote-compose-android-server-driven-ui/); [Dove Letter](https://doveletter.dev/articles/remote-compose)
  - **No source links RemoteCompose to Create My Widget.** A search-engine summary implied the link, but the underlying pages do not support it.

**Unverified third-party claim**
- A sponsored, low-reliability blog says Gemini processes the prompt on-device. It also says widgets keep updating in the background and draw on Gmail, Calendar and Messages. — [buildmvpfast.com](https://www.buildmvpfast.com/blog/google-create-my-widget-generative-ui-android-gemini-2026); treat as unverified.

### Inferences
- **Probably not arbitrary code.** The feature is a first-party Google app (`yourwidget`) that hosts the widgets, offers fixed template categories and a carousel of "alternative styles and layouts," and regenerates with "the same core information." That pattern fits generating a structured layout plus a data-binding spec, rendered natively by the app's own widget renderer (for example a declarative format such as A2UI, or RemoteCompose). It does not fit generating arbitrary Kotlin or web code. This is an inference; Google has not confirmed the format.
- **Likely sandboxed by Android's app model.** The widgets appear to be ordinary Android app widgets owned by the Create My Widget app. If so, they are sandboxed by the Android app model, not by a special per-widget sandbox. Not documented.
- **Likely a split between device and cloud.** "Hybrid intelligence architecture" most plausibly means Gemini Nano handles some steps (such as intent parsing or layout) while cloud Gemini and Google Search handle live data retrieval. That would reconcile the two conflicting hands-on reports, but this is not confirmed.

### Gaps
- No Google documentation found on the generated artifact's format (code vs. declarative UI vs. template filling), the renderer, or the model used (Nano vs. cloud Gemini version).
- No documentation on refresh frequency, background data fetching, or battery cost.
- No documentation on whether widgets sync across devices or a Google account (for example phone to Googlebook).

---

## 3. What can generated widgets and apps access, and how does the user approve it?

### Takeaway
Create My Widget draws on **Google Search/the web and the user's Google apps (Gmail, Calendar; possibly messaging)**. It **cannot use third-party app data**. No Google source describes its permission or consent flow. Android's mechanism for exposing other apps' functions to Gemini is **AppFunctions** (an on-device MCP-style API), but it is in experimental/early-access preview, and nothing links it to Create My Widget.

### Cited Findings

**Data sources**
- Google says Gemini "can search the internet or connect to Google apps such as Gmail and Calendar." — [Google blog, 2026-05-12](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/)
- TechCrunch reports the same sources: the web plus Google apps like Gmail and Calendar. — [TechCrunch, 2026-05-12](https://techcrunch.com/2026/05/12/googles-create-my-widget-feature-will-let-you-vibe-code-your-own-widgets/)
- TechRadar says the feature "pulls data from your calendar, inbox, and messaging apps" privately. — [TechRadar via Yahoo, 2026-05-12](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html)
- Basic Tutorials lists the data sources as web content plus linked Gmail and Calendar. — [Basic Tutorials, 2026-09-28](https://basic-tutorials.com/news/create-my-widget-googles-ai-app-for-custom-android-widgets-is-available-on-the-play-store/)

**Third-party apps**
- Android Authority says it "can't directly work with third-party app data — so you can't make your own custom Tinder widgets, for example." — [Android Authority](https://www.androidauthority.com/create-my-widget-3665312/). Quoted from search-result text; the page returned HTTP 403 when fetched.
- Android Police (2026-06-08) calls third-party support uncertain. It says AppFunctions could let developers connect their apps, but adoption is unknown. — [Android Police](https://www.androidpolice.com/android-widget-future/)

**AppFunctions (developer mechanism)**
- At I/O 2026 (May), Google said "AppFunctions allows your application to act as an on-device Model Context Protocol (MCP) server" to share an app's "tools, services and data to the system and agents." Status: "currently available in experimental preview," with an early access program for production use. Google also offers a skill that generates AppFunctions and a test agent for debugging them. — [Android Developers Blog, I/O '26](https://android-developers.googleblog.com/2026/05/android-ai-intelligence-system.html)
- The Android 17 release post (June 2026) says AppFunctions let agents like Gemini "discover and execute AppFunctions to perform workflows on behalf of the user." It says the Gemini integration is in private preview with trusted testers, and that Samsung Gallery on the Galaxy S26 is the showcase. — [Android Developers Blog, Android 17](https://android-developers.googleblog.com/2026/06/Android-17.html). Taken from a search-result excerpt of the official page; I did not fetch the page in full.
- ADK for Android (Agent Development Kit) is available "for experimentation," supporting multi-agent workflows across on-device and cloud models. — [Android Developers Blog, I/O '26](https://android-developers.googleblog.com/2026/05/android-ai-intelligence-system.html)

**Permissions and approval**
- The Googlebook Help Center page on creating widgets says nothing about permissions, connected-app consent or privacy handling. — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)
- The Disconnekt was told "regular Gemini terms apply," meaning user data may be used for model training. — [The Disconnekt, 2026-09-21](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/)

**Antigravity's access (developer app creation)**
- Antigravity works through the Googlebook Linux environment and terminal. — [9to5Google, 2026-09-30](https://9to5google.com/2026/09/30/google-antigravity-play-store/)
- Its "Build with Google" bundles include an Android CLI bundle (create projects, test apps with natural-language instructions, deploy to an Android virtual device). They also include a Firebase bundle and MCP servers such as the Dart MCP server and Google Cloud data MCP servers. — [Antigravity docs, Build with Google](https://antigravity.google/docs/build-with-google/) (undated)

### Inferences
- Create My Widget most likely uses Gemini's existing Google-app connections (Gmail and Calendar, the same connections used in the Gemini app), so consent probably follows the Gemini app's connected-apps settings. This is not documented for Create My Widget.
- AppFunctions is the most likely future route for third-party data in generated widgets. As of 2026-10-09 that is speculation (Android Police), not an announced plan.

### Gaps
- No source describes per-widget permission prompts, how a user revokes a widget's access to Gmail or Calendar, or whether widget data stays on-device.
- No source confirms that AppFunctions are enabled for Gemini on Googlebook OS specifically.

---

## 4. Can creations be shared, published or exported, and are there developer APIs?

### Takeaway
I found **no evidence** that Create My Widget creations can be shared, exported or published to other users, the Play Store or the web. There is also **no public developer API or SDK** for extending Create My Widget. Developer extension points on the platform are general Android ones: AppFunctions (on-device MCP), ADK for Android, and an upcoming A2UI Compose renderer. Apps built with Antigravity or AI Studio can be deployed and published through normal developer channels.

### Cited Findings

**Create My Widget**
- The Help Center covers creating, editing, resizing and removing widgets. It mentions no sharing, export or sync. — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)
- Google's two Googlebook posts mention no sharing of widgets and no developer APIs for the feature. — [Google blog, 2026-05-12](https://blog.google/products-and-platforms/platforms/android/meet-googlebook/); [Google blog, 2026-09-21](https://blog.google/products-and-platforms/devices/googlebook/googlebook-built-in-intelligence/)

**Developer surfaces announced at I/O 2026**
- AppFunctions (on-device MCP server, experimental preview), ADK for Android (experimental), the upcoming A2UI Jetpack Compose Renderer, the ML Kit GenAI Prompt API, and the Gemini Nano 4 preview. — [Android Developers Blog, I/O '26](https://android-developers.googleblog.com/2026/05/android-ai-intelligence-system.html)

**Full-app creation and publishing paths**
- **Antigravity on Googlebook:** "Create and deploy apps right on your Googlebook." Users can also install Claude Code and OpenAI's Codex in the Linux environment. — [9to5Google, 2026-09-30](https://9to5google.com/2026/09/30/google-antigravity-play-store/)
- **AI Studio Build mode (web)**, page last updated 2026-08-20:
  - It "generates a Kotlin and Jetpack Compose project that you can preview in a browser-based emulator."
  - You can "install it on a physical device using ADB in the browser."
  - You can "publish to the Play Store for internal testing."
  - Web apps use React plus a Node.js runtime, with Cloud Run deployment and automatic Firebase/Firestore setup.
  - The page says the Antigravity agent harness "is powering the Build mode experience."
  - Source: [Google AI for Developers, Build apps in AI Studio](https://ai.google.dev/gemini-api/docs/aistudio-build-mode)

### Inferences
- **Two creation tiers.** A consumer tier (Create My Widget: personal, unshareable, locked into Google's renderer) and a developer tier (Antigravity and AI Studio: real Kotlin/Compose or web code that can be published). The developer tier is where "tools for Gemini" can be made, by implementing AppFunctions in an app.
- **Third-party MCP support only through developer tools.** Antigravity supports MCP servers in its developer workflow, and AppFunctions is described as on-device MCP. Nothing shows ordinary users plugging MCP servers into Gemini on Googlebook.

### Gaps
- No information on whether widgets carry over to a new Googlebook or phone through account backup.
- Not confirmed whether apps made in Antigravity on a Googlebook can be packaged as APKs and installed locally, or only run in the Linux container. Google's wording ("deploy apps right on your Googlebook") is ambiguous.

---

## 5. How does it relate to Google's earlier natural-language creation products, and what does it reuse?

### Takeaway
Create My Widget is Google's first **OS-level, persistent** generative UI surface. Earlier products were ephemeral or lived inside a web app: Gemini's dynamic view generates an interactive UI per chat response, and Opal makes mini-apps inside Gemini web. Google has **not stated** that Create My Widget reuses dynamic view, Opal or Canvas technology. The clearest documented technology sharing is on the developer side: Antigravity's agent harness powers AI Studio Build mode, and Antigravity is the app-building tool on Googlebook.

### Cited Findings

**Gemini generative UI / dynamic view (Nov 2025)**
- Google Research says its generative UI research reaches users as the "dynamic view" experiment in the Gemini app and in AI Mode in Search.
- Dynamic view uses Gemini's agentic coding capabilities to design and code a fully customized interactive response for each prompt.
- Human raters strongly preferred these interfaces over standard outputs when generation speed was ignored.
- Source: [Google Research, Generative UI](https://research.google/blog/generative-ui-a-rich-custom-visual-interactive-user-experience-for-any-prompt/); background in [9to5Google, 2025-11-25](https://9to5google.com/2025/11/25/gemini-generative-uis-apps/)

**Opal (Google Labs mini-apps)**
- In December 2025 Google added Opal to the Gemini web app, reachable from the Gems manager.
- Users describe a mini-app in plain language, and a "text-to-steps" view shows an editable list of the steps Opal inferred.
- The finished mini-app becomes a reusable Gem. An Advanced Editor at opal.google gives finer control.
- Source: [eWeek](https://www.eweek.com/news/google-opal-vibe-coding-gemini/); [Chrome Unboxed](https://chromeunboxed.com/google-brings-vibe-coding-tool-opal-to-gemini-and-keeps-removing-barriers-to-building-your-next-app/)

**AI Studio Build and Antigravity**
- AI Studio Build mode is powered by "the core components of the agent harness" of the Antigravity agent. It generates Kotlin/Compose Android apps and React/Node web apps. — [Google AI for Developers](https://ai.google.dev/gemini-api/docs/aistudio-build-mode)
- Antigravity 2.0 was released at I/O 2026 (May). Its Android app for Googlebook appeared on the Play Store on 2026-09-30. — [9to5Google](https://9to5google.com/2026/09/30/google-antigravity-play-store/)

**Comparison with existing widget tools**
- Reviewers compare Create My Widget with KWGT, Widgetopia and Tasker's widget editor. Those rely on predefined data sources (weather, battery, stocks) unless users write code. — [TechRadar via Yahoo](https://tech.yahoo.com/articles/taking-first-step-generative-ui-190000004.html); [Android Police](https://www.androidpolice.com/android-widget-future/)
- Android Police also compares it with Google's own At a Glance. — [Android Police](https://www.androidpolice.com/android-widget-future/)
- A 9to5Google reader called the app "Pretty limited" and said they had built their own widget in AI Studio instead (reader comment, not a review). — [9to5Google, 2026-09-24](https://9to5google.com/2026/09/24/google-create-my-widget-app/)

### Inferences

**How Create My Widget compares with each product**

| Product | Where it runs | What it makes | Persistence | Main difference from Create My Widget |
|---|---|---|---|---|
| Create My Widget (2026) | OS home screen/desktop | Persistent widgets with live data | Kept on the desktop, refreshed "on repeat" | — |
| Gemini dynamic view (Nov 2025) | Gemini chat | Coded interactive UI for one answer | Ephemeral, in chat | Same "generative UI" idea, but one-off |
| Opal (Dec 2025 in Gemini) | Gemini web | Multi-step AI workflow mini-apps | Saved as Gems | Workflow logic, not a glanceable OS surface |
| AI Studio Build / Antigravity | Web IDE / Googlebook Linux | Full codebases (Kotlin/Compose, React/Node) | Code projects | Developer-oriented |

- Google's own "first step in generative UI" framing positions Create My Widget as the start of an OS-level generative UI roadmap, not as a port of dynamic view. The two may share Gemini models; shared rendering code is not documented.

### Gaps
- No source found linking Create My Widget or Googlebook to **Gemini Canvas, Gems, Firebase Studio or Jules**. Nothing was found on whether these run natively on Googlebook OS beyond the web browser.
- No Google statement on whether Create My Widget uses dynamic view's coding approach (HTML/JS) or a different structured-UI approach.
- Firebase Studio's 2026 status relative to AI Studio Build mode was not established. Searches did not surface it.

---

## 6. What limits, failure modes or quality issues have reviewers reported?

### Takeaway
As of 2026-10-09, there are **no in-depth post-launch reviews** of Create My Widget with measured latency, accuracy or battery impact. Reported limits:
- English only, in five countries;
- no third-party app data;
- phone rollout restricted to Gemini Intelligence-capable Pixel and Galaxy devices, with Fold 8 incompatibility reported;
- reviewers consider it a minor feature, and one reader called it "cookie-cutter";
- Google's own help page tells users to check the generated data for accuracy;
- conflicting reports on whether it needs the cloud.

### Cited Findings

**Availability limits**
- English only, in the United States, United Kingdom, Canada, Australia and India. — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)
- On phones, the app was reportedly incompatible with the Galaxy Z Fold 8 as of 2026-09-28. The rollout is staged and limited to Pixel and Galaxy devices. — [Basic Tutorials](https://basic-tutorials.com/news/create-my-widget-googles-ai-app-for-custom-android-widgets-is-available-on-the-play-store/)
- Android Police (2026-06-08) notes that Gemini Intelligence has a high hardware barrier, so Create My Widget "won't be universal at first." — [Android Police](https://www.androidpolice.com/android-widget-future/)
- A specific "12GB+ RAM / flagship chipset / Gemini Nano v3" requirement appears only in sponsored or third-party posts; unverified. — [buildmvpfast.com](https://www.buildmvpfast.com/blog/google-create-my-widget-generative-ui-android-gemini-2026)

**Accuracy and hallucination risk**
- The Help Center tells users to check that details such as schedule dates and countdown numbers are accurate before adding a widget. — [Googlebook Help Center](https://support.google.com/googlebook/answer/18399586?hl=en-GB&ref_topic=16901302)
- Android Police raised accuracy as a concern, since wrong travel or calendar data could affect a user's day. — [Android Police, 2026-06-08](https://www.androidpolice.com/android-widget-future/)

**Reviewer assessments**
- TechCrunch: "Vibe-coding widgets are a minor addition, as well." The Googlebook's AI features are not "enough reason to buy a new device." — [TechCrunch, 2026-09-21](https://techcrunch.com/2026/09/21/googles-899-googlebook-is-a-bet-that-youll-buy-a-new-laptop-for-gemini/)
- 9to5Google: "Googlebooks will definitely have to guide people on how to use this capability." The reviewer also says daily-workflow testing is still pending. — [9to5Google hands-on, 2026-09-21](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)
- A 9to5Google reader comment called the app "Pretty limited" and "cookie-cutter" (opinion, not a review). — [9to5Google, 2026-09-24](https://9to5google.com/2026/09/24/google-create-my-widget-app/)
- One positive anecdote: a London Underground status widget worked. — [The Disconnekt, 2026-09-21](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/)

**Cloud dependence, privacy and cost**
- The Disconnekt says Gemini on Googlebook needs an internet connection, has no local model (contradicting 9to5Google's "Gemini Nano … hybrid" statement), and is covered by regular Gemini terms. Googlebooks include 12 months of Google AI Pro, after which it must be paid for. — [The Disconnekt](https://thedisconnekt.com/i-tested-the-new-googlebook-and-gemini-integration-blew-me-away/); [9to5Google](https://9to5google.com/2026/09/21/googlebook-os-hands-on/)

**General launch impressions (not specific to widgets)**
- Chrome Unboxed (2026-10-05): the new OS has a steep learning curve and "local AI integration … feels equal parts exhilarating and slightly overwhelming." It deferred judgment on Gemini Intelligence features. — [Chrome Unboxed](https://chromeunboxed.com/the-googlebook-era-is-here-some-first-impressions-of-the-hardware-and-a-whole-new-os/)
- Chrome Unboxed (2026-10-06): desktop widgets in general (Calendar, Clock, Gemini prompt box, Weather) are "responsive." AI-generated widgets are not reviewed specifically. — [Chrome Unboxed](https://chromeunboxed.com/real-desktop-widgets-and-shortcuts-on-the-googlebook-desktop-are-refreshing/)
- The Verge's first impressions were reportedly titled "exciting but underbaked" and called Googlebooks "a little janky" at times. — [The Verge via Google News](https://news.google.com/read/CBMirAFBVV95cUxQR0Zrc0NIZXhsLWhLX1pwZENoRFoybkRaRFRDMlhCUVlnbWRPekRZV2lPMDRkOTBidXEtR2dZQWttWmtCcWZZQTZMUEotRDFUamItN1dfRnpYVExtR1RjUWpXOXZSYmVza0cyOEw4M1ZzMUhHbFJMcTlvUDkwVVRsa1Y2ODdYRWR1eDVqU2ZZaFVBWDdUZkFFVmlFRWFnRnh4WC1RYW45X3ZqcUZ3?hl=en-US&gl=US&ceid=US%3Aen). Taken from search-result text only; The Verge could not be fetched.

**Antigravity's limits**
- The Antigravity app runs only on Googlebook OS. It is unclear whether it will keep feature parity with the desktop versions, which get major updates weekly. — [9to5Google, 2026-09-30](https://9to5google.com/2026/09/30/google-antigravity-play-store/)

### Inferences
- Reviews say little about failures because most coverage dates from the 2026-09-21 press event or launch week (2026-10-04 to 10-06). There has not yet been time for long-term widget testing.
- Google's instruction to verify dates and countdowns implies widgets can show wrong data, a hallucination risk that Google acknowledges.

### Gaps
- No measured generation latency. The Help Center says previews appear "within seconds" for templates; free-text prompts were not timed.
- No reports on battery impact of background widget refreshes, broken or blank widgets, or stale data.
- No reports on the quality of apps built with Antigravity on Googlebook hardware (performance, local build times).
- The Verge's full Googlebook review and Android Authority's article could not be fetched (blocked/403). Claims attributed to them come from search excerpts.
