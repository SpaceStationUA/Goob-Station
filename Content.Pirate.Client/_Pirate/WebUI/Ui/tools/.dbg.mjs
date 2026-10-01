process.env.TUI_IFACE = "GalaxyMap";
const { chromium } = await import("playwright-core");
const { createServer } = await import("vite");
const port = 5386;
const vite = await createServer({ server: { host: "127.0.0.1", port, strictPort: true }, logLevel: "error" });
await vite.listen();
const b = await chromium.launch({ headless: true, channel: "chrome" });
const p = await (await b.newContext()).newPage();
p.on("pageerror", (e) => console.log("[pageerror]", e.message));
await p.goto(`http://127.0.0.1:${port}/`, { waitUntil: "load" });
await p.waitForSelector("svg.chart");
await p.waitForTimeout(400);
await p.evaluate(() => document.querySelector(".spikebar button").click());
await p.waitForTimeout(250);
console.log(await p.evaluate(async () => {
  const out = {};
  for (const [type, seed] of [["gas", 1992559903], ["gas", 388817]]) {
    const im = new Image();
    im.src = window.__galaxyStill({ seed, type, px: 128, dpr: 1 });
    await im.decode();
    const c = document.createElement("canvas"); c.width = im.width; c.height = im.height;
    const cx = c.getContext("2d"); cx.drawImage(im, 0, 0);
    const d = cx.getImageData(0, 0, im.width, im.height).data;
    const h = new Map();
    for (let i = 0; i < d.length; i += 4) {
      if (d[i+3] === 0) continue;
      const k = `${d[i]},${d[i+1]},${d[i+2]}`;
      h.set(k, (h.get(k) || 0) + 1);
    }
    const top = [...h.entries()].sort((a,b)=>b[1]-a[1]).slice(0, 6)
      .map(([k,v]) => `${k} ${Math.round(100*v/(d.length/4))}%`);
    out[`${type} ${seed}`] = { distinct: h.size, top: top.join("  |  ") };
  }
  return JSON.stringify(out, null, 1);
}));
await b.close(); await vite.close();
