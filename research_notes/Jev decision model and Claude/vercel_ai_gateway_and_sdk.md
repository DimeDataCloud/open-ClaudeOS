# Vercel AI Gateway and AI SDK support for Jev (TypeSafe AI)

Research date: 2026-10-09. All pages were fetched directly on that date, and the dates given are the publish or last-updated dates the pages show. Labels: VERIFIED means confirmed on a Vercel or vercel/ai primary source. PARTIALLY VERIFIED means the substance is real but the wording in the pasted description is wrong or imprecise. NOT FOUND / LIKELY FABRICATED means no primary source supports it.

## Is `typesafe-ai/jev` listed on the Vercel AI Gateway model list, and with what ID, pricing, provider and capabilities?

### Takeaway
VERIFIED. Jev is listed on AI Gateway with the model ID `typesafe-ai/jev`. It costs $0.042 per 1M input tokens, the page lists no output price, the context window is 32,000 tokens, and two providers serve it: `typesafe-ai` and `digitalocean`. It is a decision model (earlier called an "evaluation" model), not a chat or language model.

### Cited Findings
- The model page, as fetched on 2026-10-09, shows: name "Jev", described as TypeSafe AI's System One decision model for structured decisions in software; model ID `typesafe-ai/jev`; Type "Evaluation"; providers `typesafe-ai` and `digitalocean`; $0.042/1M input tokens with no output price; context window 32,000; maximum output tokens 0. The page gives no release date, latency or throughput figures, and no detailed capability metadata. — [AI Gateway model page: Jev](https://vercel.com/ai-gateway/models/jev)
- The sample code on the model page now uses the renamed API, `import { experimental_decide as decide } from 'ai';`. — [AI Gateway model page: Jev](https://vercel.com/ai-gateway/models/jev)
- The Decision docs (last updated 2026-10-07) say to use the **Decision** filter on the models page (`/ai-gateway/models?capabilities=decision`) to see the supported decision models. They also say "some decision models price input tokens only". — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision)
- A sample gateway response in the docs shows the routing metadata `resolvedProvider: "typesafe-ai"`, `canonicalSlug: "typesafe-ai/jev"`, and a per-call cost of `"0.00001155"` for 275 input tokens. — [Vercel Docs: TypeSafe API with AI Gateway](https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe)
- Decision requests are not supported through Chat Completions, Responses, or the Anthropic- or Cohere-compatible endpoints. — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision)
- The launch changelog, "TypeSafe AI's Jev now available on AI Gateway", is dated **2026-09-16** and written by Rohan Taneja, Zachary Chen and Jerilyn Zheng. — [Vercel Changelog](https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway)

### Inferences
- The model page's Type label ("Evaluation") still uses the old name. The docs renamed "evaluation models" to "decision models" around 2026-10-05 (see the AI SDK section below), so the label probably hasn't been updated yet.
- $0.042/1M input tokens with no output price matches vendor-reported pricing in secondary coverage (input-only billing), but I relied only on the Vercel page for the figure.

### Gaps
- The model page gives no latency, throughput or release-date fields, so I can't cite those from Vercel.
- I did not separately confirm whether DigitalOcean-served Jev is priced differently from TypeSafe-served Jev. The fetched page showed one price.

## Does the AI SDK have an "evaluate" function or provider, or a TypeSafe/Jev provider package, and what is the real code shape?

### Takeaway
PARTIALLY VERIFIED, and the description's wording is wrong. "Evaluate" is a **function**, not a provider. The `ai` package added `experimental_evaluate` in 7.0.103 (2026-09-16). It was renamed `experimental_decide` in 7.0.128 (2026-10-05), and `experimental_evaluate` remains as a deprecated alias. There is also a real dedicated provider package, **`@ai-sdk/typesafe-ai`** (not `@ai-sdk/typesafe`, which does not exist on npm). When you use AI Gateway, though, you just pass the string `'typesafe-ai/jev'`, or `gateway.decisionModel('typesafe-ai/jev')`, to `decide`/`evaluate`. Calls do **not** go through `generateObject` or `generateText`.

### Cited Findings
- **Changelog for `ai` (vercel/ai `packages/ai/CHANGELOG.md`, fetched 2026-10-09):**
  - 7.0.103: "Add `experimental_evaluate` and the isolated experimental v4 evaluation model specification for Choice, Score, and Boolean questions against shared state…". npm publish time 2026-09-16T18:36Z.
  - 7.0.104: evaluation model aliases and registry resolution (`customProvider` accepts `evaluationModels`). Published 2026-09-16.
  - 7.0.105: "Resolve evaluation model IDs through AI Gateway when no default provider is configured". Published 2026-09-16.
  - 7.0.111: "add telemetry support to `experimental_evaluate`". Published 2026-09-22.
  - 7.0.128: "feat: rename `evaluate` to `decide`". Published 2026-10-05T18:25Z.
  - Latest on 2026-10-09 is `ai@7.0.136`.
  - Sources: [vercel/ai packages/ai/CHANGELOG.md](https://github.com/vercel/ai/blob/main/packages/ai/CHANGELOG.md); [npm registry: ai](https://registry.npmjs.org/ai)
- The Sept 16 launch changelog says Jev is accessed "through the experimental `evaluate` API in AI SDK 7" and requires AI SDK 7.0.105 or later. Its code sample (verbatim):
  ```ts
  import { experimental_evaluate as evaluate } from 'ai';

  const result = await evaluate({
    model: 'typesafe-ai/jev',
    state: 'The support agent issued a full refund to the customer.',
    questions: {
      refunded: { type: 'boolean', instructions: 'Was a refund issued?' },
    },
    providerOptions: { gateway: { zeroDataRetention: true } },
  });
  ```
  — [Vercel Changelog, 2026-09-16](https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway)
- The current docs (last updated 2026-10-07) say: "Through the AI SDK, decisions require `ai` 7.0.128 or later, or `experimental_evaluate` on earlier AI SDK 7 releases", and "Decision models were previously called evaluation models. The `experimental_evaluate` export and `gateway.evaluationModel()` still work as deprecated aliases." The current shape is:
  ```ts
  import { experimental_decide as decide } from 'ai';
  import { gateway } from '@ai-sdk/gateway'; // optional; a plain string ID also works
  const result = await decide({
    model: gateway.decisionModel('typesafe-ai/jev'), // or just 'typesafe-ai/jev'
    state: 'The support agent issued a full refund to the customer.',
    questions: { refunded: { type: 'boolean', instructions: 'Was a refund issued?' } },
  });
  // result.answers -> { refunded: { type: 'boolean', probability: 0.99 } }
  ```
  — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision)
- **Question types (AI SDK and Gateway):**
  - `boolean` returns `probability`, which is P(true).
  - `choice` takes `criteria` as a record of options and returns `choice` plus `probabilities`.
  - `score` takes `criteria` as an ordered array of at least two labels and returns an interpolated `score` plus `probabilities`.
  - `state` accepts a string, an object or an array.
  - Several questions are answered in one round trip.
  - [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision)
- **TypeSafe's native naming differs.** The TypeSafe SDK's Boolean type is `noul`, and its method is `client.systemOne(...)`. The Gateway's TypeSafe-compatible API accepts `noul`, while the AI SDK and `/v1/evaluate` use `boolean`. — [Vercel Docs: TypeSafe API with AI Gateway](https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe) (last updated 2026-10-05)
- **Dedicated provider package `@ai-sdk/typesafe-ai`:**
  - npm: created 2026-09-16 (3.0.0, published 2026-09-16T18:38Z), latest 3.0.17 (2026-10-08). The repo directory is `packages/typesafe-ai` in `vercel/ai`.
  - Its 3.0.0 changelog: "Add the TypeSafe provider for `experimental_evaluate`, supporting native Choice, Score, and Boolean questions in one request… probability distributions, confidence metadata…". 3.0.13 (2026-10-05): "rename `evaluate` to `decide`".
  - Sources: [npm: @ai-sdk/typesafe-ai](https://registry.npmjs.org/@ai-sdk%2Ftypesafe-ai); [vercel/ai packages/typesafe-ai/CHANGELOG.md](https://github.com/vercel/ai/blob/main/packages/typesafe-ai/CHANGELOG.md)
- **The provider docs show the direct, non-Gateway shape.** `import { typeSafeAi } from '@ai-sdk/typesafe-ai'; import { experimental_decide } from 'ai';` with `model: typeSafeAi.decisionModel('jev-latest')`. It reads `TYPESAFE_AI_API_KEY`, and the base URL defaults to `TYPESAFE_AI_BASE_URL`, then `https://api.typesafe.ai/v1`. Confidence is at `result.providerMetadata.typesafe.confidence[questionId]` for Choice and Score. — [vercel/ai content/providers/01-ai-sdk-providers/105-typesafe-ai.mdx](https://github.com/vercel/ai/blob/main/content/providers/01-ai-sdk-providers/105-typesafe-ai.mdx)
- The AI SDK docs page "Decisions" lists decision-model support for TypeSafe AI (`typeSafeAi.decisionModel('jev-latest')`), OpenAI (`openai.decisionModel('gpt-6-luna')`), Anthropic and Google. TypeSafe and OpenAI are native, and Anthropic and Google "adapt structured output", with prompted Boolean probabilities. The old URL `ai-sdk.dev/docs/ai-sdk-core/evaluation`, which the 2026-09-21 changelog links to, now serves this "Decisions" page. — [AI SDK docs: Decisions](https://ai-sdk.dev/docs/ai-sdk-core/decisions) (fetched via [ai-sdk.dev/docs/ai-sdk-core/evaluation](https://ai-sdk.dev/docs/ai-sdk-core/evaluation))
- `@ai-sdk/typesafe` (the name suggested in the task) returns "Not found" on the npm registry. — [npm registry query](https://registry.npmjs.org/@ai-sdk%2Ftypesafe) (checked 2026-10-09)
- TypeSafe's own client is `@typesafe-ai/sdk`, latest 0.6.0, published 2026-09-15, repo `github.com/typesafe-ai/typesafe-sdk-js`. — [npm: @typesafe-ai/sdk](https://registry.npmjs.org/@typesafe-ai%2Fsdk)
- The HTTP API, with no SDK, is `POST https://ai-gateway.vercel.sh/v1/evaluate` with `model`, `state` and `questions`. The TypeSafe-compatible endpoint is `POST https://ai-gateway.vercel.sh/typesafe/v1/systemone`. — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision); [Vercel Docs: TypeSafe API](https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe)
- A second changelog, "AI Gateway now supports TypeSafe clients and an HTTP API for Jev", is dated **2026-09-21** and written by Rohan Taneja and Jerilyn Zheng. It added the TypeSafe-client base URL `https://ai-gateway.vercel.sh/typesafe` and the `/v1/evaluate` HTTP API. — [Vercel Changelog, 2026-09-21](https://vercel.com/changelog/ai-gateway-now-supports-typesafe-clients-and-an-http-api-for-jev) (also linked from the docs as `/changelog/ai-gateway-now-supports-typesafe-clients-and-http-api-for-jev`)

### Inferences
- The description's phrase "AI SDK evaluate provider (typesafe-ai/jev)" mixes up three real things: the `experimental_evaluate` function, the AI Gateway model ID `typesafe-ai/jev`, and the separate `@ai-sdk/typesafe-ai` provider package. It reads like a garbled summary of real material, not an invention. It is also out of date: since 2026-10-05 the preferred name is `experimental_decide`.
- New code should use `experimental_decide` with `ai >= 7.0.128`. Code pinned to an earlier 7.x release (such as the form-router template, which pins `ai@7.0.107`) uses `experimental_evaluate`, which still works as a deprecated alias.
- Both APIs are `experimental_`. The AI SDK docs say the API "is experimental and subject to change in patch releases", and the evaluate-to-decide rename in a patch version is an example of that.

### Gaps
- GitHub code and issue search on vercel/ai was blocked in this session because the GitHub API is scoped to configured repos. I verified through raw files on the `main` branch (CHANGELOGs, package.json, provider .mdx) instead of a code search, so I did not review issues or PRs mentioning "typesafe" or "jev".
- I did not read the `@ai-sdk/typesafe-ai` source code itself, only its changelog, package.json and docs.

## Do the two Knowledge Base articles exist, and what do they say about thresholds, fallback logic, code and dates?

### Takeaway
Both exist. "Route form submissions with Jev and AI SDK" is VERIFIED as a KB guide (published 2026-09-21, updated 2026-09-22, by Ben Sabic). It uses a **0.95 confidence threshold**, and the fallback is `openai/gpt-6-luna-fast`. "What is Jev, TypeSafe AI's System One model?" is PARTIALLY VERIFIED: it exists at `vercel.com/i/what-is-jev`, a Vercel explainer linked from the KB hub, not at a `/kb/guide/` URL. It was published 2026-09-19, updated 2026-10-07, by Ben Sabic. It discusses thresholds and gating only qualitatively, with no numbers. The detailed threshold guidance is in a sibling page, `vercel.com/i/jev-probabilities-and-thresholds`.

### Cited Findings
- **"Route form submissions with Jev and AI SDK"** is at `vercel.com/kb/guide/jev-ai-sdk-form-router`. Published 2026-09-21, last updated 2026-09-22, author Ben Sabic. The template repo is `github.com/vercel-labs/jev-ai-sdk-form-router`. It covers Next.js, AI SDK, AI Gateway, Zod, Vitest, React Email, Resend, pnpm and Node.js 22+. — [Vercel KB guide](https://vercel.com/kb/guide/jev-ai-sdk-form-router)
- **Threshold and gating in that guide:**
  - `CONFIDENCE_THRESHOLD = 0.95`, described as "a starting point".
  - Jev's answer is accepted only if its confidence is a valid number from 0 to 1 and at least 0.95. The unrounded value is compared, so 0.94999 falls back.
  - There are three fallback reasons: `low-confidence`, `missing-confidence` (absent or invalid metadata), and `jev-error` (failure or the 12-second timeout).
  - The fallback model receives the same questions and state but not Jev's answer, and its choice becomes final with no further confidence check.
  - If the fallback fails, no routing decision is returned.
  - Source: [Vercel KB guide](https://vercel.com/kb/guide/jev-ai-sdk-form-router)
- **Actual template code (`lib/router.ts` on `main`, fetched 2026-10-09):**
  ```ts
  import { experimental_evaluate as evaluate, generateText, Output } from "ai";
  const CONFIDENCE_THRESHOLD = 0.95;
  const JEV_TIMEOUT_MS = 12_000;
  const LUNA_TIMEOUT_MS = 25_000;
  // Jev call
  const result = await evaluate({
    abortSignal: AbortSignal.timeout(JEV_TIMEOUT_MS), maxRetries: 1,
    model: models.jev ?? "typesafe-ai/jev",
    questions: { destination: { criteria, instructions, type: "choice" } },
    state: { example: example.id, submission },
  });
  // confidence read from result.providerMetadata.typesafe.confidence.destination (validated with zod safeParse)
  if (confidence !== null && confidence >= CONFIDENCE_THRESHOLD) { /* accept Jev */ }
  // otherwise fallback:
  const { output } = await generateText({
    abortSignal: AbortSignal.timeout(LUNA_TIMEOUT_MS), maxOutputTokens: 1000, maxRetries: 1,
    model: models.luna ?? "openai/gpt-6-luna-fast",
    output: Output.object({ schema: z.object({ destination: z.enum(destinationIds) }) }),
    prompt: JSON.stringify({ questions, state }), reasoning: "low", system: "You route form submissions. …",
  });
  ```
  `package.json` pins `"ai": "7.0.107"`, `"next": "16.3.5"`, `"react": "19.2.8"` and `"zod": "^4.6.5"`. `.env.example` says: "Required locally unless you use Vercel OIDC: `AI_GATEWAY_API_KEY=`". — [vercel-labs/jev-ai-sdk-form-router lib/router.ts](https://github.com/vercel-labs/jev-ai-sdk-form-router/blob/main/lib/router.ts); [package.json](https://github.com/vercel-labs/jev-ai-sdk-form-router/blob/main/package.json); [.env.example](https://github.com/vercel-labs/jev-ai-sdk-form-router/blob/main/.env.example)
- Note on conflicting sources: one search-engine snippet said the fallback was `openai/gpt-5.6-luna-fast`. The live guide and the repo code both say **`openai/gpt-6-luna-fast`**, so the snippet was wrong. — [Vercel KB guide](https://vercel.com/kb/guide/jev-ai-sdk-form-router)
- **"What is Jev, TypeSafe AI's System One model?"** is at `vercel.com/i/what-is-jev`. Published 2026-09-19, last updated 2026-10-07, author Ben Sabic.
  - It covers inputs (text, JSON objects, arrays as one shared state), the question types (Choice, Score, Boolean), question design (include an "insufficient-evidence" option), decision traces, and the point that schema conformance ≠ semantic correctness.
  - On thresholds, it says to choose a routing threshold that balances assignment errors against human-review workload, and it gives **no numeric threshold**. The 0.70/0.20/0.10 values are only an illustrative distribution.
  - Gating advice: use an insufficient-evidence category and keep application code in charge of policy.
  - It contains no code samples.
  - Source: [Vercel: What is Jev](https://vercel.com/i/what-is-jev)
- **Sibling page "How should you use Jev's probabilities to set decision thresholds?"** Published 2026-09-19, updated 2026-10-07, by Ben Sabic.
  - Separates choice probability, confidence (concentration of the distribution) and score.
  - Advises calibrating cutoffs on labeled data.
  - Its hypothetical table: a 0.80 cutoff gives 38/760 wrong automatic routes (5%), and a 0.95 cutoff gives 4/400 (1%) with 600 sent to review.
  - Below-cutoff items, timeouts and failures go to human review or a defined destination. It does not describe a fallback-model pattern.
  - Source: [Vercel: Jev probabilities and thresholds](https://vercel.com/i/jev-probabilities-and-thresholds)
- **KB hub "Jev from TypeSafe AI"** is at `vercel.com/kb/jev-from-typesafe-ai`, published 2026-09-22.
  - Build guides it links: `/kb/guide/typesafe-jev-and-ai-sdk`, `/kb/guide/moderate-product-reviews-jev-tanstack-ai`, `/kb/guide/jev-ai-sdk-form-router`, `/kb/guide/auto-approve-tool-calls-eve-jev`.
  - Explainers it links: `/i/what-is-jev`, `/i/jev-use-cases`, `/i/jev-integrations`, `/i/when-to-use-jev`, `/i/jev-agent-control`, `/i/jev-probabilities-and-thresholds`, `/i/jev-vs-gpt-6-astra`, `/i/openai-decisions-api-vs-jev`, `/i/jev-vs-perplexity`, `/i/jev-vs-laya-vs-liquid-d1`, `/i/using-jev-in-tanstack-start-with-tanstack-ai`, `/i/what-is-openai-decisions-api`.
  - Source: [Vercel KB: Jev from TypeSafe AI](https://vercel.com/kb/jev-from-typesafe-ai)
- **Gateway-level fallback gating also exists as a product feature ("Decision Fallbacks", docs last updated 2026-10-08):**
  - You opt in per request via `providerOptions.gateway.models: [{ model: 'openai/gpt-6-astra', when: { question: 'intent', confidenceBelow: 0.6 } }]`, or `probabilityBetween: [0.4, 0.6]` for Boolean.
  - Compound conditions are `any`, `all` and `atLeast`. Missing confidence matches conservatively (`confidence_unavailable`).
  - There is one fallback stage, both stages are billed, and latency is sequential.
  - The feature was previously called "evaluation fallbacks", and the deprecated `x-ai-gateway-evaluation-fallback-*` headers are still sent.
  - The docs say: "Choose thresholds from your own results and risk tolerance."
  - Source: [Vercel Docs: Decision Fallbacks](https://vercel.com/docs/ai-gateway/models-and-providers/decision-fallbacks)

### Inferences
- The pasted description's statement that these articles "cover confidence thresholding and fallback gating" is accurate for the form-router guide (0.95 threshold plus an app-level LLM fallback). For "What is Jev" it is only loosely true: that page discusses threshold choice and gating qualitatively and points elsewhere.
- Two fallback approaches are documented, so the report writer should not conflate them:
  - **(a) App-level:** the form-router template's own `if (confidence >= 0.95)` followed by `generateText` with `openai/gpt-6-luna-fast`.
  - **(b) Gateway-level:** declarative `providerOptions.gateway.models[].when`, with docs examples using `confidenceBelow: 0.6` and `openai/gpt-6-astra`.
- The 0.95 (template) and 0.6 (docs) figures are example starting points, not recommended values. Vercel's guidance is to calibrate on your own labeled data.

### Gaps
- I did not fetch the other KB guides (`typesafe-jev-and-ai-sdk`, `auto-approve-tool-calls-eve-jev`, `moderate-product-reviews-jev-tanstack-ai`), so I have no details on their thresholds or code.
- The fetch tool could not reproduce the form-router guide's code blocks verbatim. The code quoted above comes from the template repo's `main` branch, which the guide cites, and matches the guide's excerpts (`CONFIDENCE_THRESHOLD`, `models.luna ?? "openai/gpt-6-luna-fast"`).

## How does AI Gateway authentication work for third-party models, and does it support the "no standalone key" claim?

### Takeaway
VERIFIED, with one qualification. Through AI Gateway you do **not** need a TypeSafe API key. Requests authenticate with Vercel credentials: an `AI_GATEWAY_API_KEY`, or a Vercel OIDC token that is provided automatically on Vercel deployments. Usage is billed through AI Gateway. BYOK, adding your own TypeSafe key, is optional. You still need a Vercel credential, so "no key at all" would be wrong outside Vercel deployments.

### Cited Findings
- "Every request to AI Gateway requires Vercel authentication. Use an AI Gateway API key or OpenID Connect (OIDC) token. Bring Your Own Key (BYOK) provider credentials control how AI Gateway authenticates to a model provider, but they don't replace request authentication." (docs last updated 2026-09-08) — [Vercel Docs: AI Gateway Authentication and BYOK](https://vercel.com/docs/ai-gateway/authentication-and-byok)
- API keys "never expire unless you revoke them". The AI SDK reads `AI_GATEWAY_API_KEY` automatically when given a plain-string model ID. "Vercel deployments receive an OIDC token as `VERCEL_OIDC_TOKEN`, so you can authenticate without creating an API key", and an explicit API key takes precedence over OIDC. — [Vercel Docs: AI Gateway Authentication and BYOK](https://vercel.com/docs/ai-gateway/authentication-and-byok)
- BYOK is configured at the team level. It adds no markup, and if your credentials fail, requests can retry with system credentials. — [Vercel Docs: AI Gateway Authentication and BYOK](https://vercel.com/docs/ai-gateway/authentication-and-byok)
- On the TypeSafe-compatible API, the API key or OIDC token "is the credential AI Gateway authenticates you with, not the credential used to call the model. To bill the provider directly instead of through AI Gateway, add a TypeSafe key under BYOK." Migrating an existing client means swapping `TYPESAFE_API_KEY` for `AI_GATEWAY_API_KEY` and setting `baseURL: 'https://ai-gateway.vercel.sh/typesafe'`. — [Vercel Docs: TypeSafe API with AI Gateway](https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe)
- "Decision requests work with BYOK. If your team has added a key for the provider, it is used automatically." — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision)
- The form-router guide says AI Gateway authenticates with a Vercel OIDC token when deployed, or with `AI_GATEWAY_API_KEY` locally. — [Vercel KB guide](https://vercel.com/kb/guide/jev-ai-sdk-form-router)
- By contrast, the direct provider `@ai-sdk/typesafe-ai`, without the gateway, needs `TYPESAFE_AI_API_KEY`. — [vercel/ai typesafe-ai provider docs](https://github.com/vercel/ai/blob/main/content/providers/01-ai-sdk-providers/105-typesafe-ai.mdx)
- The Sept 16 changelog adds that Zero Data Retention and No Training can be enabled per request (`providerOptions.gateway.zeroDataRetention`), and that evaluation calls appear in logs, custom reporting and budgets. — [Vercel Changelog, 2026-09-16](https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway)

### Inferences
- "Call Jev without maintaining a direct standalone key" is accurate if it means a TypeSafe key. On Vercel deployments, OIDC means no secret has to be stored at all. Locally or off-Vercel, you still maintain one secret, the AI Gateway key, which covers every gateway model rather than being Jev-specific.
- Secondary sources say TypeSafe's direct API is early-access or waitlisted, which would make the gateway a practical access route. I did not verify this against TypeSafe's own site (it is outside this scope).

### Gaps
- I did not fetch the OIDC subpage (`/docs/ai-gateway/authentication-and-byok/oidc`) for token lifetime or `vercel env pull` details.

## Is there a Vercel changelog or blog post announcing Jev / TypeSafe AI support, and when?

### Takeaway
VERIFIED. There are two changelog entries: **2026-09-16**, "TypeSafe AI's Jev now available on AI Gateway" (AI SDK path), and **2026-09-21**, "AI Gateway now supports TypeSafe clients and an HTTP API for Jev" (TypeSafe-client and HTTP paths). A separate "OpenAI Decisions API now available on AI Gateway" changelog also exists and is linked from the Decision docs.

### Cited Findings
- 2026-09-16, "TypeSafe AI's Jev now available on AI Gateway" (Rohan Taneja, Zachary Chen, Jerilyn Zheng). It describes Jev as "a probabilistic decision model for software" and requires AI SDK 7.0.105 or later. It says "TypeSafe reports" Jev was up to 193.6x faster and 444.6x cheaper than LLMs on its workflow evaluations, a vendor claim with no methodology given. — [Vercel Changelog](https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway)
- 2026-09-21, "AI Gateway now supports TypeSafe clients and an HTTP API for Jev" (Rohan Taneja, Jerilyn Zheng). It covers three paths, a TypeSafe client, the HTTP `POST /v1/evaluate` API and the AI SDK, all billed through AI Gateway. It mentions eve (eve.dev) as an agent framework using Jev as its default evaluation model. — [Vercel Changelog](https://vercel.com/changelog/ai-gateway-now-supports-typesafe-clients-and-an-http-api-for-jev)
- The Decision docs' related links include "OpenAI Decisions API now available on AI Gateway". — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision); [changelog link](https://vercel.com/changelog/openai-decisions-api-now-available-on-ai-gateway)

### Inferences
- The timeline is consistent across primary sources:
  - `@typesafe-ai/sdk` 0.6.0 published 2026-09-15.
  - `ai@7.0.103` with `experimental_evaluate`, and `@ai-sdk/typesafe-ai@3.0.0`, published 2026-09-16, the same day as the launch changelog.
  - TypeSafe-compatible API and HTTP API added 2026-09-21; KB guides and explainers dated 2026-09-19 to 2026-09-22.
  - Rename to `decide` on 2026-10-05.

### Gaps
- I did not fetch the OpenAI Decisions changelog, so I have no date for it.
- I found no Vercel *blog* post, as opposed to changelog entries, about Jev. I did not run a dedicated search of vercel.com/blog.

## Overall verdict on the pasted claims

### Takeaway
Claim (1) is substantially true but imprecise and slightly out of date. Claim (2) is true for both titles: the form-router guide is a `/kb/guide/` page with concrete thresholds, and "What is Jev" is a `/i/` explainer linked from the KB hub that covers thresholds only qualitatively. Nothing in the description looks fabricated, but some of the terminology is garbled.

### Cited Findings
- **Claim 1, "native support for Jev via the AI SDK evaluate provider (typesafe-ai/jev)": PARTIALLY VERIFIED.**
  - Jev on AI Gateway is real (`typesafe-ai/jev`, 2026-09-16). — [Changelog](https://vercel.com/changelog/typesafe-ai-jev-now-available-on-ai-gateway)
  - The AI SDK API is a **function**, `experimental_evaluate`, renamed `experimental_decide` on 2026-10-05, not an "evaluate provider". — [Vercel Docs: Decision](https://vercel.com/docs/ai-gateway/modalities/decision); [vercel/ai CHANGELOG](https://github.com/vercel/ai/blob/main/packages/ai/CHANGELOG.md)
  - A separate provider package, `@ai-sdk/typesafe-ai`, also exists. — [npm](https://registry.npmjs.org/@ai-sdk%2Ftypesafe-ai)
- **Claim 1, "without maintaining a direct standalone key": VERIFIED**, with the qualification that you still need an AI Gateway key or Vercel OIDC token, not a TypeSafe key. — [Vercel Docs: TypeSafe API](https://vercel.com/docs/ai-gateway/sdks-and-apis/typesafe); [Vercel Docs: Auth](https://vercel.com/docs/ai-gateway/authentication-and-byok)
- **Claim 2, "Route form submissions with Jev and AI SDK": VERIFIED.** KB guide dated 2026-09-21; 0.95 confidence floor; fallback to `openai/gpt-6-luna-fast` on low, missing or error. — [Vercel KB guide](https://vercel.com/kb/guide/jev-ai-sdk-form-router)
- **Claim 2, "What is Jev, TypeSafe AI's System One model?": VERIFIED as existing** (2026-09-19, at `/i/what-is-jev`, linked from the KB hub). **PARTIALLY VERIFIED as covering thresholding and fallback gating**: it covers them qualitatively, with no numbers and no fallback model. — [Vercel: What is Jev](https://vercel.com/i/what-is-jev)

### Inferences
- The description was probably produced by summarising the real Vercel pages. The errors are the kind a summariser makes: "evaluate provider" in place of "evaluate function", and describing both articles as KB articles when one sits at an `/i/` URL. Nothing fabricated was introduced.

### Gaps
- I did not check whether the URLs a report writer might quote will remain stable. The `experimental_` rename shows the naming can change in patch releases, so code snippets should be re-checked before publication.
