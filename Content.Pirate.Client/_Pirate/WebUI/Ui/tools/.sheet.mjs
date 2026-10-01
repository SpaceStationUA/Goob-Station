// Render a grid of planet types. usage: node sheet.mjs <out.png> <spec...>
// spec = type:px:count   e.g.  lava:200:3
process.env.TUI_IFACE = "GalaxyMap";
const { chromium } = await import("playwright-core");
const { createServer } = await import("vite");
const out = process.argv[2];
const specs = process.argv.slice(3).map((a) => {
  const [type, px, count] = a.split(":");
  return { type, px: +px || 200, count: +count || 1 };
});
const port = 5300 + Math.floor(Math.random() * 90);
const vite = await createServer({ server: { host: "127.0.0.1", port, strictPort: true }, logLevel: "error" });
await vite.listen();
const b = await chromium.launch({ headless: true, channel: "chrome" });
const p = await (await b.newContext({ viewport: { width: 1760, height: 1100 }, deviceScaleFactor: 2 })).newPage();
p.on("pageerror", (e) => console.log("[pageerror]", e.message));
await p.goto(`http://127.0.0.1:${port}/`, { waitUntil: "load" });
await p.waitForSelector("svg.chart");
await p.waitForTimeout(500);
await p.evaluate(() => document.querySelector(".spikebar button").click());
await p.waitForTimeout(300);
const timing = await p.evaluate((specs) => {
  const out = [];
  let s = 0x5eed1;
  for (const sp of specs) {
    for (let k = 0; k < sp.count; k++) {
      const t0 = performance.now();
      window.__galaxyStill({ seed: (s += 4444), type: sp.type, px: sp.px, dpr: 2 });
      out.push(Math.round(performance.now() - t0));
    }
  }
  return out;
}, specs);
await p.evaluate((specs) => {
  document.body.innerHTML = `<div id="flat" style="background:#080d16;padding:14px;font:10px ui-monospace;color:#8aa;display:flex;flex-wrap:wrap;gap:12px"></div>`;
  const box = document.getElementById("flat");
  let s = 0x5eed1;
  for (const sp of specs) {
    for (let k = 0; k < sp.count; k++) {
      const c = document.createElement("div");
      c.style.textAlign = "center";
      const im = document.createElement("img");
      im.src = window.__galaxyStill({ seed: (s += 4444), type: sp.type, px: sp.px, dpr: 2 });
      im.style.cssText = `image-rendering:pixelated;width:${sp.px}px;height:${sp.px}px`;
      c.appendChild(im);
      const l = document.createElement("div");
      l.textContent = `${sp.type} ${sp.px}`;
      c.appendChild(l);
      box.appendChild(c);
    }
  }
}, specs);
await p.waitForTimeout(1500 + timing.reduce((a, b) => a + b, 0));
await p.locator("#flat").screenshot({ path: out });
console.log("render ms per still:", timing.join(","));
await b.close();
await vite.close();
