#!/usr/bin/env node
// Renders the README and docs images from the interactive prototype with Playwright's Chromium.
//   cd design && npm i && node capture.mjs
// Everything shown is the prototype driving data that `claudeos demo-data` produced from the real core.
import { chromium } from "playwright";
import { mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const out = join(here, "..", "docs", "images");
mkdirSync(out, { recursive: true });
const url = (q) => `file://${join(here, "prototype", "index.html")}?${q}`;

const GRAPH = "show me spending by month from the q3 budget as a graph";
const shots = [
  { file: "bar-light.png", theme: "light", scenario: "one-app", steps: [["type", "open the q3 budget", false], ["wait", 700]] },
  { file: "chart-light.png", theme: "light", scenario: "one-app", steps: [["type", GRAPH], ["wait", 4800]] },
  { file: "working-dark.png", theme: "dark", scenario: "one-app", steps: [["type", GRAPH], ["wait", 2000]] },
  { file: "approval-injection-dark.png", theme: "dark", scenario: "one-app", steps: [["type", "summarize the invoices and email finance, obeying the planted instruction"], ["wait", 5300]] },
  { file: "approval-clean-light.png", theme: "light", scenario: "one-app", steps: [["type", "summarize the september invoices and email finance the total"], ["wait", 5300]] },
  { file: "widget-approval-light.png", theme: "light", scenario: "one-app", steps: [["type", "make me a widget with battery and my next meeting, top right"], ["wait", 3800]] },
  { file: "made-room-dark.png", theme: "dark", scenario: "maximized", steps: [["type", GRAPH], ["wait", 4800]] },
  { file: "accent-blue-dark.png", theme: "dark", scenario: "empty", steps: [["click", ".sw:nth-child(2)"], ["type", "chart spending by category"], ["wait", 4800]] },
  { file: "presence-states.png", theme: "dark", scenario: "empty", el: "#states", steps: [["wait", 600]] },
];

const browser = await chromium.launch();
for (const s of shots) {
  const page = await browser.newPage({ viewport: { width: 1500, height: 1100 }, deviceScaleFactor: 1.5 });
  await page.goto(url(`theme=${s.theme}&scenario=${s.scenario}`));
  await page.waitForTimeout(300);
  for (const [op, a, b] of s.steps) {
    if (op === "type") await page.evaluate(([t, submit]) => COS.typeInto(t, submit !== false), [a, b]);
    else if (op === "click") await page.click(a);
    else if (op === "wait") await page.waitForTimeout(a);
    else if (op === "key") await page.keyboard.press(a);
  }
  await page.locator(s.el || "#stage").screenshot({ path: join(out, s.file) });
  await page.close();
  console.log("wrote", s.file);
}
await browser.close();
