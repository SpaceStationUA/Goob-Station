process.env.TUI_IFACE = "GalaxyMap";
const { chromium } = await import("playwright-core");
const { createServer } = await import("vite");
const port = 5387;
const vite = await createServer({ server: { host: "127.0.0.1", port, strictPort: true }, logLevel: "error" });
await vite.listen();
const b = await chromium.launch({ headless: true, channel: "chrome" });
const p = await (await b.newContext({ viewport: { width: 1500, height: 700 }, deviceScaleFactor: 2 })).newPage();
p.on("pageerror", (e) => console.log("[pageerror]", e.message));
await p.goto(`http://127.0.0.1:${port}/`, { waitUntil: "load" });
await p.waitForSelector("svg.chart");
await p.waitForTimeout(500);
await p.evaluate(() => document.querySelector(".spikebar button").click());
await p.waitForTimeout(300);
const rows = JSON.parse(process.argv[2]);
await p.evaluate((rows) => {
  document.body.innerHTML = `<div id="flat" style="background:#0d1220;padding:14px;display:flex;flex-wrap:wrap;gap:16px"></div>`;
  const box = document.getElementById("flat");
  for (const r of rows) {
    for (const dpr of [2, 1]) {
      const c = document.createElement("div"); c.style.textAlign = "center";
      const im = document.createElement("img");
      im.src = window.__galaxyStill({ seed: r.seed, type: r.type, px: 190, dpr });
      im.style.cssText = "image-rendering:pixelated;width:190px;height:190px";
      c.appendChild(im);
      const l = document.createElement("div");
      l.textContent = `${r.type} ${r.seed} dpr${dpr}`;
      l.style.cssText = "font:10px ui-monospace;color:#8aa";
      c.appendChild(l); box.appendChild(c);
    }
  }
}, rows);
await p.waitForTimeout(2500);
await p.locator("#flat").screenshot({ path: "/tmp/planets.png" });
console.log("ok");
await b.close(); await vite.close();
