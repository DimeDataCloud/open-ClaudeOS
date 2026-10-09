# Jev (TypeSafe AI): what is real, and the official API and SDKs

Research date: 2026-10-09. All registry data was read from the npm and PyPI JSON APIs on that date. No package was installed or run. Labels used below: **VERIFIED** (seen in a primary source, URL given), **PARTIALLY VERIFIED** (secondary source only, or the substance matches but the wording does not), **NOT FOUND / LIKELY FABRICATED**.

Short version: TypeSafe AI and Jev are real. Jev launched on 2026-09-15. Both SDK package names in the pasted description are correct. The broad description holds up. The pasted example code does not match the real SDK: the class name, method name, Choice field, answer field and the missing `instructions` are all wrong (details in Q3). The "under 300 ms" figure is not stated anywhere TypeSafe publishes.

---

## Q1. Do TypeSafe AI and the Jev model exist, and is Jev described as a "System One", non-generative, decision-only model?

### Takeaway
Both exist. TypeSafe AI is an SF-based AI lab that came out of stealth on 2026-09-15 with Jev, which it calls "the first System One model". Jev returns typed decisions with probabilities and does not generate text. That matches the pasted description, although TypeSafe does not use the exact word "non-generative". The founders, the $40M seed round and the 2024 founding date come from press coverage, not from TypeSafe's own pages.

### Cited Findings
- **VERIFIED.** The typesafe.ai homepage calls TypeSafe AI an AI lab "building machine-native intelligence infrastructure for automation", "Made in SF". It calls Jev "the first public System One Model … optimized for automation", in early access, and labels it "TS.AI.0S1". The page timestamp is Oct 8, 2026, the footer says "Version 0.01, ©2026", and the contact address is hello@typesafe.ai — [typesafe.ai](https://typesafe.ai)
- **VERIFIED.** The homepage tagline is "LLMs produce words for people. Jev produces typed decisions." It also says Jev returns "typed decisions with calibrated probabilities", and it describes "a new architecture, a new sampler, and a new training algorithm: Reinforcement Learning for Calibrated Decisions (RLCD)" — [typesafe.ai](https://typesafe.ai)
- **VERIFIED.** The docs say: "Jev is TypeSafe's flagship model and the first System One model… Jev evaluates typed questions against a state and returns structured results directly. No text generation, no parsing." — [docs.typesafe.ai/introduction](https://docs.typesafe.ai/introduction.md)
- **VERIFIED.** Another docs page says: "System One models do not write replies, produce code, or generate explanations of their reasoning. You define the possible answers through primitives." — [docs: System One](https://docs.typesafe.ai/concepts/system-one.md)
- **VERIFIED.** The name comes from Kahneman: "The System One name comes from the concept Daniel Kahneman popularized in his book *Thinking, Fast and Slow*… the emphasis is on fast, focused judgments." — [docs: System One](https://docs.typesafe.ai/concepts/system-one.md)
- **VERIFIED.** The docs also say: "Jev is a System One model. It does not generate text, write code, or hold a conversation." and Jev "is **not** a drop-in replacement for the LLM behind Claude Code, Cursor, opencode, Copilot…" — [docs: Jev with coding agents](https://docs.typesafe.ai/introduction/coding-agents.md)
- **VERIFIED.** The launch blog post is "Introducing System One Models & Jev". Its header date is Sep 15, 2026, but the page metadata says Oct 8, 2026, so the page was probably edited later. The author is "Diogo Almeida, founder, TypeSafe". It says: "While Jev gives up string generation, it's optimized for structured outputs" and calls Jev a "frontier-intelligence function call: unstructured state in, typed probabilistic decisions out." — [TypeSafe blog launch post](https://typesafe.ai/blog/introducing-system-one-models-and-jev)
- **VERIFIED.** Jev 1.13 is the current model (`jev-1.13.0`). The aliases `jev-latest` and `jev-preview` both point to it, and no preview build exists at the moment — [docs: Models](https://docs.typesafe.ai/models.md)
- **PARTIALLY VERIFIED (press, 2026-09-16).** TypeSafe came out of stealth on Tuesday 2026-09-15 and was co-founded in 2024. It is headquartered in San Francisco. VKTR names these people:
  - Diogo Almeida, co-founder and CEO, a former OpenAI researcher described as a "co-inventor of RLHF"
  - Erik Gafni, CTO, previously at Invitae and Freenome
  - Sasha Sheng, COO, formerly at Meta/FAIR

  VKTR also reports a "$40 million seed round led by DCVC", with a quote from DCVC GP James Hardiman. Author: Michelle Hawley — [VKTR](https://www.vktr.com/ai-platforms/chatgpt-cocreator-launches-typesafe-ai-with-jev/)
- **NOT FOUND on TypeSafe's own pages.** I found no founder list or funding figure on the homepage or the blog index. The blog index has a post titled "Diogo Almeida - Founders You Should Know", but it does not state his title — [typesafe.ai/blog](https://typesafe.ai/blog)
- **Corroborating identity (VERIFIED, registry).** The npm maintainers use @typesafe.ai addresses: `diogo149` (diogo@typesafe.ai) and `alliesafe` (allie@typesafe.ai). The PyPI maintainer is "Daniel Gafni <daniel@typesafe.ai>" — [npm registry JSON](https://registry.npmjs.org/@typesafe-ai/sdk); [PyPI JSON](https://pypi.org/pypi/typesafe-sdk/json)
- Other coverage turned up in search but I did not read it: The New Stack, LetsDataScience ("TypeSafe AI Launches Jev for Structured Software Decisions"), Futura-Sciences, The Register, and a Dealroom profile ("TypeSafe AI | Jev") — [search results: thenewstack.io/typesafe-jev-system-one](https://thenewstack.io/typesafe-jev-system-one/); [letsdatascience.com](https://letsdatascience.com/news/typesafe-ai-launches-jev-decision-model-889a38c0); [app.dealroom.co](https://app.dealroom.co/companies/typesafe_ai_jev)

### Inferences
- The pasted description is right on substance: decision-only, System One, no text generation. "Non-generative" is a fair paraphrase of TypeSafe's own words ("No text generation", "gives up string generation"), but TypeSafe does not use that word itself.
- The CTO named in the press is **Erik** Gafni, but the PyPI maintainer is **Daniel** Gafni (daniel@typesafe.ai). These may be two different people. Do not merge them.
- A search snippet attributed the line "After co-inventing ChatGPT, I spent 2 years in stealth…" to a LinkedIn URL in the name of a "Daniel Eberharter". That pairing looks like a search-index mismatch. I did not use it.

### Gaps
- I could not confirm the funding amount, lead investor or founding year from TypeSafe itself or from SEC/Crunchbase-type records. They come from one press article I read (VKTR) plus search snippets.
- I did not read the team page ("Our Team"). The homepage fetch did not show any names.
- I did not search Hacker News or Product Hunt for launch threads because of the time budget.

---

## Q2. What is "noul"? Is it a real term? Which question and answer types does the real API support?

### Takeaway
"Noul" is a real, documented TypeSafe question type, not a typo. It is a yes/no question, and its answer is a single number from 0 to 1: the probability that the answer is yes. The API supports exactly three question types: `choice`, `score` and `noul`. The docs do not explain where the name comes from.

### Cited Findings
- **VERIFIED.** The three question types are listed as: "The three TypeSafe question types (Choice, Score, Noul)" — [docs llms.txt index](https://docs.typesafe.ai/llms.txt)
- **VERIFIED.** The docs define it this way: "A Noul question asks the TypeSafe model to evaluate a yes/no question and return the probability that the answer is yes… A Noul answer is a single number representing the probability that the answer is yes where 0 means no and 1 means yes." — [docs: Noul](https://docs.typesafe.ai/primitives/noul.md)
- **VERIFIED.** A Noul has no separate `confidence` field: "There is no separate `confidence` value for a Noul… A Noul's probability distribution has only two outcomes… so the single `noul` value describes it completely." — [docs: Noul](https://docs.typesafe.ai/primitives/noul.md)
- **VERIFIED.** The Noul request fields are:
  - `type: "noul"`
  - `instructions`, which the HTTP API marks as required
  - an optional `criteria` object with `true` and `false` descriptions — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** The Choice type "picks one option from a set you define". Its `criteria` field is required and is a "map of option to rubric description; use null when an option needs no extra detail. You can have a maximum of 255 options per Choice." The answer contains `choice`, `probabilities` (a map whose values sum to 1) and `confidence` — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** The Score type rates the state on a rubric. Its `criteria` field is "an ordered array of level descriptions… at least two levels; the API accepts up to 10". The answer contains `score` (a probability-weighted value that "can land between levels"), `legend`, `probabilities` and `confidence` — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** The JS SDK types match the API:
  - Questions: `NoulQuestion { type: "noul"; instructions?; criteria?: { true?; false? } }`, `ChoiceQuestion { type: "choice"; instructions?; criteria: T }`, `ScoreQuestion { type: "score"; instructions?; criteria: [EntryType, EntryType, ...] }`
  - Answers: `NoulResponse { type: "noul"; noul: number }`, `ChoiceResponse { choice; confidence; probabilities }`, `ScoreResponse { score; confidence; legend; probabilities }`

  Source: [typesafe-sdk-js v0.6.0 src/types.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/types.ts)
- **VERIFIED.** The Python SDK exposes `Choice`, `Noul`, `NoulCriteria` and `Score` classes — [docs: Noul](https://docs.typesafe.ai/primitives/noul.md); [PyPI typesafe-sdk README](https://pypi.org/project/typesafe-sdk/)
- **NOT FOUND.** I searched the full docs text ([llms-full.txt](https://docs.typesafe.ai/llms-full.txt), about 910 KB) and the launch blog post. Neither explains the name "Noul", and the launch post does not use the word at all — [launch post](https://typesafe.ai/blog/introducing-system-one-models-and-jev)

### Inferences
- The pasted description ("predefined choices, scores, or yes/no questions (called 'noul')") gets all three types and the meaning of noul right.
- "Noul" is TypeSafe's own coinage. Any etymology (for example a blend of "null" and "bool") would be guesswork, because none is documented.

### Gaps
- I found no official explanation of the name "Noul".

---

## Q3. What does the real API look like, and does the pasted example code match it? (Every discrepancy)

### Takeaway
The real API is a single endpoint, `POST https://api.typesafe.ai/v1/systemone`, with Bearer auth. The request body has `state`, `model` and `questions`. The response has `model`, `answers` and `usage`. The JS SDK's entry point is `new TypeSafeClient()` with the method `client.systemOne()`, and the Python equivalent is `TypeSafeClient().system_one()`. The pasted code gets the overall idea and the `state` field and `type: 'noul'` right. It gets the class name, the method name, the Choice field name and shape, and the answer field wrong, and it leaves out the required `instructions`.

### Cited Findings
- **VERIFIED.** Endpoint and auth:
  ```
  POST https://api.typesafe.ai/v1/systemone
  Authorization: Bearer <API_KEY>
  Content-Type: application/json
  ```
  The request body requires `state` (string, object or array), `model` (for example `"jev-latest"`) and `questions` (a map of question id to Question). About the question id, the docs say: "The key is not sent to the underlying model and is not used in inference." — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** The response body has `model` (the versioned ID, for example `"jev-1.13.0"`), `answers` (keyed by the same ids as the questions) and `usage {input_tokens, output_tokens}`. Example: `{"model":"jev-1.13.0","answers":{"is_urgent":{"type":"noul","noul":0.95}},"usage":{"input_tokens":296,"output_tokens":20}}` — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** A second endpoint, `GET /v1/models`, lists model names, descriptions and release dates — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** Error codes: 401 (bad or missing key), 422 (validation failure, "for example a missing required field or a malformed question"), 429 (rate limit) and 529 (overloaded) — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED (JS SDK source v0.6.0).**
  - The package exports `TypeSafeClient`, the builder helpers `choice`, `noul` and `score`, the error classes and the types. It does **not** export anything named `TypeSafe`. Source: [src/index.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/index.ts)
  - The client defaults are `DEFAULT_BASE_URL = "https://api.typesafe.ai"` and `DEFAULT_MODEL = "jev-latest"`.
  - The methods are `systemOne(request, options)`, which sends `POST /v1/systemone`, and `models.list()`, which sends `GET /v1/models`. There is no `evaluate` method. Source: [src/client.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/client.ts)
- **VERIFIED.** `TypeSafeClientConfig` accepts the following options. Source: [src/types.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/types.ts)

  | Option | Behaviour (SDK v0.6.0) |
  | --- | --- |
  | `apiKey` | Falls back to the `TYPESAFE_API_KEY` environment variable |
  | `baseURL` | Base URL override |
  | `defaultModel` | Defaults to `jev-latest` |
  | `timeout` | 10,000 ms per attempt by default |
  | `retry` | 2 retries by default, on 408, 429 and 5xx |
  | `logLevel` | Logging verbosity |
  | `defaultHeaders` | Extra headers sent with every request |
  | `dangerouslyAllowBrowser` | Browser use is refused unless this is set |
  | `fetch` | Custom fetch implementation |
- **VERIFIED.** The official JS quickstart:
  ```ts
  import { choice, TypeSafeClient } from "@typesafe-ai/sdk";
  const client = new TypeSafeClient();
  const response = await client.systemOne({
    state: { document: "I was charged twice. Please fix this ASAP." },
    questions: { category: choice("What is this ticket about?", { billing: null, technical: null, other: null }) },
  });
  console.log(response.answers.category.choice);
  ```
  Source: [npm README / docs.typesafe.ai/sdk/javascript](https://docs.typesafe.ai/sdk/javascript)
- **VERIFIED.** JS builder signatures:
  - `noul(instructions = null, criteria?)`
  - `choice(instructions, criteria)`, which throws if `criteria` is an array ("Choice criteria must be a map of labels to descriptions, not a list.")
  - `score(instructions, criteria)`, which throws if `criteria` is not an array

  Source: [src/questions.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/questions.ts)
- **VERIFIED.** The official Python quickstart is `from typesafe_sdk import Choice, TypeSafeClient`, then `with TypeSafeClient() as client: response = client.system_one(state={...}, questions={"category": Choice(instructions=..., criteria={...})})`. Answers can be read with `response.answers[...]` or with the typed accessors `.choices[...]`, `.nouls[...]` and `.scores[...]` — [PyPI typesafe-sdk README](https://pypi.org/project/typesafe-sdk/); [docs llms-full.txt](https://docs.typesafe.ai/llms-full.txt)

**Discrepancy table: pasted example vs. documented SDK (JS v0.6.0, docs as of 2026-10-09)**

| # | Pasted code | Real SDK/API | Status |
|---|---|---|---|
| 1 | `new TypeSafe({ apiKey })` | `new TypeSafeClient({ apiKey })` or `new TypeSafeClient()` with `TYPESAFE_API_KEY` set. Nothing named `TypeSafe` is exported ([index.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/index.ts)). | **Wrong class name.** The `apiKey` option itself is real. |
| 2 | `jev.evaluate({...})` | `client.systemOne({...})`. There is no `jev` object and no `evaluate` method. The model is chosen through the `model` field or `defaultModel` (`jev-latest`) ([client.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/client.ts)). The docs do describe the endpoint as an "evaluation endpoint", which may be where `evaluate` came from. | **Wrong method and wrong object** |
| 3 | `state: {...}` | `state` (string, object, array or null) | **Correct** |
| 4 | `is_safe: { type: 'noul' }` (no instructions) | `type: "noul"` is correct. The HTTP API marks `instructions` as **required**, while the SDK types make it optional (default `null`). The question key `is_safe` "is not sent to the underlying model", so without `instructions` the model is never told what to judge ([API ref](https://docs.typesafe.ai/api.md)). | **Partially correct.** The required question text is missing. |
| 5 | `action_type: { type: 'choice', options: [...] }` | The field is `criteria`, a **map** of label to description (or `null`), up to 255 labels. The `choice()` helper rejects arrays, and there is no `options` field ([API ref](https://docs.typesafe.ai/api.md), [questions.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/questions.ts)). `instructions` is also missing here. | **Wrong field name and wrong shape** |
| 6 | `decision.answers.is_safe.probability` | `response.answers.is_safe.noul` (a number from 0 to 1). No answer type has a `probability` field. Choice and Score answers have a plural `probabilities` **map**, plus `confidence` ([types.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/types.ts)). | **Wrong field name** |
| 7 | `decision.answers...` | `answers` is correct, and the response also carries `model` and `usage` | **Correct** |
| 8 | (implied) separate per-model client `jev` | One endpoint and one client. The `model` string selects Jev (`jev-latest`, `jev-preview` or `jev-1.13.0`) ([Models](https://docs.typesafe.ai/models.md)) | **Conceptually wrong** |

### Inferences
- Running the pasted code against `@typesafe-ai/sdk@0.6.0` would fail before any request is sent: `TypeSafe` is not an export, and `.evaluate` does not exist.
- If the call were fixed but the question shapes left as they are, the API would most likely return 422 (missing required `instructions`, and `options` in place of `criteria`). I infer this from the documented 422 semantics. I did not test it.
- Even if it ran, reading `.probability` would give `undefined`.
- The pasted code reads like an LLM's plausible guess at the API ("evaluate", "options", "probability"). That fits the user's suspicion that the description is partly AI-generated.

### Gaps
- I did not observe live server behaviour, because the task said not to run code. Two things are unconfirmed: whether the server rejects a Noul with no `instructions`, or accepts it as `null` the way the SDK typing allows; and how it handles unknown fields like `options`.

---

## Q4. Do `@typesafe-ai/sdk` (npm) and `typesafe-sdk` (PyPI) exist? Versions, dates, maintainers, repos, and look-alike packages

### Takeaway
Both packages exist and are official. Each lists @typesafe.ai maintainers and links to repositories under github.com/typesafe-ai. On npm, `@typesafe-ai/sdk` 0.6.0 is the latest (published 2026-09-15), built by GitHub Actions with provenance. On PyPI, `typesafe-sdk` 0.7.2 is the latest (2026-09-26). Several look-alike packages exist: two community or third-party wrappers, one defensive anti-slopsquatting shim, one 2010 package with an unrelated purpose, and a Vercel AI SDK provider.

### Cited Findings
- **VERIFIED: npm `@typesafe-ai/sdk`** — [registry JSON](https://registry.npmjs.org/@typesafe-ai/sdk)
  - The package was created 2026-09-12T02:56:18Z.
  - Versions:
    - `0.0.0-bootstrap.0`, 2026-09-12T02:56:19Z. Its description reads "Placeholder for the TypeSafe JavaScript and TypeScript SDK; not a functional SDK."
    - `0.5.7`, 2026-09-12T04:13:21Z
    - `0.6.0`, 2026-09-15T18:17:19Z. This is the `latest` dist-tag, described as "TypeScript SDK for the TypeSafe API".
  - Maintainers are `alliesafe` (allie@typesafe.ai) and `diogo149` (diogo@typesafe.ai). The `author` field of the real versions is "evinism". Versions 0.5.7 and 0.6.0 were published by "GitHub Actions" with npm/SLSA provenance attestations.
  - Repository: `github.com/typesafe-ai/typesafe-sdk-js`. Homepage: `https://docs.typesafe.ai/sdk/javascript`.
  - License MIT, Node >= 20, ESM + CJS + TypeScript types, and no runtime dependencies.
- **VERIFIED.** The npm search API reported about 1.5M weekly and 2.7M monthly downloads for `@typesafe-ai/sdk`, and "267 dependents", as of 2026-10-09 — [npm search API](https://registry.npmjs.org/-/v1/search?text=typesafe-ai&size=20)
- **VERIFIED.** The JS changelog: v0.5.7 (2026-09-11) was the "initial public release". v0.6.0 (2026-09-15) brought a breaking change, so that `Score.criteria` is now an ordered sequence instead of an integer-keyed dict — [docs: JS changelog](https://docs.typesafe.ai/sdk/javascript/changelog.md)
- **VERIFIED: PyPI `typesafe-sdk`** — [PyPI JSON](https://pypi.org/pypi/typesafe-sdk/json)
  - Releases:

    | Version | Uploaded (UTC) |
    | --- | --- |
    | 0.0.1a0 | 2026-09-09T10:34Z |
    | 0.5.7 | 2026-09-11T23:05Z |
    | 0.6.0 | 2026-09-15T10:23Z |
    | 0.7.0 | 2026-09-18T09:12Z |
    | 0.7.1 | 2026-09-21T15:57Z |
    | 0.7.2 | 2026-09-26T21:20Z (latest) |
  - People: the author is "TypeSafe AI <support@typesafe.ai>" and the maintainer is "Daniel Gafni <daniel@typesafe.ai>". PyPI roles are Owner `danielgafni` and Maintainer `alliesafe`.
  - Repository: `github.com/typesafe-ai/typesafe-sdk-python`. Docs: `docs.typesafe.ai/sdk/python/`.
  - License MIT, Python >= 3.10.
  - Dependencies: `httpx2`, `pydantic>=2.12`, `tenacity`, `typing-extensions`. There is an optional `http2` extra.
  - Classifier: "Development Status :: 5 - Production/Stable".
  - Import name: `typesafe_sdk`.
- **VERIFIED.** The Python changelog:
  - v0.5.7, the initial public release, is dated 2026-09-14 in the changelog, but PyPI shows an upload at 2026-09-11T23:05Z. This is a small date mismatch.
  - v0.6.0 (2026-09-15) made the same Score criteria change as the JS SDK.
  - v0.7.0 (2026-09-18) switched from msgspec to pydantic and added `response_model`.
  - v0.7.1 (2026-09-21) added "examples for usage with AI gateways".
  - v0.7.2 (2026-09-26) added the `http2` extra.

  Source: [docs: Python changelog](https://docs.typesafe.ai/sdk/python/changelog.md)
- **VERIFIED (repo exists).** I fetched source files from `typesafe-ai/typesafe-sdk-js` at tag `v0.6.0` (index.ts, client.ts, types.ts, questions.ts, resources/models.ts) — [GitHub raw, v0.6.0](https://github.com/typesafe-ai/typesafe-sdk-js/tree/v0.6.0)
- **Look-alike packages (VERIFIED, registry metadata):**
  - PyPI `typesafe-ai` 0.1.0, uploaded 2026-09-17, is **not affiliated** with TypeSafe. It is a "redirect shim" registered by Gerome Dexheimer (PyPI owner `PleasePrompto`) as a defence against "slopsquatting". It says that "AI coding assistants sometimes invent the package name `typesafe-ai`". It depends on `typesafe-sdk>=0.6.0` and re-exports it — [PyPI JSON](https://pypi.org/pypi/typesafe-ai/json)
  - PyPI `typesafe` 0.9.1 (2010-04-09) is an unrelated "formal type asserting decorators" library by Krister Hedfors — [PyPI JSON](https://pypi.org/pypi/typesafe/json)
  - npm `@ai-sdk/typesafe-ai` 3.0.17 (published 2026-10-08) is described as "AI SDK integration for Typesafe AI". It is maintained by `vercel-release-bot` in the `vercel/ai` repo, so it is third-party (Vercel), not TypeSafe's own SDK — [npm search API](https://registry.npmjs.org/-/v1/search?text=typesafe-ai&size=20)
  - npm `@effect-uai/typesafe-ai` 0.18.0 ("TypeSafe AI provider… (Jev System One decision model)", by `jantxu`/betalyra) and npm `@effect-agent/ai-typesafe` 0.1.0-beta.100 (by `danieljvdm`) are community packages — [npm search API](https://registry.npmjs.org/-/v1/search?text=typesafe-ai&size=20)

### Inferences
- Both package names in the pasted description are correct. The publish history fits the 2026-09-15 launch: placeholders went up on 2026-09-09 and 2026-09-12, the first public releases on 2026-09-11 and 2026-09-12, and v0.6.0 on launch day.
- Someone registered a defensive shim for `typesafe-ai` because LLMs invent that name. Anyone copying install commands from AI-generated text should check the exact names: `@typesafe-ai/sdk` and `typesafe-sdk`.
- The Scala/Akka company "Typesafe" (now Lightbend) did not appear in any registry result for these names. The Typesafe Config Java library lives on Maven and has nothing to do with this.

### Gaps
- I could not list the full `typesafe-ai` GitHub organisation. Org-level GitHub API access was blocked in this session.
- I did not check the `typesafe-sdk-python` repo directly. It is known only from the PyPI metadata link.
- The npm download counts come from the search API's aggregate fields. I did not cross-check them against the npm downloads API.

---

## Q5. Published latency, accuracy, calibration, pricing, rate limits and benchmarks. Is "under 300 ms" a documented figure?

### Takeaway
"Under 300 ms" is **not** a documented figure. TypeSafe gives three different numbers: "about 100 ms" for most queries, "150ms" for real-time use cases, and "70ms-500ms" end-to-end in the launch post. The top of that last range is above 300 ms. Pricing is $0.042 per million input tokens, and output is free. Rate limits are 100K tokens/s and 80 requests/s, with a warning that they change dynamically. TypeSafe publishes no standalone calibration metric (ECE, Brier) and no independent benchmark. Its speed and cost multipliers come from vendor-run evaluations scored against other LLMs.

### Cited Findings
- **VERIFIED.** Pricing for Jev 1.13 is "$42 / $0.042" per billion / per million tokens. It is "charged per input token. Output tokens are free." — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** Rate limits are "100K tokens per second / 80 requests per second". The docs warn: "**Rate limits are adjusting dynamically.** … can change without notice… Higher limits are available on custom and enterprise plans." — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** "Most queries complete in about 100 ms. System One is fast enough for real-time request paths and user interfaces." — [docs: How to build with TypeSafe](https://docs.typesafe.ai/concepts/how-to-build-with-system-one)
- **VERIFIED.** "Frontier intelligence at real-time speeds (150ms) means AI can make decisions faster than human perception." — [docs: Example use cases](https://docs.typesafe.ai/concepts/use-case-map)
- **VERIFIED.** The launch post says "End-to-end response time is 70ms-500ms", with evals "generally run from our laptops on the West Coast". It compares this with "3 to 329 seconds" for frontier LLMs and claims "40x-200x faster… for System One shaped queries" — [launch post](https://typesafe.ai/blog/introducing-system-one-models-and-jev)
- **VERIFIED.** Two docs cookbook runs report mean round-trip latencies of **111 ms** (Noul) and **114 ms** (Choice). In the same tables, claude-haiku-4-5 took about 1.0–3.9 s and claude-opus-4-8-reasoning about 10.4–13.9 s — [docs: Self-consistency nouls](https://docs.typesafe.ai/cookbooks/consistency_noul_cookbook.md); [docs: Self-consistency choices](https://docs.typesafe.ai/cookbooks/consistency_choice_cookbook.md)
- **VERIFIED (vendor marketing).** The homepage claims "193.6x Faster, 444.6x Cheaper", "based on workflows for System One tasks". Its chart shows Jev at 0.114 s against 8.566 s for LLMs, and $0.000081 against $0.013880. It also says "238x Lower input price than Claude Fable 5.1" and "Zero Hallucinations". The chart values work out to about 75x and 171x, not the headline multipliers — [typesafe.ai](https://typesafe.ai)
- **VERIFIED.** The launch post qualifies those claims:
  - The workflow evals used "the average of GPT-6 Astra and Fable 5.1" as reference probabilities. The post admits this may bias results toward those providers. Accuracy is therefore measured as agreement with reference LLMs, not against ground truth.
  - The 193x and 444x figures are "on the higher end of real world gains".
  - The 0% hallucination claim "is not empirical". It rests on "Schema matching is guaranteed".

  Source: [launch post](https://typesafe.ai/blog/introducing-system-one-models-and-jev)
- **VERIFIED.** The docs' position on calibration: "Calibration is measured across groups of predictions; it does not guarantee that an individual answer is correct." — [docs: System One](https://docs.typesafe.ai/concepts/system-one.md)
- **VERIFIED.** Results from the docs cookbooks:
  - On a CLERC legal re-ranking task (40 queries), top-1 accuracy rose from 5% to 18% and top-10 from 38% to 62%.
  - Batching 13 questions into one call was "12.2x cheaper and 10.0x faster with no change in answers".

  Source: [docs llms.txt index](https://docs.typesafe.ai/llms.txt)
- **VERIFIED.** Known weaknesses, last reviewed 2026-10-02, are listed for Jev 1.13:
  - literal reading
  - math and counting
  - date comparison
  - indirection
  - large irrelevant state
  - adversarial content
  - contradictory criteria
  - sensitivity to the order of Choice options
  - generation, where the advice is "Use a generative model"

  Source: [docs: Jev 1.13 jaggedness](https://docs.typesafe.ai/model-jaggedness/jev-1.13.md)
- **PARTIALLY VERIFIED (press, 2026-09-16).** VKTR repeats the company's 70–500 ms figure, "up to 193x faster than Claude Sonnet 5" and "approximately 444x cheaper than Claude Opus 5". It adds: "No independent evaluation has confirmed these claims as of yet." — [VKTR](https://www.vktr.com/ai-platforms/chatgpt-cocreator-launches-typesafe-ai-with-jev/)

### Inferences
- "Under 300 ms" looks like a rounded paraphrase that someone (or some AI) made up. The ~100 ms typical figure supports "usually well under 300 ms". The documented upper end of 500 ms contradicts it as a guarantee. No SLA or latency percentile (p50/p95/p99) is published.
- Network location matters. TypeSafe's own latency numbers were measured from US West Coast laptops.
- The accuracy and "intelligence" claims compare Jev with other LLMs and were produced by TypeSafe. Treat them as unverified marketing until an independent evaluation exists.

### Gaps
- I found no published calibration metric, such as ECE, Brier score or reliability diagrams with numbers. I searched the full docs for "ECE", "expected calibration", "Brier" and "benchmark", and checked the launch post.
- No latency percentiles or SLA are published.
- I did not read the "Lies, Damned Lies, and Benchmarks" post (/blog/antibenchmaxxing) or the case study "Jack & Jill & Jev: Cutting candidate screening costs by 88%" ([blog index](https://typesafe.ai/blog)).
- I found no independent benchmark of Jev.

---

## Q6. Input limits, modalities, language support, and data handling or retention

### Takeaway
Jev accepts text only: a string, a JSON object or an array. Each request is capped at 64k tokens in total, and the state plus the longest single question is capped at 32k. English works best. TypeSafe says it does not train on customer requests or responses, and it offers zero data retention (ZDR) to enterprise customers. Retention details sit in a Data Processing Agreement that I did not read.

### Cited Findings
- **VERIFIED.** "Context length: 64k tokens per request; 32k tokens for `state` plus the longest question… The 64k budget covers the `state` plus all questions combined." — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** "Input: Text only. String, JSON object, or array of text values. No image, audio, or video input." — [docs: Models](https://docs.typesafe.ai/models.md); [docs: System One](https://docs.typesafe.ai/concepts/system-one.md)
- **VERIFIED.** Per-question limits: up to 255 options per Choice, and 2 to 10 levels per Score — [docs: API reference](https://docs.typesafe.ai/api.md)
- **VERIFIED.** "English is the primary training language and where accuracy is currently best. Other languages, including CJK scripts, are handled but not equally well." — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** Jev "is not fine-tuned or LoRA-adapted with customer data… the same weights serve every account" — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** "Jev is not trained on customer requests or responses. See Legal for the Data Processing Agreement, the Privacy Policy, and details on zero data retention (ZDR) for enterprise customers." — [docs: Models](https://docs.typesafe.ai/models.md)
- **VERIFIED.** The legal page links a Data Processing Agreement ("including data retention"), a Master Customer Agreement and a Privacy Policy ("our commitment not to train models on user data"). It adds: "We also offer zero data retention (ZDR) for enterprise customers." — [docs: Legal](https://docs.typesafe.ai/legal.md); [DPA](https://typesafe.ai/legal/data-processing); [Privacy Policy](https://typesafe.ai/legal/privacy-policy)
- **VERIFIED.** The JS SDK refuses to run in a browser unless `dangerouslyAllowBrowser: true` is set, because the API key would be exposed. At `debug` log level it logs request bodies unredacted ("Known credential headers are redacted; bodies are not") — [src/types.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/types.ts); [src/client.ts](https://github.com/typesafe-ai/typesafe-sdk-js/blob/v0.6.0/src/client.ts)
- **VERIFIED (Claude-relevant positioning).**
  - The docs include a "Guardrails for LLMs" cookbook: "Screen every message going into and out of an LLM app with one TypeSafe request, thresholding hazard probabilities and severity to pass, review, block, or route."
  - The "Intent routing" pattern routes requests to "deterministic logic, a specialist LLM, or a human".
  - An "Agent skill" is offered as a "Drop-in skill for Claude Code, Codex, and other agent environments."

  Source: [docs llms.txt index](https://docs.typesafe.ai/llms.txt)
- **VERIFIED.** The launch post lists "Score, judge, verify, guardrail, and detect jailbreaks of LLM prompts, reasoning traces, and/or outputs." It does not mention Claude by name — [launch post](https://typesafe.ai/blog/introducing-system-one-models-and-jev)
- **VERIFIED.** API keys and a Playground are at console.typesafe.ai. Access is early access, with developers admitted from a waitlist — [typesafe.ai](https://typesafe.ai); [docs: Jev with coding agents](https://docs.typesafe.ai/introduction/coding-agents.md); [VKTR](https://www.vktr.com/ai-platforms/chatgpt-cocreator-launches-typesafe-ai-with-jev/)

### Inferences
- The pasted claim that Jev is a "fast, low-cost screening layer, guardrail or decision gate around LLMs like Claude" matches use cases TypeSafe documents itself. TypeSafe frames this as generic LLM guardrailing and routing. It does not describe a Claude-specific integration. Claude models appear only as comparison baselines and as a target for the coding-agent skill.
- The pasted claim that "docs and API keys live at typesafe.ai" is accurate in substance: docs are on docs.typesafe.ai, keys and the Playground on console.typesafe.ai, and the API on api.typesafe.ai. Access may still be gated by the waitlist.
- Sensitive data: the default retention period is not stated in the docs. Only enterprise customers get ZDR. Anyone sending regulated data should read the DPA first.

### Gaps
- I did not read the DPA or the Privacy Policy, so the actual default retention period for API inputs and outputs is not captured here.
- I found no published information on data residency or region, SOC 2 or other certifications, or a public uptime or status page.
