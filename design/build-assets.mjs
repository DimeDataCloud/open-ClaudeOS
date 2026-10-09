#!/usr/bin/env node
// Renders the package icons (and the README banner mark) from design/assets/spark.svg with the
// Chromium that ships with Playwright. Output goes to the Shell's Assets folder.
import { chromium } from "playwright";
import { readFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const svg = readFileSync(join(here, "assets", "spark.svg"), "utf8");
const out = join(here, "..", "windows", "src", "ClaudeOS.Shell", "Assets");
mkdirSync(out, { recursive: true });

// name, width, height, mark size as a fraction of the shorter side, background ("transparent" or a colour)
const assets = [
  ["Square44x44Logo.scale-200.png", 88, 88, 0.96, "transparent"],
  ["Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24, 1, "transparent"],
  ["Square150x150Logo.scale-200.png", 300, 300, 0.72, "#FAF9F7"],
  ["Wide310x150Logo.scale-200.png", 620, 300, 0.72, "#FAF9F7"],
  ["SplashScreen.scale-200.png", 1240, 600, 0.38, "#FAF9F7"],
  ["StoreLogo.png", 100, 100, 0.9, "transparent"],
  ["LockScreenLogo.scale-200.png", 48, 48, 1, "transparent"],
];

const browser = await chromium.launch();
for (const [name, w, h, frac, bg] of assets) {
  const size = Math.round(Math.min(w, h) * frac);
  const page = await browser.newPage({ viewport: { width: w, height: h } });
  await page.setContent(`<body style="margin:0;width:${w}px;height:${h}px;background:${bg};display:grid;place-items:center">${svg.replace('width="128" height="128"', `width="${size}" height="${size}"`)}</body>`);
  await page.screenshot({ path: join(out, name), omitBackground: bg === "transparent" });
  await page.close();
}
await browser.close();
console.log(`wrote ${assets.length} assets to ${out}`);
