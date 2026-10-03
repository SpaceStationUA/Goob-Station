/**
 * DOM-level checks for the galaxy chart.
 *
 * `npm run check` covers geometry, data and the source contract. None of it can
 * see the view layer, and that is where the worst bug lived: every marker was
 * frozen at the pixel position it got for the initial 1200x700 default, because
 * Solid runs a <For>/<Show> child body inside untrack() and the transform had
 * been read into a local there. Every geometry check passed, the chart looked
 * correct at exactly one window size, and resizing or browser-zooming made the
 * stars drift out of their countries. `getScreenCTM()` compounded it, since that
 * matrix includes the document zoom.
 *
 * So these assertions drive a real browser and compare what the page DREW
 * against what it should have drawn. That catches the whole family: frozen
 * geometry, mis-scaled pointer mapping, and highlight lag.
 *
 * Browser zoom is emulated the way Cmd+/- actually behaves — by changing the
 * size of the CSS viewport. Setting deviceScaleFactor does NOT do this; it only
 * changes DPR, which is why an earlier attempt at reproducing the zoom report
 * saw three identical results.
 *
 * Needs `npm i -D playwright-core` and a browser. Skips cleanly without them.
 */
process.env.TUI_IFACE = "GalaxyMap";

let chromium;
try {
  ({ chromium } = await import("playwright-core"));
} catch {
  console.log("\ncheck:dom skipped — playwright-core not installed (npm i -D playwright-core)\n");
  process.exit(0);
}

const { createServer } = await import("vite");

const PORT = 5199;

const vite = await createServer({
  // Bind explicitly. Vite's default host resolves `localhost`, which on a
  // dual-stack machine is ::1 while Playwright dials 127.0.0.1.
  server: { host: "127.0.0.1", port: PORT, strictPort: true },
  logLevel: "error",
});
await vite.listen();
const BASE = vite.resolvedUrls?.local?.[0] ?? `http://127.0.0.1:${PORT}`;
console.log(`check:dom — serving ${BASE}`);

let failures = 0;
function check(name, ok, detail = "") {
  console.log(`  ${ok ? "ok  " : "FAIL"}  ${name}${detail ? ` — ${detail}` : ""}`);
  if (!ok) failures++;
}

/**
 * Resolve a browser, degrading to a skip rather than an error.
 *
 * Playwright's bundled Chromium is keyed to the driver version, so a machine
 * whose browser cache came from a different release has a download the driver
 * will not accept. Falling back to the system Chrome covers that, and skipping
 * covers a machine with neither — a check that cannot run should say so, not
 * take the whole suite down.
 */
async function launchBrowser() {
  const env = process.env.PLAYWRIGHT_CHROMIUM_EXECUTABLE;
  const attempts = [env ? { executablePath: env } : {}, { channel: "chrome" }, { channel: "msedge" }];
  for (const opts of attempts) {
    try {
      return await chromium.launch({ headless: true, ...opts });
    } catch {
      /* try the next one */
    }
  }
  console.log(
    "\ncheck:dom skipped — no usable browser. Run `npx playwright install chromium`,\n" +
      "or set PLAYWRIGHT_CHROMIUM_EXECUTABLE to a Chrome/Chromium binary.\n",
  );
  process.exit(0);
}

/**
 * Turn the toolbar's PLANETS toggle on or off, and wait for it to take.
 *
 * This used to be a side effect of opening `Spike`: its panel carried a PLANETS ON
 * MAP checkbox that the harness flipped once and never put back. With the panel
 * deleted the toggle is a real button, and the marker checks need it pressed
 * explicitly — the chart draws dots by default, so without this every world-marker
 * check measures zero and passes vacuously or fails for the wrong reason.
 *
 * Reads the button's own `on` class rather than tracking a boolean, so calling this
 * twice with the same argument is a no-op instead of a second toggle.
 */
async function setPlanets(pg, on) {
  // `pg` rather than a captured `page`: the zoom loop makes one page per viewport
  // and there is no module-level page to close over.
  await pg.evaluate(async (want) => {
    const btns = [...document.querySelectorAll(".toolbar > button")];
    const b = btns.find((x) => /^PLANETS|^ПЛАНЕТИ/.test(x.textContent ?? ""));
    if (!b) throw new Error("no PLANETS button in the toolbar");
    if (b.classList.contains("on") !== want) b.click();
    // The markers are generated after the signal lands, so wait for one to appear
    // rather than for a fixed delay.
    const wantMarks = want ? "[data-turning], .planet-mark" : null;
    if (wantMarks) {
      const t0 = Date.now();
      while (Date.now() - t0 < 4000 && !document.querySelector(wantMarks)) {
        await new Promise((ok) => setTimeout(ok, 40));
      }
    }
  }, on);
  await pg.waitForTimeout(150);
}

/** Sizes a browser zoom of each percentage produces, in CSS pixels. */
const ZOOMS = [
  { label: "100%", w: 1600, h: 1000 },
  { label: "125%", w: 1280, h: 800 },
  { label: "80%", w: 2000, h: 1250 },
];

const browser = await launchBrowser();

try {
  for (const zoom of ZOOMS) {
    const ctx = await browser.newContext({ viewport: { width: zoom.w, height: zoom.h } });
    const page = await ctx.newPage();
    const errors = [];
    page.on("pageerror", e => errors.push(e.message));
    await page.goto(BASE, { waitUntil: "load" });
    await page.waitForSelector("svg.chart");
    await page.waitForTimeout(250);

    console.log(`\nzoom ${zoom.label} (${zoom.w}x${zoom.h} css px):`);

    // --- markers track the live transform, not a stale one ------------------
    // Capitals are the only circles drawn at r=10, and their stroke is the owner
    // territory's colour, which gives us both a handle on each marker and the
    // identity of the nation it belongs to.
    const marks = await page.evaluate(() => {
      const svg = document.querySelector("svg.chart");
      const w = +svg.getAttribute("width");
      const h = +svg.getAttribute("height");
      const pad = 56;
      const scale = Math.min((w - pad * 2) / 132, (h - pad * 2) / 74);
      const toPx = (lx, ly) => ({ x: lx * scale + w / 2, y: ly * scale + h / 2 });

      const rings = [...svg.querySelectorAll("circle")]
        .filter(c => c.getAttribute("r") === "10")
        .map(c => ({
          x: +c.getAttribute("cx"),
          y: +c.getAttribute("cy"),
          owner: c.getAttribute("stroke"),
        }));

      // Expected on-screen position of every capital, from the model data the
      // page was given. Sol is a capital at (-2, 11) LY.
      const sol = toPx(-2, 11);
      let solErr = Infinity;
      for (const r of rings) solErr = Math.min(solErr, Math.hypot(r.x - sol.x, r.y - sol.y));

      // For each ring, which flat fill paths contain its centre?
      const flats = [...svg.querySelectorAll('path[fill-rule="evenodd"]')].filter(
        p => !(p.getAttribute("fill") ?? "").startsWith("url("),
      );
      const contained = rings.map(r => {
        const hits = flats.filter(p => {
          try {
            return p.isPointInFill(new DOMPoint(r.x, r.y));
          } catch {
            return false;
          }
        });
        return { owner: r.owner, insideOwnTerritory: hits.some(p => p.getAttribute("fill") === r.owner) };
      });

      return { scale, solErr, rings: rings.length, contained };
    });

    check(
      "star markers track the live transform",
      marks.solErr < 2,
      `Sol off by ${marks.solErr.toFixed(1)}px across ${marks.rings} capitals`,
    );

    const strays = marks.contained.filter(c => !c.insideOwnTerritory);
    check(
      "every capital is drawn inside its own territory",
      strays.length === 0,
      strays.length ? `${strays.length} outside: ${strays.map(s => s.owner).join(", ")}` : `${marks.contained.length} checked`,
    );

    // --- the highlight follows the pointer ----------------------------------
    const seen = new Set();
    for (const [fx, fy] of [
      [0.5, 0.5],
      [0.3, 0.4],
      [0.7, 0.6],
      [0.4, 0.7],
    ]) {
      await page.mouse.move(zoom.w * fx, zoom.h * fy);
      await page.waitForTimeout(40);
      const at = await page.evaluate(() => {
        const h = document.querySelector(".cell-hover");
        if (!h || h.classList.contains("off")) return null;
        const b = h.getBoundingClientRect();
        return `${(b.x + b.width / 2).toFixed(1)},${(b.y + b.height / 2).toFixed(1)}`;
      });
      if (at) seen.add(at);
    }
    check("hover highlight moves with the pointer", seen.size >= 3, `${seen.size} distinct cells`);

    await page.mouse.move(zoom.w * 0.45, zoom.h * 0.55);
    await page.waitForTimeout(60);
    const under = await page.evaluate(() => {
      const h = document.querySelector(".cell-hover");
      const b = h.getBoundingClientRect();
      return { cx: b.x + b.width / 2, cy: b.y + b.height / 2 };
    });
    const hexR = 2 * marks.scale;
    const off = Math.hypot(under.cx - zoom.w * 0.45, under.cy - zoom.h * 0.55);
    check(
      "hover highlight is the cell under the cursor",
      off < hexR,
      `off by ${off.toFixed(1)}px, hex radius ${hexR.toFixed(1)}px`,
    );

    // --- brushes: nation, unclaim, contest ---------------------------------
    await page.mouse.click(zoom.w * 0.45, zoom.h * 0.55);
    await page.waitForTimeout(120);
    const panel = await page.evaluate(() => document.querySelector(".panel")?.textContent ?? null);
    check("clicking a cell selects its territory", panel !== null, panel?.slice(0, 32));

    const armed = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[0].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("a nation brush arms and says what it will do", /PAINTING/i.test(armed), armed.slice(0, 34));

    const svgBox = await page.evaluate(() => {
      const s = document.querySelector("svg.chart");
      const r = s.getBoundingClientRect();
      return { x: r.x, y: r.y, w: r.width, h: r.height };
    });
    await page.mouse.click(svgBox.x + svgBox.w * 0.45, svgBox.y + svgBox.h * 0.55);
    await page.waitForTimeout(150);
    const undo = await page.evaluate(() =>
      [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
    );
    check("painting a cell records an undo step", undo);

    const contest = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[swatches.length - 2].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("contest brush arms", /CONTESTED/i.test(contest), contest.slice(0, 34));

    const uncontest = await page.evaluate(async () => {
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      swatches[swatches.length - 1].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return {
        note: document.querySelector(".armed-note")?.textContent ?? "",
        // The two dispute brushes must be distinguishable, or arming one while
        // the other is held silently re-arms the wrong behaviour.
        onlyOneArmed: document.querySelectorAll(".toolbar .swatch.on").length,
      };
    });
    check(
      "uncontest brush arms and is distinct from contest",
      /CONTESTED/i.test(uncontest.note) && uncontest.onlyOneArmed === 1,
      `${uncontest.note.slice(0, 30)}, ${uncontest.onlyOneArmed} armed`,
    );

    // Contesting then un-contesting the same cell must be a round trip.
    const roundTrip = await page.evaluate(async () => {
      const layer = () =>
        (document.querySelector(".contested-layer path")?.getAttribute("d") ?? "").split("M")
          .length - 1;
      const swatches = [...document.querySelectorAll(".toolbar .swatch")];
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const at = { x: r.x + r.width * 0.42, y: r.y + r.height * 0.5 };
      const click = () =>
        svg.dispatchEvent(
          new MouseEvent("click", { bubbles: true, clientX: at.x, clientY: at.y }),
        );
      const wait = () => new Promise(ok => setTimeout(ok, 200));

      swatches[swatches.length - 2].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await wait();
      const before = layer();
      click();
      await wait();
      const afterMark = layer();
      swatches[swatches.length - 1].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await wait();
      click();
      await wait();
      const afterClear = layer();
      return { before, afterMark, afterClear, note: (document.querySelector(".armed-note")?.textContent ?? "(none)").slice(0, 24) };
    });
    check(
      "contesting a cell adds it and un-contesting removes it",
      roundTrip.afterMark === roundTrip.before + 1 && roundTrip.afterClear === roundTrip.before,
      `${roundTrip.before} -> ${roundTrip.afterMark} -> ${roundTrip.afterClear} [${roundTrip.note}]`,
    );

    // --- drag to paint ------------------------------------------------------
    const undoCount = () =>
      page.evaluate(() => {
        const b = [...document.querySelectorAll(".toolbar button")].find(x =>
          x.textContent.includes("UNDO"),
        );
        return b ? Number(b.textContent.replace(/\D+/g, "")) || 0 : 0;
      });

    // Put the brush down first, then pick the nation one. Clicking an already
    // armed swatch disarms it — that is what a toggle is for — so arming
    // "only if empty" would leave whatever the previous step happened to be
    // holding, and a drag with the wrong brush is correctly a no-op.
    const armedNation = await page.evaluate(async () => {
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      const sw = [...document.querySelectorAll(".toolbar .swatch")];
      sw[0].dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 80));
      return document.querySelector(".armed-note")?.textContent ?? "";
    });
    check("a nation brush is armed before dragging", /PAINTING/i.test(armedNation), armedNation.slice(0, 30));

    // One drag: press, a few moves, release. Then assert the pending preview
    // covered MORE cells than there were move events — that is the observable
    // signature of line interpolation, and its absence is what makes a fast
    // drag come out dashed.
    const MOVES = 4;
    const editsBefore = await undoCount();
    const drag = await page.evaluate(async (moves) => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const fire = (type, fx, fy, button) =>
        svg.dispatchEvent(
          new MouseEvent(type, {
            bubbles: true,
            button: button ?? 0,
            clientX: r.x + r.width * fx,
            clientY: r.y + r.height * fy,
          }),
        );
      const pendingCells = () => {
        const d = document.querySelector(".stroke-pending path")?.getAttribute("d") ?? "";
        return (d.match(/M/g) ?? []).length;
      };

      fire("mousedown", 0.30, 0.30, 0);
      await wait(30);
      const afterPress = pendingCells();
      for (let i = 1; i <= moves; i++) {
        fire("mousemove", 0.30 + 0.03 * i, 0.30 + 0.014 * i);
        await wait(30);
      }
      const beforeRelease = pendingCells();
      fire("mouseup", 0.30 + 0.03 * moves, 0.30 + 0.014 * moves);
      await wait(200);
      return { afterPress, beforeRelease, afterRelease: pendingCells() };
    }, MOVES);

    check("pressing starts a stroke on the cell under the pointer", drag.afterPress === 1, `${drag.afterPress} cells`);
    check(
      "a drag covers more cells than it has move events — no gaps",
      drag.beforeRelease > MOVES,
      `${drag.beforeRelease} cells from ${MOVES} moves`,
    );
    check("the preview is cleared on release", drag.afterRelease === 0, `${drag.afterRelease} cells`);

    const editsAfter = await undoCount();
    check(
      "a whole drag is ONE undo step, not one per cell",
      editsAfter === editsBefore + 1,
      `UNDO ${editsBefore} -> ${editsAfter}`,
    );

    // Escape mid-drag must abandon the stroke rather than commit half of it.
    const cancelled = await page.evaluate(async () => {
      const wait = (ms = 220) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const undo = () => {
        const b = [...document.querySelectorAll(".toolbar button")].find(x =>
          x.textContent.includes("UNDO"),
        );
        return b ? Number(b.textContent.replace(/\D+/g, "")) || 0 : 0;
      };
      const fire = (type, fx, fy) =>
        svg.dispatchEvent(
          new MouseEvent(type, {
            bubbles: true,
            button: 0,
            clientX: r.x + r.width * fx,
            clientY: r.y + r.height * fy,
          }),
        );
      const before = undo();
      fire("mousedown", 0.6, 0.3, 0);
      await wait(30);
      for (let i = 1; i <= 3; i++) {
        fire("mousemove", 0.6 - 0.02 * i, 0.3 + 0.01 * i);
        await wait(30);
      }
      window.dispatchEvent(new KeyboardEvent("keydown", { key: "Escape", bubbles: true }));
      await wait(250);
      return { before, after: undo(), preview: (document.querySelector(".stroke-pending path")?.getAttribute("d") ?? "").length };
    });
    check(
      "escape abandons an in-progress stroke",
      cancelled.after === cancelled.before && cancelled.preview === 0,
      `UNDO ${cancelled.before} -> ${cancelled.after}`,
    );

    // --- admin vs player ----------------------------------------------------
    check("an admin still sees the brushes", await page.evaluate(() => !!document.querySelector(".paintpick")));

    // A player gets the same chart with the tools removed. Driven by flipping
    // the source's permission, because that is the one input the real build
    // varies: the shipped binary is identical for everyone and only the payload
    // differs.
    const player = await page.evaluate(async () => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      window.__galaxySetPermission?.(false);
      await wait();
      const out = {
        supported: typeof window.__galaxySetPermission === "function",
        paint: !!document.querySelector(".paintpick"),
        undo: [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
        // The chart itself must survive: a player still reads the map.
        stars: document.querySelectorAll("circle.system-label, text.system-label").length,
        labels: document.querySelectorAll(".terr-name").length,
        grid: !!document.querySelector(".toolbar button"),
      };
      return out;
    });
    check("the page can be told it is read-only", player.supported);
    check("a player gets no paint tools", !player.paint);
    check("a player gets no undo", !player.undo);
    check("a player still sees the place names", player.stars > 0, `${player.stars} labels`);
    check("a player still sees the territory names", player.labels > 0, `${player.labels} names`);
    check("a player keeps the grid toggle", player.grid);

    // And a click on a cell must still select, not silently do nothing.
    const playerClick = await page.evaluate(async () => {
      const wait = (ms = 200) => new Promise(ok => setTimeout(ok, ms));
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      svg.dispatchEvent(
        new MouseEvent("click", {
          bubbles: true,
          clientX: r.x + r.width * 0.45,
          clientY: r.y + r.height * 0.55,
        }),
      );
      await wait();
      return document.querySelector(".panel")?.textContent?.slice(0, 24) ?? null;
    });
    check("a player can still select a territory", playerClick !== null, playerClick ?? "no panel");

    await page.evaluate(async () => {
      window.__galaxySetPermission?.(true);
      await new Promise(r => setTimeout(r, 150));
    });

    // --- planet animation ---------------------------------------------------
    // Two properties have to hold, and the second is the one that cannot be seen
    // in a still: consecutive frames must differ, and a full turn must land
    // exactly back on the start.
    //
    // The obvious refinement — scaling the shift by cos(latitude), so the poles
    // hold still and the equator sweeps fastest — is NOT a rigid rotation. A
    // mid-latitude pixel advances by cos(lat) of a texture period instead of a
    // whole one, so the sequence drifts and never returns. An earlier version of
    // this check compared the last frame against the first, which passed with the
    // bug in place: those two are one step apart in the sequence either way, so
    // the metric could not see the drift. The direct property is that a full turn
    // reproduces the start pixel for pixel.
    const spin = await page.evaluate(async () => {
      const N = 12;
      const sheet = window.__galaxySheet({ seed: 0x5eed1, type: "terran", px: 48, dpr: 1 }, N);

      const img = new Image();
      img.src = sheet.uri;
      await img.decode();
      const cv = document.createElement("canvas");
      cv.width = img.width;
      cv.height = img.height;
      const cx = cv.getContext("2d");
      cx.drawImage(img, 0, 0);
      const d = sheet.framePx;
      const frame = (n) => cx.getImageData(n * d, 0, d, d).data;

      const decode = async (uri) => {
        const im = new Image();
        im.src = uri;
        await im.decode();
        const c2 = document.createElement("canvas");
        c2.width = im.width;
        c2.height = im.height;
        c2.getContext("2d").drawImage(im, 0, 0);
        return c2.getContext("2d").getImageData(0, 0, im.width, im.height).data;
      };
      const opts = { seed: 0x5eed1, type: "terran", px: 48, dpr: 1 };
      const start = await decode(window.__galaxyStill({ ...opts, spin: 0 }));
      const full = await decode(window.__galaxyStill({ ...opts, spin: 1 }));
      const half = await decode(window.__galaxyStill({ ...opts, spin: 0.5 }));

      const maxDiff = (a, b) => {
        let m = 0;
        for (let i = 0; i < a.length; i += 4) {
          m = Math.max(m, Math.abs(a[i] - b[i]), Math.abs(a[i + 1] - b[i + 1]), Math.abs(a[i + 2] - b[i + 2]));
        }
        return m;
      };
      const f0 = frame(0);
      const f1 = frame(1);
      let changed = 0;
      for (let i = 0; i < f0.length; i += 4) {
        if (Math.abs(f0[i] - f1[i]) > 8) changed++;
      }
      return {
        frames: N,
        size: [img.width, img.height],
        step: changed / (d * d),
        closure: maxDiff(start, full),
        halfTurn: maxDiff(start, half),
      };
    });

    check(
      "the sheet is a horizontal strip of frames",
      spin.size[0] === spin.size[1] * spin.frames,
      `${spin.size[0]}x${spin.size[1]} for ${spin.frames} frames`,
    );
    check(
      "consecutive frames differ, so it actually rotates",
      spin.step > 0.005,
      `${(spin.step * 100).toFixed(1)}% of pixels change per frame`,
    );
    check(
      "half a turn is visibly different from the start",
      spin.halfTurn > 24,
      `max channel delta ${spin.halfTurn}`,
    );
    check(
      "a full turn lands exactly back on the start, so the loop closes",
      spin.closure === 0,
      `max channel delta ${spin.closure} after 360 degrees`,
    );

    // --- the system overlay ------------------------------------------------
    // Clicking a star is the whole reason the overlay exists, so the things that
    // could quietly break it are pinned down here: which system opens, how big the
    // world is drawn, and that a marker click does not leak into the territory
    // panel underneath.
    //
    // The state arriving from the brushes block has a brush armed, which matters
    // twice over: an armed brush suppresses the marker hit targets entirely (so
    // the element these checks look for would not exist), and it takes priority
    // over a marker click. Both are asserted below rather than worked around.
    await page.keyboard.press("Escape");
    await page.waitForTimeout(120);
    check(
      "escape clears the brush, so markers are clickable again",
      (await page.evaluate(() => document.querySelector(".armed-note")?.textContent ?? "")) === "",
    );

    // Which marker, and where. A capital, so the ring and the widest hit radius
    // are both in play.
    const star = await page.evaluate(() => {
      for (const h of document.querySelectorAll(".sys-hit")) {
        if (document.querySelector(`text.system-label.capital[data-sys="${h.dataset.sys}"]`)) {
          const r = h.getBoundingClientRect();
          const own = document.querySelector(`text.system-label[data-sys="${h.dataset.sys}"]`);
          return {
            id: h.dataset.sys,
            name: own.textContent.trim(),
            x: r.x + r.width / 2,
            y: r.y + r.height / 2,
            r: +h.getAttribute("r"),
          };
        }
      }
      return null;
    });
    check("capitals expose a click target", star !== null, star?.name ?? "none found");

    await page.mouse.click(star.x, star.y);
    await page.waitForTimeout(200);
    const opened = await page.evaluate(() => ({
      overlay: document.querySelector(".overlay") !== null,
      title: document.querySelector(".overlay h2")?.textContent ?? "",
      territoryPanel: document.querySelector(".panel:not(.overlay)") !== null,
    }));
    check("clicking a star opens the overlay for that system", opened.overlay && opened.title === star.name,
      `${opened.title} (wanted ${star.name})`);
    check(
      "the territory panel is not also open — one panel at a time",
      !opened.territoryPanel,
    );

    // The point of the overlay. A map marker is 16-40px; anything near that here
    // would mean the overlay had quietly become the thing it replaced.
    const art = await page.evaluate(() => {
      const w = document.querySelector(".overlay .world");
      const img = document.querySelector(".overlay .world-still");
      if (!w) return null;
      const r = w.getBoundingClientRect();
      return {
        css: r.width,
        natural: img ? img.naturalWidth : 0,
        dpr: window.devicePixelRatio || 1,
        panel: document.querySelector(".overlay").getBoundingClientRect().width,
      };
    });
    check(
      "the overlay draws its world far larger than a map marker",
      art && art.css >= 150,
      art ? `${art.css}px css, ${art.natural}px generated, panel ${Math.round(art.panel)}px` : "no art",
    );
    check(
      "the world fits inside the panel",
      art && art.css <= art.panel,
      art ? `${art.css} vs ${Math.round(art.panel)}` : "",
    );
    // Against css * dpr, not against css. `natural >= css` passes trivially at
    // dpr 1, which is exactly where this suite runs — it would have gone green
    // against a sprite generated at half the display resolution, which is the
    // bug it exists to catch.
    check(
      "the still is generated at the display ratio, not upscaled",
      art && art.natural === Math.round(art.css * art.dpr),
      art ? `${art.natural}px generated for ${art.css} css @ dpr ${art.dpr}` : "",
    );

    // The close button, before anything else needs a closed panel.
    await page.click(".overlay-close");
    await page.waitForTimeout(150);
    check(
      "the close button dismisses the overlay",
      (await page.evaluate(() => document.querySelector(".overlay") === null)),
    );

    // The deferral. Generating 24 frames costs about half a second, so the panel
    // shows a finished still first and upgrades. If someone makes the strip
    // synchronous this goes red, which is the point: the regression is a frozen
    // page on click, and nothing else would notice it.
    //
    // Measured on a system this suite has NOT already opened. That is not a
    // detail: `planetSheet` caches, so re-opening the star above is a cache hit
    // and both the still and the strip land in the same frame. The property
    // being asserted is about a cold generate, so it has to use a cold system —
    // otherwise the check passes for the wrong reason and would not notice the
    // strip being made synchronous.
    const cold = await page.evaluate((used) => {
      for (const h of document.querySelectorAll(".sys-hit")) {
        if (h.dataset.sys !== used) return h.dataset.sys;
      }
      return null;
    }, star.id);
    check("found an unopened system to time", cold !== null, cold ?? "");
    // The close-button check above already left the panel shut, which is the
    // state this needs: the overlay toggles, so timing an open panel would time
    // the close. page.evaluate cannot close over `cold`, so it is passed in.
    const defer = await page.evaluate(async (sysId) => {
      const marks = [];
      const t0 = performance.now();
      const obs = new MutationObserver(() => {
        if (document.querySelector(".world-still") && !marks.some(m => m.k === "still"))
          marks.push({ k: "still", t: performance.now() - t0 });
        if (document.querySelector(".world.turning") && !marks.some(m => m.k === "strip"))
          marks.push({ k: "strip", t: performance.now() - t0 });
      });
      obs.observe(document.body, { childList: true, subtree: true, attributes: true });
      const h = document.querySelector('.sys-hit[data-sys="' + sysId + '"]');
      const r = h.getBoundingClientRect();
      // A real click on the svg, so the timed path is the one a player takes and
      // not a synthetic call into the handler.
      document.querySelector("svg.chart").dispatchEvent(
        new MouseEvent("click", {
          bubbles: true,
          clientX: r.x + r.width / 2,
          clientY: r.y + r.height / 2,
        }),
      );
      // Poll rather than sample once. The strip is now built across macrotasks —
      // 96 frames at 200px is ~3.1s of pixel loop — so a single fixed wait can land
      // either side of completion depending on machine speed, and "the strip never
      // arrived" is not a thing that can be concluded from one sample.
      for (let i = 0; i < 60; i++) {
        if (document.querySelector(".overlay .world-turn")) break;
        await new Promise((res) => setTimeout(res, 250));
      }
      obs.disconnect();
      return marks;
    }, cold);
    const still = defer.find(m => m.k === "still");
    const strip = defer.find(m => m.k === "strip");
    check(
      "a world is on screen immediately, before any rotation is generated",
      still !== undefined,
      still ? `${Math.round(still.t)}ms` : "no still",
    );
    check(
      "the rotation is deferred rather than blocking the click",
      still !== undefined && strip !== undefined && strip.t > still.t + 50,
      still && strip ? `still ${Math.round(still.t)}ms, strip ${Math.round(strip.t)}ms` : "strip never arrived",
    );

    // Toggle-to-close, on whichever system is actually open. Clicking the
    // original capital here would be a different test: that one switches systems,
    // because the overlay moved to `cold` for the timing above.
    const openId = await page.evaluate((sysId) => {
      const h = document.querySelector('.sys-hit[data-sys="' + sysId + '"]');
      const t = document.querySelector(".overlay h2")?.textContent ?? "";
      if (!h) return null;
      const r = h.getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2, name: t };
    }, cold);
    check("the overlay is open on the system that was timed", openId !== null, openId?.name ?? "overlay not open on the timed system");
    if (openId) {
      await page.mouse.click(openId.x, openId.y);
      await page.waitForTimeout(150);
      check(
        "clicking the open system again closes the overlay",
        (await page.evaluate(() => document.querySelector(".overlay") === null)),
      );
    }

    /**
     * Which half of a ring is drawn in front.
     *
     * This is a check on the emitted geometry, not on `ringHalf`, because the
     * defect was never in the function: `above` means the UPPER arc and the near
     * half is the LOWER one, and the component passed `front` where it wanted
     * `back`. A unit test of `ringHalf` on its own passes happily through that.
     *
     * So: measure the paths. The ring's SVGs are centred on the planet, so in the
     * path data the near half sits at positive y (SVG y runs down) and the far
     * half at negative y.
     */
    /**
     * The overlay's ring, measured off its own pixels.
     *
     * This used to measure the two SVG halves' bounding boxes and assert one was
     * above the planet and one below. That is the right property for the SVG ring and
     * it is not this ring any more — the canvas cuts the planet's hole itself, so
     * there are no halves to find. Asserting on the markup would have kept passing
     * while measuring nothing at all.
     *
     * So: read the canvas. The properties that matter are all visible in its pixels.
     * Where the planet is, the ring's upper half must be EMPTY. Where the planet's
     * face is, the ring's lower half must be COVERED. And the ring must reach
     * further out than the planet, or it is a collar.
     *
     * The first of those is the one worth having. It is exactly the bug this was
     * found with — the far half drawing over the planet — and no check on the DOM
     * could see it, because the DOM said the ring was behind the image and the
     * image was static so it painted underneath.
     */
    const overlayRing = await page.evaluate(async () => {
      const open = document.querySelector(".sys-hit[data-sys='burzsia']");
      if (!open) return { canvas: false, detail: "burzsia did not open" };
      const r = open.getBoundingClientRect();
      document
        .querySelector("svg.chart")
        .dispatchEvent(
          new MouseEvent("click", {
            bubbles: true,
            clientX: r.x + r.width / 2,
            clientY: r.y + r.height / 2,
          }),
        );
      for (let i = 0; i < 40 && !document.querySelector(".overlay"); i++) {
        await new Promise((res) => setTimeout(res, 50));
      }
      await new Promise((res) => setTimeout(res, 400));

      const c = document.querySelector(".world-ring-gl");
      const host = document.querySelector(".world-ring-gl-host");
      const planet = document.querySelector(".world-still");
      const panel = document.querySelector(".overlay");
      if (!c || !host || !planet || !panel) {
        document.querySelector(".overlay-close")?.click();
        return {
          canvas: false,
          detail: c ? "canvas present but no host/planet/panel" : "no canvas ring in the overlay",
        };
      }

      // readPixels on a WebGL canvas is not dependable here, so go through the
      // compositor: data URL, decode, then a 2D canvas.
      const im = new Image();
      im.src = c.toDataURL();
      await im.decode();
      const off = document.createElement("canvas");
      off.width = c.width;
      off.height = c.height;
      const ctx = off.getContext("2d");
      ctx.drawImage(im, 0, 0);
      const data = ctx.getImageData(0, 0, off.width, off.height).data;

      const mid = off.width / 2;
      // A ring pixel is warm or plum: never neutral, and never the panel's own
      // near-black. The panel behind the canvas shows through where the ring is cut.
      const lit = (x, y) => {
        const i = ((Math.round(y) * off.width) + Math.round(x)) * 4;
        const R = data[i];
        const G = data[i + 1];
        const B = data[i + 2];
        return R + G + B > 90;
      };

      // The planet's radius, in canvas pixels, from the ring host's own geometry.
      // The shader cuts its hole at exactly this radius, so it is also the number
      // every assertion below is about.
      const hole = Number(host.dataset.ringHole);

      /**
       * The invariant, stated the way the shader states it.
       *
       * Inside the planet's disc the ring must be ABSENT above the planet's centre and
       * PRESENT below it. That is `if (uv.y < 0.5)` made observable, and it is the
       * whole difference between a ring passing round a planet and a hoop with a
       * bite taken out of one side.
       *
       * Counting inside the disc rather than scanning outward for the ring's edge is
       * what makes this robust. A 6:1 ellipse rotated 40 degrees crosses the vertical
       * centre line at about 55px, well inside the planet's 100px radius, so there is
       * nothing to find on that line outside the hole at all and a "first lit row"
       * probe returns nothing. This does not care where the ring crosses.
       */
      /**
       * Rotate a canvas pixel back into the ring's own frame, the way the shader
       * does, so "which side is in front" is asked in the frame the shader answers
       * in. `rotate` subtracts the centre, applies the matrix, adds it back — and in
       * the vertex shader's y-down space that is a clockwise turn on screen.
       */
      const rot = Number(host.dataset.ringRotation);
      const cos = Math.cos(rot);
      const sin = Math.sin(rot);

      /**
       * The shader's own \`rotated.y\`, MEASURED rather than derived.
       *
       * Deriving it is a trap. The shader writes
       *
       *     coord *= mat2(vec2(cos, -sin), vec2(sin, cos))
       *
       * and the two arguments are the matrix's COLUMNS, so working out what \`v * m\`
       * does with that by reading gives one answer; getting the row/column convention
       * wrong yields a check that is confidently 90 degrees out. It was wrong here,
       * and the symptom was 247 lit pixels on the "far" side of a boundary that was
       * in the wrong place rather than a shader bug.
       *
       * So it is measured. Histogramming the rotated-y of every lit pixel inside the
       * planet's disc puts them all on one side of \`x * cos + y * sin\` and none on
       * the other, which is the definition. That is the expression below.
       */
      const rotatedY = (x, y) => {
        const dx = (x - mid) / off.width;
        // Row 0 of a readback is the visual top, which is where v_uv.y is 0.
        const dy = (y - mid) / off.height;
        return dx * cos + dy * sin;
      };

      let wrongSide = 0;   // lit where the ring should be cut for the planet
      let rightSide = 0;   // lit where it should cross in front
      let behindLit = 0;   // lit on the far side, which must be nothing
      for (let y = 0; y < off.height; y += 2) {
        for (let x = 0; x < off.width; x += 2) {
          const dx = x - mid;
          const dy = y - mid;
          if (dx * dx + dy * dy >= hole * hole) continue;
          // The reference's own test, on its own rotated uv: `uv.y < 0.5`, which is
          // `rotated.y < 0` since the rotation is about the uv centre.
          const behind = rotatedY(x, y) < 0;
          const isLit = lit(x, y);
          if (behind) {
            if (isLit) behindLit++;
          } else if (isLit) rightSide++;
        }
      }
      wrongSide = behindLit;
      insideTotal = behindLit + rightSide;

      const outer = Number(host.dataset.ringOuter);
      const spread = outer / Number(host.dataset.ringHole);
      const cb = c.getBoundingClientRect();
      const pb = panel.getBoundingClientRect();
      const fits = cb.left >= pb.left - 1 && cb.right <= pb.right + 1;

      document.querySelector(".overlay-close")?.click();
      return {
        canvas: true,
        detail: `${c.width}x${c.height} backing, ${Math.round(cb.width)} css`,
        // A small tolerance, because the cut is a hard threshold and the sampling
        // grid straddles it: a pixel sitting on the boundary counts as lit or not
        // depending on which side of it the sample falls. Asserting exactly zero
        // would make this fail on rounding. Five per cent of the near side is far
        // below anything a real occlusion bug could produce — that one leaves
        // roughly half the disc covered.
        holeClear: rightSide > 0 && behindLit <= rightSide * 0.05,
        holeDetail:
          `${behindLit} ring pixels inside the planet's disc on the far side against ` +
          `${rightSide} on the near side (${(100 * behindLit / Math.max(1, rightSide)).toFixed(1)}% leak)`,
        nearPresent: rightSide > 0,
        nearDetail:
          `${rightSide} ring pixels cross in front of the planet's face, ${behindLit} behind it`,
        spread,
        room: (pb.width / 2) / Number(host.dataset.ringHole),
        fits,
        fitDetail: `ring ${Math.round(cb.width)}px in a ${Math.round(pb.width)}px panel`,
      };
    });

    /**
     * The COMPOSITED page, which is the only thing a viewer sees.
     *
     * Everything above reads the ring canvas's own pixels, and that is a real gap:
     * putting the canvas back behind the planet sprite leaves every one of those
     * numbers unchanged, because the shader still cuts the hole and still draws the
     * near arm — the sprite just paints over the result. All of them pass. That was
     * not hypothetical: it is the arrangement this started in, and it renders as two
     * stubs either side of a planet instead of a ring round one.
     *
     * So this reads the screenshot. The discriminator has to be something the planet
     * cannot produce, and there is one: the planet's palette is cream, tan and brown,
     * all of which have green above blue, while the ring's shadowed tones are plum
     * and violet, which have blue above green. `b > g` therefore holds for the ring
     * and never for the planet, which makes it safe to look for the ring lying across
     * the planet's face.
     */
    /**
     * Make sure the overlay is CLOSED, then open it, then wait — each step bounded.
     *
     * The first version clicked the system and waited for the canvas unconditionally.
     * The probe above leaves the overlay open, so that click was a toggle: it closed
     * the panel, and the wait then sat on a canvas that was never coming, until the
     * whole suite hit its timeout with no message. A hang is the worst way for a
     * check to fail, because it looks like the machine rather than the code.
     *
     * So: settle the state first, and give up with a recorded failure rather than
     * throwing, so one broken expectation cannot take the other 300 down with it.
     */
    await page.evaluate(async () => {
      const close = () => document.querySelector(".overlay-close");
      for (let i = 0; i < 60 && close(); i++) {
        close().click();
        await new Promise((ok) => setTimeout(ok, 50));
      }
    });
    await page.evaluate(() => {
      const open = document.querySelector(".sys-hit[data-sys='burzsia']");
      const r = open.getBoundingClientRect();
      document
        .querySelector("svg.chart")
        .dispatchEvent(
          new MouseEvent("click", {
            bubbles: true,
            clientX: r.x + r.width / 2,
            clientY: r.y + r.height / 2,
          }),
        );
    });
    let gotCanvas = true;
    try {
      await page.waitForSelector(".world-ring-gl", { timeout: 8000 });
    } catch {
      gotCanvas = false;
    }
    await page.waitForTimeout(500);

    const ringClip = await page.evaluate(() => {
      const planet = document.querySelector(".world-still").getBoundingClientRect();
      const host = document.querySelector(".world-ring-gl-host");
      // A box around the planet, so the sample is the planet's face and a little
      // more. Anything outside the disc is not the thing being asserted.
      const pad = 4;
      return {
        x: Math.round(planet.left - pad),
        y: Math.round(planet.top - pad),
        width: Math.round(planet.width + pad * 2),
        height: Math.round(planet.height + pad * 2),
        hole: Number(host.dataset.ringHole),
      };
    });
    const ringShot = await page.screenshot({ clip: ringClip });
    const composed = gotCanvas
      ? await page.evaluate(
        async ([b64, size]) => {
          const im = new Image();
          im.src = "data:image/png;base64," + b64;
          await im.decode();
          const cv = document.createElement("canvas");
          cv.width = im.width;
          cv.height = im.height;
          const c2 = cv.getContext("2d");
          c2.drawImage(im, 0, 0);
          const d = c2.getImageData(0, 0, cv.width, cv.height).data;
          const mid = cv.width / 2;
          let plum = 0;
          let inside = 0;
          for (let y = 0; y < cv.height; y++) {
            for (let x = 0; x < cv.width; x++) {
              const dx = (x - mid) * 2;
              const dy = y - mid;
              if (dx * dx + dy * dy >= size.hole * size.hole) continue;
              inside++;
              const i = (y * cv.width + x) * 4;
              if (d[i + 2] > d[i + 1] + 6) plum++;
            }
          }
          return { plum, inside, total: cv.width * cv.height };
        },
          [ringShot.toString("base64"), { hole: ringClip.hole }],
        )
      : { plum: 0, inside: 0, total: 0 };
    await page.evaluate(() => document.querySelector(".overlay-close")?.click());

    /**
     * The nebula behind the chart.
     *
     * Worth checking at all because it is easy to add a decoration that renders
     * nothing and looks fine: the page has a dark background, so a nebula that
     * failed to compile, or compiled and drew 80% transparency, is very close to
     * invisible in a screenshot. Measured off its own pixels instead.
     */
    const neb = await page.evaluate(async () => {
      const c = document.querySelector(".nebula-canvas");
      if (!c) return { present: false, detail: "no .nebula-canvas in the DOM" };
      const im = new Image();
      im.src = c.toDataURL();
      await im.decode();
      const off = document.createElement("canvas");
      off.width = c.width;
      off.height = c.height;
      const ctx = off.getContext("2d");
      ctx.drawImage(im, 0, 0);
      const d = ctx.getImageData(0, 0, off.width, off.height).data;
      let covered = 0;
      // Every covered pixel's luminance, so the spread can be measured. Collecting
      // them all costs a sort over a couple of hundred thousand numbers once per
      // zoom level, which is nothing next to a screenshot.
      const lums = [];
      const total = d.length / 4;
      for (let i = 0; i < d.length; i += 4) {
        if (d[i + 3] < 128) continue;
        covered++;
        lums.push((d[i] + d[i + 1] + d[i + 2]) / 3);
      }
      lums.sort((a2, b2) => a2 - b2);
      const q = (f) => lums[Math.min(lums.length - 1, Math.floor(lums.length * f))] ?? 0;
      const p05 = q(0.05);
      const p95 = q(0.95);
      const p50 = q(0.5);
      const p99 = q(0.99);
      // Spread between the dimmest and brightest fifth of the covered pixels.
      //
      // This replaces an absolute luminance threshold, which was measuring the
      // PALETTE rather than the picture: it asked for pixels above 45, which the warm
      // ramp has and the cool default does not, so a correctly-rendered nebula in a
      // deliberately dark ramp reported as "a flat fill". The property worth
      // asserting is that the gas has TONES -- that it is not one colour painted over
      // another -- and spread says that for any ramp, warm or cool, bright or dim.
      //
      // Measured p50 -> p99 rather than p05 -> p95, and the reason is that most
      // covered pixels are the FLAT FILL: the shader paints background_color wherever
      // col_value falls under the cutoff, and in a dark ramp that is most of the
      // canvas. Including it in the spread measures the fill, not the gas. p50 upward
      // asks the right question -- is there structure ABOVE the floor.
      const spread = p99 - p50;
      const maxL = lums[lums.length - 1] ?? 0;
      // Distinct colours, as a proxy for there being more than one layer present.
      const seen = new Set();
      for (let i = 0; i < d.length; i += 4 * 97) {
        if (d[i + 3] < 128) continue;
        seen.add(`${d[i] >> 4},${d[i + 1] >> 4},${d[i + 2] >> 4}`);
      }
      return {
        present: true,
        coveredPct: (100 * covered) / total,
        spread,
        p05,
        p50,
        p95,
        p99,
        maxLum: maxL,
        distinct: seen.size,
        backing: `${c.width}x${c.height}`,
        detail: `${c.width}x${c.height} backing`,
      };
    });
    check("the nebula is on the page at all", neb.present, neb.detail ?? "missing");
    check(
      "and it covers a real part of the chart",
      neb.present && neb.coveredPct > 8 && neb.coveredPct < 70,
      `${(neb.coveredPct ?? 0).toFixed(1)}% of the canvas is covered`,
    );
    check(
      "and it has TONES in it, rather than one flat colour over another",
      neb.present && neb.spread > 12,
      `luminance rises ${(neb.spread ?? 0).toFixed(0)} from the median covered pixel ` +
        `(${Math.round(neb.p50 ?? 0)}) to the 99th percentile (${Math.round(neb.p99 ?? 0)}), ` +
        `peak ${Math.round(neb.maxLum ?? 0)}. A spread rather than a threshold, and measured ` +
        `from the median up, so it holds for a dim ramp and ignores the flat fill.`,
    );
    check(
      "with more than one tone in it, so both layers are drawing",
      neb.present && neb.distinct >= 4,
      `${neb.distinct} distinct colours`,
    );
    /**
     * The backdrop must not take pointer events.
     *
     * The first version of this asked for `elementFromPoint` at the centre of the
     * viewport and it failed — not because the backdrop was capturing anything, but
     * because the chart's own box does not reach the exact centre and the point
     * landed on margin, where the backdrop is the topmost thing. A layout-dependent
     * probe for a property that is declared in CSS is the wrong kind of test.
     *
     * So this asks the question directly. `pointer-events: none` on the host is
     * inherited by the canvas, but the canvas is inserted from script after the
     * stylesheet is applied, so it is worth checking rather than assuming.
     */
    /**
     * Is any of it actually ON SCREEN?
     *
     * Every check above reads the nebula canvas. That is not the same question, and
     * it is not a pedantic one: the canvas rendered 54% coverage at peak luminance
     * 182 and contributed a maximum per-pixel delta of 2 to the finished page,
     * because the chart drew an OPAQUE radial gradient over the whole SVG. It was
     * fully hidden.
     *
     * And it stayed hidden through a screenshot review, because the chart already
     * had two hand-rolled radial gradients behind it labelled `nebulaA` and
     * `nebulaB` -- one blue, one purple. Those are what I saw, described as gas in
     * the corners, and wrote up as working. The tell was the only one available: the
     * gas I described was in a colour the new palette does not contain.
     *
     * So this measures the page with the backdrop and without it, and requires a real
     * difference. It is the only assertion here that would have caught that, and it
     * is negative-controlled by putting the opaque background back.
     */
    const shotWith = await page.screenshot();
    // `visibility: hidden`, NOT removing the element and NOT reloading.
    //
    // Both of those were tried. Removing the backdrop is fine in itself, but it left
    // the page without it for every check that follows, and reloading to put it back
    // threw away the armed brush and the paint history that the next three checks
    // depend on -- so a check about a background decoration broke three unrelated
    // ones. Hiding it and putting it back touches nothing else.
    await page.evaluate(() => {
      document.querySelector(".nebula-backdrop").style.visibility = "hidden";
    });
    await page.waitForTimeout(150);
    const shotWithout = await page.screenshot();
    await page.evaluate(() => {
      document.querySelector(".nebula-backdrop").style.visibility = "";
    });
    const onScreen = await page.evaluate(
      async ([x, y]) => {
        const load = async (s) => {
          const i = new Image();
          i.src = "data:image/png;base64," + s;
          await i.decode();
          const c = document.createElement("canvas");
          c.width = i.width;
          c.height = i.height;
          const g = c.getContext("2d");
          g.drawImage(i, 0, 0);
          return g.getImageData(0, 0, c.width, c.height).data;
        };
        const A = await load(x);
        const B = await load(y);
        let changed = 0;
        let sum = 0;
        let max = 0;
        for (let i = 0; i < A.length; i += 4) {
          const d =
            (Math.abs(A[i] - B[i]) + Math.abs(A[i + 1] - B[i + 1]) + Math.abs(A[i + 2] - B[i + 2])) / 3;
          if (d > 4) {
            changed++;
            sum += d;
          }
          if (d > max) max = d;
        }
        return {
          pct: (100 * changed) / (A.length / 4),
          mean: sum / Math.max(1, changed),
          max: Math.round(max),
        };
      },
      [shotWith.toString("base64"), shotWithout.toString("base64")],
    );
    // Coverage and peak, not mean. These thresholds were first set against the warm
    // ramp, where the nebula is bright enough that mean does the job -- and the cool
    // default then failed them while being perfectly visible, because it is
    // deliberately dimmer. Mean is the wrong statistic anyway: a nebula that covered
    // a little of the page strongly beats one that covered a lot of it faintly, and
    // coverage times peak is what "can you see it" actually means.
    //
    // The separation from the opaque-background failure is wide: 1.5% and peak 15
    // when it is hidden, against 15% and peak 39 when it is not.
    check(
      "and it is actually ON SCREEN, not just correct inside its own canvas",
      onScreen.pct > 8 && onScreen.max > 25,
      `removing the backdrop changes ${onScreen.pct.toFixed(1)}% of the page, peak ${onScreen.max}. ` +
        `An opaque background rect over the SVG is what drops this to 1.5%.`,
    );

    // The palette picker: three ramps, and clicking must actually change the picture.
    const palettes = await page.evaluate(() => ({
      count: document.querySelectorAll(".sky-swatch").length,
      active: document.querySelector(".nebula-backdrop")?.dataset.palette ?? "",
    }));
    check(
      "the sky picker offers a ramp per palette",
      palettes.count === 3,
      `${palettes.count} swatches, ${palettes.active} active`,
    );
    // Cycle once and require the backdrop to report a different ramp.
    const before = palettes.active;
    await page.click(".sky-pick");
    await page.waitForTimeout(900);
    const after = await page.evaluate(
      () => document.querySelector(".nebula-backdrop")?.dataset.palette ?? "",
    );
    check(
      "and clicking it changes the ramp the backdrop reports",
      after !== "" && after !== before,
      `${before} -> ${after}`,
    );
    await page.click(".sky-pick");
    await page.click(".sky-pick");
    await page.waitForTimeout(900);

    const pe = await page.evaluate(() => {
      const host = document.querySelector(".nebula-backdrop");
      const cv = document.querySelector(".nebula-canvas");
      if (!host) return { ok: false, why: "no host" };
      const h = getComputedStyle(host).pointerEvents;
      const c = cv ? getComputedStyle(cv).pointerEvents : "(no canvas)";
      return { ok: h === "none" && c === "none", why: `host ${h}, canvas ${c}` };
    });
    check("and it does not swallow clicks meant for the chart", pe.ok, pe.why);

    check(
      "the overlay's ring is a live canvas, not the SVG fallback",
      overlayRing.canvas,
      overlayRing.detail,
    );
    check(
      "and it is cut for the planet, so the far half passes behind it",
      overlayRing.holeClear,
      overlayRing.holeDetail,
    );
    check(
      "while the near half is drawn over the planet's face",
      overlayRing.nearPresent,
      overlayRing.nearDetail,
    );
    check(
      "the ring reaches well past the planet's edge, so it is a ring and not a collar",
      overlayRing.spread > 1.6,
      `outer radius is ${overlayRing.spread.toFixed(2)}x the planet's, panel allows ${overlayRing.room.toFixed(2)}x`,
    );
    check(
      "and it fits the panel it is drawn in",
      overlayRing.fits,
      overlayRing.fitDetail,
    );
    check(
      "and the ring is actually visible ACROSS the planet, not just in its own canvas",
      composed.plum > composed.inside * 0.01,
      `${composed.plum} of ${composed.inside} pixels inside the planet's disc are ring-coloured ` +
        `(blue above green, which no planet tone does); the near arm has to reach the face`,
    );


    // Open space must still select the territory underneath. This is the
    // regression a marker hit target invites: widen the radius far enough and
    // clicks that used to select a country start opening a planet instead.
    const openSpace = await page.evaluate(() => {
      const svg = document.querySelector("svg.chart");
      const r = svg.getBoundingClientRect();
      const hits = [...document.querySelectorAll(".sys-hit")].map(h => {
        const b = h.getBoundingClientRect();
        return { x: b.x + b.width / 2, y: b.y + b.height / 2, r: +h.getAttribute("r") };
      });
      // A grid of candidates; take the first that is clear of every hit radius.
      for (let fy = 0.2; fy <= 0.8; fy += 0.05) {
        for (let fx = 0.2; fx <= 0.8; fx += 0.05) {
          const x = r.x + r.width * fx;
          const y = r.y + r.height * fy;
          if (hits.every(h => Math.hypot(h.x - x, h.y - y) > h.r + 12))
            return { x, y, fx, fy };
        }
      }
      return null;
    });
    check("found open space well clear of every marker", openSpace !== null);
    if (openSpace) {
      await page.mouse.click(openSpace.x, openSpace.y);
      await page.waitForTimeout(150);
      const terr = await page.evaluate(() => ({
        panel: document.querySelector(".panel:not(.overlay)") !== null,
        overlay: document.querySelector(".overlay") !== null,
      }));
      check(
        "clicking open space still selects the territory, and opens no overlay",
        terr.panel && !terr.overlay,
      );
    }

    // An armed brush wins over a marker. An admin dragging a stroke across a
    // capital must not have that capital swallow the click and open a panel
    // mid-drag.
    await page.evaluate(() => {
      document.querySelector(".toolbar .swatch").dispatchEvent(new MouseEvent("click", { bubbles: true }));
    });
    await page.waitForTimeout(120);
    await page.mouse.click(star.x, star.y);
    await page.waitForTimeout(180);
    const brushWins = await page.evaluate(() => ({
      overlay: document.querySelector(".overlay") !== null,
      undo: [...document.querySelectorAll(".toolbar button")].some(b => b.textContent.includes("UNDO")),
    }));
    check(
      "with a brush armed, a marker click paints instead of opening the overlay",
      !brushWins.overlay && brushWins.undo,
      `overlay ${brushWins.overlay}, undo ${brushWins.undo}`,
    );
    await page.keyboard.press("Escape");
    await page.waitForTimeout(120);

    // --- how a world is drawn ---------------------------------------------
    // The banding is the look. Both of these guard properties that a change
    // could quietly remove while every screenshot still looked plausible.
    const look = await page.evaluate(async () => {
      const decode = async (uri) => {
        const im = new Image();
        im.src = uri;
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        return { data: cx.getImageData(0, 0, im.width, im.height), w: im.width, h: im.height };
      };
      const opts = { seed: 0x5eed1, px: 128, dpr: 1 };
      const world = await decode(window.__galaxyStill({ ...opts, type: "terran" }));
      const star = await decode(window.__galaxyStill({ ...opts, type: "star" }));

      // Distinct opaque colours. A smooth-shaded sphere produces tens of
      // thousands of them; a dithered banded one produces a small palette plus
      // one dither pixel per blend.
      const palette = (img) => {
        const set = new Set();
        for (let i = 0; i < img.data.data.length; i += 4) {
          if (img.data.data[i + 3] > 0)
            set.add((img.data.data[i] << 16) | (img.data.data[i + 1] << 8) | img.data.data[i + 2]);
        }
        return set.size;
      };
      // Corner transparency: a planet is exactly its disc, a star bleeds.
      // Two different probes, because the two claims are about different radii.
      //
      // A planet's disc has radius d/2 and a star's has radius d/3 with its
      // corona reaching d/2, so the only place that is outside one and inside the
      // other is the annulus between d/3 and d/2 — 6% in from the top edge. The
      // box CORNER is at d/sqrt(2), outside everything including the corona, so
      // it is transparent for every type and proves nothing about either claim.
      const cornerAlpha = (img) => img.data.data[3];
      const annulusAlpha = (img) => {
        const i = (Math.round(img.h * 0.06) * img.w + Math.round(img.w / 2)) * 4;
        return img.data.data[i + 3];
      };
      const centreAlpha = (img) => {
        const i = ((img.h / 2) * img.w + img.w / 2) * 4;
        return img.data.data[i + 3];
      };
      return {
        px: world.w * world.h,
        worldColours: palette(world),
        starColours: palette(star),
        worldCorner: cornerAlpha(world),
        starEdge: annulusAlpha(star),
        worldEdge: annulusAlpha(world),
        worldCentre: centreAlpha(world),
        starCentre: centreAlpha(star),
      };
    });

    /**
     * Craters.
     *
     * The obvious check — count dark blobs on the lit side — does not work, and
     * two attempts are worth recording. Counting pixels darker than a smoothed
     * copy of themselves gave barren 208 cratered versus 208 uncratered, because
     * at the time a 128px sprite drew no craters at all; the frequency curve
     * clamped to two cells across the whole sphere and the product field never
     * crossed the bowl threshold. After fixing that, the ratio only reached 1.1,
     * and on lava it went BELOW 1.0 — a crater darkens a dark patch less than it
     * darkens a bright one, so "how many dark pixels" is not a function of "how
     * many craters".
     *
     * So this measures the difference between the two renders directly, and the
     * control is exact rather than statistical: a world with an atmosphere has no
     * craters to suppress, so suppressing them must change NOTHING, byte for
     * byte. If the flag were ignored, or the cache key missed it, or the renderer
     * were nondeterministic, that assertion fails — and it is the assertion that
     * makes the other two mean anything.
     */
    const crat = await page.evaluate(async () => {
      const px = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, px: 128, dpr: 1, ...opts });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        return cx.getImageData(0, 0, im.width, im.height).data;
      };
      const lum = (d, i) => 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
      const compare = async (type) => {
        const on = await px({ type });
        const off = await px({ type, suppressCraters: true });
        let changed = 0;
        let darker = 0;
        let opaque = 0;
        for (let i = 0; i < on.length; i += 4) {
          if (on[i + 3] > 0) opaque++;
          if (on[i] === off[i] && on[i + 1] === off[i + 1] && on[i + 2] === off[i + 2]) continue;
          changed++;
          if (lum(on, i) < lum(off, i) - 4) darker++;
        }
        return { changed, darker, opaque, total: on.length / 4 };
      };
      return {
        barren: await compare("barren"),
        lava: await compare("lava"),
        terran: await compare("terran"),
      };
    });

    /**
     * Ice world melt lakes, and specifically that they FRAGMENT the water.
     *
     * This asserts on water AREA, and the choice was made by measurement rather
     * than by taste. Counting connected bodies was the obvious thing to check and
     * it is not reliable: across five seeds the body count went 9/7, 7/8, 17/11,
     * 38/17, 76/6 — and on one seed it went DOWN, because where the main field
     * already has water the ponds merge into it instead of adding to the count.
     * Water area is unambiguous in every case: 20x, 200x, 1.3x, 2.7x and 27x, so
     * the threshold is set at 1.2x, which the weakest seed still clears.
     *
     * So the fragmentation claim — which is the visually interesting one — is NOT
     * asserted. It is real and visible; it is just not a property that holds
     * across seeds, and a check that fails on a legitimate seed is worse than no
     * check.
     *
     * The water test is a luminance threshold, which only works on the LIT side,
     * so the region is restricted to the upper left where the light is. The
     * terminator drags shaded ice down below the water's own luminance, and a
     * threshold applied to the whole disc would therefore count the night half as
     * a lake.
     */
    const lake = await page.evaluate(async () => {
      const bodies = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type: "ice", px: 128, dpr: 1, ...opts });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        // Ice tones are all above 190 in luminance; water is below 175. Nothing
        // sits between, which is what makes a threshold usable at all here.
        const wet = new Uint8Array(w * h);
        let wetPx = 0;
        for (let y = 0; y < Math.round(h * 0.5); y++) {
          for (let x = 0; x < Math.round(w * 0.6); x++) {
            const i = (y * w + x) * 4;
            if (d[i + 3] === 0) continue;
            const l = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
            if (l < 185) {
              wet[y * w + x] = 1;
              wetPx++;
            }
          }
        }
        // Flood fill, 4-connected.
        let n = 0;
        const stack = [];
        for (let k = 0; k < wet.length; k++) {
          if (!wet[k]) continue;
          n++;
          stack.push(k);
          wet[k] = 0;
          while (stack.length) {
            const p = stack.pop();
            const x = p % w;
            const y = (p - x) / w;
            if (x > 0 && wet[p - 1]) (wet[p - 1] = 0), stack.push(p - 1);
            if (x < w - 1 && wet[p + 1]) (wet[p + 1] = 0), stack.push(p + 1);
            if (y > 0 && wet[p - w]) (wet[p - w] = 0), stack.push(p - w);
            if (y < h - 1 && wet[p + w]) (wet[p + w] = 0), stack.push(p + w);
          }
        }
        return { bodies: n, wetPx };
      };
      const px = async (opts) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type: "terran", px: 128, dpr: 1, ...opts });
        await im.decode();
        return im.src;
      };
      const on = await bodies({});
      const off = await bodies({ suppressLakes: true });
      return {
        on: on.bodies,
        off: off.bodies,
        wetOn: on.wetPx,
        wetOff: off.wetPx,
        // The control: a world with no lakes to suppress must be untouched.
        terranSame: (await px({})) === (await px({ suppressLakes: true })),
      };
    });

    /**
     * The overlay's rotation, for a world AND for the black hole.
     *
     * **Visibility, not presence.** The black hole's strip was animating perfectly —
     * `background-position` advancing, `playState: running` — on an element sitting at
     * `opacity: 0`, because it was mounted under `class="blackhole"` while the
     * stylesheet only reveals a strip under `.world.turning`. Every signal said it
     * worked. The only thing that showed it did not was reading the computed opacity.
     * So this asserts the strip is *seen*, which is a different question from whether
     * it exists or is scheduled.
     *
     * **Speed, because "animating" is not the same as "reads as turning".** At 48
     * seconds a turn over 24 frames each frame held for two seconds: technically
     * running, visually a slideshow. The threshold is one second of hold, on the
     * grounds that somebody watching to see whether a thing turns gives it about a
     * second. That is a taste number, and it is written down as one.
     */
    const overlaySpin = await page.evaluate(async () => {
      const open = async (id) => {
        const hit = document.querySelector(`.sys-hit[data-sys="${id}"]`);
        if (!hit) return null;
        const r = hit.getBoundingClientRect();
        hit.dispatchEvent(
          new MouseEvent("click", {
            bubbles: true,
            clientX: r.x + r.width / 2,
            clientY: r.y + r.height / 2,
          }),
        );
        // Long enough for the deferred strip. 96 frames at 200px is ~3.1s of pixel
        // loop spread across macrotasks, and a shorter wait here reports "no strip at
        // all" — which is what it did at 2.2s.
        await new Promise((r2) => setTimeout(r2, 7000));
        const el = document.querySelector(".overlay .world-turn");
        const live = document.querySelector(".overlay .world-gl canvas");
        // The close below has to happen on EVERY path out of this function.
        //
        // It used to sit at the end, after the early return for "no strip", so a
        // system with no strip left its overlay open — and the next system's click
        // then landed on that overlay instead of the chart, so it never opened and
        // reported no strip either. One absence produced two, and the second was
        // pure noise. It stayed hidden because every system had a strip until the
        // black hole went live, at which point the first absence was real and the
        // cascade was not.
        if (!el && !live) {
          document.querySelector(".overlay-close")?.dispatchEvent(
            new MouseEvent("click", { bubbles: true }),
          );
          await new Promise((r2) => setTimeout(r2, 200));
          return { id, noStrip: true };
        }
        if (!el) {
          // Live path. Read the canvas back rather than trusting that a canvas
          // element exists: an element with a context that never drew is the same
          // failure as a strip with no background image, which is the one this
          // whole block exists to catch.
          const c = live;
          const gl = c.getContext("webgl2") || c.getContext("webgl");
          /**
           * Does the canvas contain a disc, or is the baked still underneath just
           * showing through?
           *
           * A transparent canvas differs from frame to frame and every assertion
           * about motion passes, while the picture a viewer sees comes entirely from
           * the fallback. That is not a hypothetical: it is what happened for a
           * stretch of work on the disc's shape, where two materially different
           * shaders produced byte-identical output and the reason was that neither
           * of them was drawing anything. Frame-to-frame difference cannot see it,
           * because the animated pixels are the ring and nothing else.
           *
           * So read the canvas's own pixels and count warm ones, and separately
           * confirm it is not simply the still redrawn.
           */
          const warm = await (async () => {
            const url = c.toDataURL();
            const im = new Image();
            im.src = url;
            await im.decode();
            const off = document.createElement("canvas");
            off.width = c.width;
            off.height = c.height;
            const ctx = off.getContext("2d");
            ctx.drawImage(im, 0, 0);
            const px = ctx.getImageData(0, 0, off.width, off.height).data;
            let n = 0;
            for (let i = 0; i < px.length; i += 4) {
              // warm and not the near-white photon ring: the disc's own palette
              if (px[i] > 70 && px[i] > px[i + 2] * 1.6 && px[i + 2] < 150) n++;
            }
            return { warm: n, total: px.length / 4, url };
          })();
          // Read BEFORE closing. The overlay is torn down by the close, so asking
          // afterwards about what was inside it reports nothing for the same
          // reason asking about a closed overlay's canvas would.
          const stillUnderneath = !!document.querySelector(".overlay .world-still");
          let distinct = 0;
          let prev = null;
          const N = 12;
          for (let i = 0; i < N; i++) {
            const shot = c.toDataURL();
            if (prev !== null && shot !== prev) distinct++;
            prev = shot;
            await new Promise((r2) => requestAnimationFrame(r2));
          }
          document.querySelector(".overlay-close")?.dispatchEvent(
            new MouseEvent("click", { bubbles: true }),
          );
          await new Promise((r2) => setTimeout(r2, 200));
          return {
            id,
            live: true,
            hasCtx: !!gl,
            canvasW: c.width,
            canvasH: c.height,
            distinct,
            samples: N,
            stillUnderneath,
            painted: distinct > 0,
            warmFrac: warm.warm / warm.total,
          };
        }
        const cs = getComputedStyle(el);
        const dur = parseFloat(cs.animationDuration) * 1000;
        const steps = parseInt(cs.animationTimingFunction.replace(/[^0-9]/g, ""), 10) || 1;
        // Does the strip have an IMAGE, and is it as many frames as it claims?
        //
        // This is the check that was missing while no world in this project turned.
        // `<Show>` without `keyed` hands its child an accessor rather than the value,
        // so the inline style was `url(() => uri)` — not a background image at all.
        // The element rendered, the animation ran, `background-position` advanced,
        // `background-size` was correct and `playState` was `running`, and the strip
        // was invisible. Asserting opacity, which is what this used to do, passed
        // throughout: an element with no background image is fully transparent, and
        // so is one that is correctly opaque. Only asking what is IN it works.
        const bi = cs.backgroundImage;
        const m = /url\(["']?(.*?)["']?\)/.exec(bi);
        let imgW = -1;
        if (m && m[1]) {
          try {
            const im = new Image();
            im.src = m[1];
            await im.decode();
            imgW = im.width;
          } catch {
            imgW = -1;
          }
        }
        // parseFloat, not Number: backgroundSize computes to "5600px 200px".
        const size = parseFloat(cs.backgroundSize) / steps;
        const out = {
          id,
          opacity: parseFloat(cs.opacity),
          playState: cs.animationPlayState,
          hasImage: bi !== "none" && imgW > 0,
          imgW,
          imgH: size,
          expectedW: size * steps,
          durMs: dur,
          steps,
          holdMs: Math.round(dur / steps),
        };
        document.querySelector(".overlay-close")?.dispatchEvent(
          new MouseEvent("click", { bubbles: true }),
        );
        await new Promise((r2) => setTimeout(r2, 200));
        return out;
      };
      return { crow: await open("the-crow"), burzsia: await open("burzsia") };
    });

    for (const [label, m] of [["black hole", overlaySpin.crow], ["world", overlaySpin.burzsia]]) {
      /**
       * Two implementations, one invariant.
       *
       * The world still uses a baked filmstrip; the black hole is a live WebGL
       * canvas. Both have to be VISIBLE and both have to actually change, and the
       * reason to branch is that "is it changing" is measured in completely
       * different units — the strip in milliseconds per frame, the canvas in
       * "how many successive animation frames drew something different".
       *
       * The canvas branch is the stronger of the two by a wide margin. The strip
       * has to be told 200ms a frame is too slow because it really is 8fps and
       * there is no way to fix that; the canvas is asserted to differ on
       * essentially every frame, which is the property that was actually missing.
       */
      if (m && m.live) {
        check(
          `the ${label} is drawn live and has a real context`,
          m.hasCtx && m.painted,
          m.hasCtx ? `context ok, ${m.distinct}/${m.samples} frames differed` : "no WebGL context",
        );
        check(
          `the ${label}'s canvas is sized to the world, not to the window`,
          m.canvasW > 0 && m.canvasW === m.canvasH,
          `${m.canvasW}x${m.canvasH}`,
        );
        check(
          `the ${label} changes on essentially every frame, not on a slideshow's`,
          m.distinct >= m.samples - 2,
          `${m.distinct} of ${m.samples - 1} frame-to-frame steps differed`,
        );
        check(
          `and the baked still is still underneath it, so the panel is never blank`,
          m.stillUnderneath === true,
          m.stillUnderneath ? "present" : "MISSING",
        );
        check(
          `the canvas draws the disc ITSELF, and is not the fallback showing through`,
          m.warmFrac > 0.01,
          `${(m.warmFrac * 100).toFixed(2)}% of the canvas is disc-coloured`,
        );
        continue;
      }
      check(
        `the ${label}'s rotating strip is visible, not merely present`,
        m && !m.noStrip && m.opacity > 0.9 && m.playState === "running",
        m && m.noStrip
          ? "no strip in the overlay at all"
          : m
            ? `opacity ${m.opacity}, playState ${m.playState}`
            : "no marker to open",
      );
      check(
        `and the ${label}'s strip actually HAS its picture — the check whose absence hid this`,
        m && !m.noStrip && m.hasImage && Math.abs(m.imgW - m.expectedW) < 2,
        m && !m.noStrip
          ? `background-image ${m.hasImage ? `${m.imgW}px wide` : "NONE — nothing to show"}, ` +
            `expected ${m.expectedW}px for ${m.steps} frames`
          : "no strip",
      );
      // 200ms a frame, i.e. 5fps minimum, tightened from the 1000ms this started
      // at. 1000ms is what a 28-frame strip over 48s produced, and it passed while
      // looking like a slideshow; the point of a threshold is to be the standard, not
      // the floor of whatever happened to be built. 96 frames over 12s is 125ms.
      check(
        `and the ${label} turns smoothly enough not to read as a slideshow`,
        m && !m.noStrip && m.holdMs > 0 && m.holdMs <= 200,
        m && !m.noStrip
          ? `${m.durMs}ms over ${m.steps} frames = ${m.holdMs}ms a frame ` +
            `(${(1000 / m.holdMs).toFixed(1)}fps)`
          : "no strip",
      );
    }

    /**
     * Consecutive frames of a turning world must actually LOOK different.
     *
     * This is the check for a failure mode that is worse than not animating, because
     * every other signal says it works. A gas giant was animating correctly — position
     * advancing, `playState: running`, opacity 1 — and two frames 1.6s apart differed
     * by 89 pixels out of 57888. The cause was structural: a band of constant
     * *latitude* is invariant under a shift in longitude, so the palette index came
     * back identical for the same pixel on every frame and the bands, which dominate
     * the image, simply did not move.
     *
     * Measured on the sprite rather than through the DOM, because the DOM can only
     * report that the animation is scheduled; it cannot report that the result looks
     * like motion. Decoding the strip and comparing frame 0 with frame 1 is the only
     * version of this question worth asking.
     */
    const framesDiff = await page.evaluate(async () => {
      const { uri } = window.__galaxySheet({ seed: 1992559903, type: "gas", px: 96, dpr: 1 }, 8);
      const im = new Image();
      im.src = uri;
      await im.decode();
      const c = document.createElement("canvas");
      c.width = im.width;
      c.height = im.height;
      const cx = c.getContext("2d");
      cx.drawImage(im, 0, 0);
      const d = cx.getImageData(0, 0, im.width, im.height).data;
      const F = im.width / 8;
      let diff = 0;
      let n = 0;
      for (let y = 0; y < im.height; y++) {
        for (let x = 0; x < F; x++) {
          const a = (y * im.width + x) * 4;
          const e = (y * im.width + x + F) * 4;
          n++;
          if (d[a] !== d[e] || d[a + 1] !== d[e + 1] || d[a + 2] !== d[e + 2]) diff++;
        }
      }
      return Math.round((100 * diff) / n);
    });

    check(
      "a turning world actually looks different frame to frame — animating is not the same as moving",
      framesDiff > 15,
      `a gas giant's frame 0 and frame 1 differ in ${framesDiff}% of pixels`,
    );

    check(
      "the black hole's strip is deferred, so opening it does not freeze the page",
      // Read SYNCHRONOUSLY, in the same task as the click. An earlier version of
      // this waited 60ms, which is already past the `setTimeout(0)` the deferral
      // uses, so it saw a finished strip and reported the deferral as broken. The
      // property being tested is "no strip exists at the end of the click's own
      // task", and only a same-task read can see that.
      await page.evaluate(() => {
        const hit = document.querySelector('.sys-hit[data-sys="the-crow"]');
        const r = hit.getBoundingClientRect();
        hit.dispatchEvent(
          new MouseEvent("click", {
            bubbles: true,
            clientX: r.x + r.width / 2,
            clientY: r.y + r.height / 2,
          }),
        );
        const early = {
          still: !!document.querySelector(".overlay .world-still"),
          strip: !!document.querySelector(".overlay .world-turn"),
        };
        document.querySelector(".overlay-close")?.dispatchEvent(
          new MouseEvent("click", { bubbles: true }),
        );
        return early.still && !early.strip;
      }),
      "still on screen, strip not yet generated, within the click's own task",
    );

    /**
     * Ring systems.
     *
     * Three things have to hold at once and none of them is implied by the other
     * two. There is a far half and a near half — one ring drawn as a single path
     * still looks like a ring, which is why a ring that lost its far side went
     * unnoticed until a path count gave it away. The far half comes BEFORE the
     * image and the near half AFTER, because the sprite does the occluding: it is
     * opaque across the disc and transparent outside it, so the far half is hidden
     * exactly where the planet is. And the whole thing has to FIT the overlay
     * panel — the first ratio put the ring at 330px and it was clipped at both
     * edges, which reads as a rendering fault rather than as a crop.
     *
     * The control is a world with no rings, which must contribute no paths at all.
     */
    // Ring markers only exist when systems are drawn as their worlds, so this
    // switches that on itself rather than relying on a panel having been opened
    // earlier. It used to press the spike's ON MAP checkbox, which is gone.
    await setPlanets(page, true);
    const ring = await page.evaluate(async () => {
      await new Promise((r) => setTimeout(r, 900));
      const far = [...document.querySelectorAll(".saturn-far")];
      const near = [...document.querySelectorAll(".saturn-near")];
      let ordered = far.length > 0;
      let nonEmpty = far.length > 0;
      // Sibling navigation, and NOT parentElement.querySelector. Every system
      // marker in the chart is a sibling in ONE <g> — Solid's <For> does not wrap
      // a per-item fragment — so a parent-scoped query returns the first image on
      // the chart rather than this marker's, and the check reports that the ring is
      // behind its planet when the planet is sixty systems away.
      // Walk forward to the sprite rather than demanding it be the IMMEDIATE next
      // sibling. A turning marker puts the image inside a <g clip-path>, so an
      // adjacency test fails on correct markup — the ordering that matters is "the
      // image is between the two halves", not "the two halves touch it".
      // Descends as well as walks: a turning marker wraps its image in a
      // <g clip-path>, so a sibling-only test never finds it even though the
      // document order is exactly right.
      const isSprite = (n) =>
        (n.tagName === "image" && n.classList.contains("planet-mark")) ||
        (n.tagName !== "image" && !!n.querySelector("image.planet-mark"));
      const imageAfter = (el) => {
        for (let n = el.nextElementSibling; n; n = n.nextElementSibling) if (isSprite(n)) return n;
        return null;
      };
      const imageBefore = (el) => {
        for (let n = el.previousElementSibling; n; n = n.previousElementSibling)
          if (isSprite(n)) return n;
        return null;
      };
      for (const f of far) {
        if (!(f.getAttribute("d") ?? "")) nonEmpty = false;
        if (!imageAfter(f)) ordered = false;
      }
      for (const n of near) {
        if (!(n.getAttribute("d") ?? "")) nonEmpty = false;
        if (!imageBefore(n)) ordered = false;
      }
      // The overlay's ring used to be measured here too, for the width of its SVG
      // against the panel. It is a canvas now, and the check above reads its pixels
      // and its box, so that measurement moved rather than being duplicated.
      return { far: far.length, near: near.length, ordered, nonEmpty };
    });

    /**
     * A black hole is a landmark, and a landmark that is not drawn is the worst
     * failure this chart has.
     *
     * The first version was a `<div>` with `<div>` children mounted inside the
     * chart's `<g>`. HTML does not render inside SVG, so every box measured 0x0, no
     * error was raised, and the marker was simply absent — at the one size where it
     * most needed to be seen. A screenshot of the area would have shown empty space
     * and looked like a placement problem. `drawn` is the check for exactly that.
     *
     * The rest are measured on the *decoded pixels* rather than on attributes,
     * because the thing that matters is legibility on a near-black page and that is
     * a property of the image, not of the markup. The void is deliberately the
     * absence of light: `voidLum > 0` says it is opaque without being a `#000`
     * hole in the chart, and `ringLum - voidLum > 60` says there is a photon ring
     * bright enough to hold the shape together. Both were true of the reference and
     * both are what stop this reading as a gap in the territory behind it.
     */
    const bh = await page.evaluate(async () => {
      const img = document.querySelector("[data-bh]");
      if (!img) return null;
      const r = img.getBoundingClientRect();
      const href = img.getAttribute("href");
      if (!href) return { drawn: r.width > 8, noHref: true };
      const im = new Image();
      im.src = href;
      await im.decode();
      const c = document.createElement("canvas");
      c.width = im.width;
      c.height = im.height;
      const cx = c.getContext("2d");
      cx.drawImage(im, 0, 0);
      const d = cx.getImageData(0, 0, im.width, im.height).data;
      const at = (x, y) => {
        const i = ((y | 0) * im.width + (x | 0)) * 4;
        return { a: d[i + 3], lum: 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2] };
      };
      // The DARKEST opaque pixel, not the centre. The disc is drawn over the
      // horizon, and at 30px its bar crosses the middle, so the centre sample came
      // back as disc (luminance 85) and the check could not tell a void from a
      // highlight. The minimum over the opaque set is the void wherever it is
      // visible, and "there is a dark-but-not-black pixel" is the property anyway.
      let voidLum = 255;
      let ringLum = 0;
      let warm = 0;
      for (let i = 0; i < d.length; i += 4) {
        if (d[i + 3] === 0) continue;
        const l = 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        if (l < voidLum) voidLum = l;
        if (l > ringLum) ringLum = l;
        // The accretion disc: warm, and clearly not the void.
        if (d[i] > 90 && d[i] > d[i + 2] + 40) warm++;
      }
      void at(im.width / 2, im.height / 2);
      const label = document.querySelector('.system-label[data-sys="the-crow"]');
      return {
        drawn: r.width > 8 && r.height > 8,
        w: Math.round(r.width),
        px: im.width,
        voidLum: Math.round(voidLum),
        ringLum: Math.round(ringLum),
        warm,
        total: d.length / 4,
        isPlanetMark: img.classList.contains("planet-mark"),
        labelClearance: label
          ? Math.round(label.getBoundingClientRect().x - (r.x + r.width / 2))
          : null,
      };
    });

    /**
     * Two shape regressions, both of which a screenshot of a single seed would have
     * hidden.
     *
     * **The gas giant's white blob.** The type had a separate cloud deck at 0.44 on
     * top of a surface that is already painted from a cloud field, so half the disc
     * went white — but the cloud field is seed-dependent, so it was a lottery rather
     * than a uniform wash. One seed banded correctly and one read as a blank ball,
     * and both were "working". The assertion is on the worst case over several
     * seeds rather than one, precisely because one seed proves nothing here.
     *
     * **The asteroid's detached arc.** The silhouette field was being multiplied by
     * the polar damping factor, which exists to stop the *sphere* projection from
     * aliasing near the limb. The silhouette is sampled in the disc plane and never
     * touches that projection, so there was nothing to damp — but damping pins the
     * field to exactly 0.5, and the threshold crosses 0.5 near the sprite's edge, so
     * the rock stopped short and then reappeared as a thin arc along the bottom.
     * Connectivity catches it directly, and it is a property a "is it round" check
     * cannot see.
     */
    const shapes = await page.evaluate(async () => {
      const load = async (type, seed) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed, type, px: 128, dpr: 1 });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        return cx.getImageData(0, 0, im.width, im.height);
      };
      // Largest share of the disc taken by any single colour, and the spread of the
      // opaque tones. A blank ball has one colour at ~50% and no spread.
      const toneStats = (img) => {
        const h = new Map();
        let n = 0;
        for (let i = 0; i < img.data.length; i += 4) {
          if (img.data[i + 3] === 0) continue;
          n++;
          const k = `${img.data[i]},${img.data[i + 1]},${img.data[i + 2]}`;
          h.set(k, (h.get(k) || 0) + 1);
        }
        const top = Math.max(...h.values()) / n;
        const lums = [...h.keys()]
          .map((k) => {
            const [r, g, b] = k.split(",").map(Number);
            return 0.299 * r + 0.587 * g + 0.114 * b;
          })
          .sort((a, b) => a - b);
        return { top: +top.toFixed(3), tones: h.size, spread: Math.round(lums[lums.length - 1] - lums[0]) };
      };
      /**
       * Connected components of the opaque region, 4-connected, AFTER one erosion.
       *
       * The erosion is not optional. The silhouette's outer boundary is dithered —
       * that is the same dither every other edge in the renderer uses — so the raw
       * opaque set is a solid body ringed by hundreds of isolated single pixels, and
       * counting those gives 300-500 "components" for a perfectly good rock. Eroding
       * once removes the fringe and leaves the body, and it removes the polar arc
       * this check exists to catch as well, since that was a thin dithered arc
       * rather than a solid shape. So one operation discards both the noise and the
       * bug.
       */
      const components = (img) => {
        const w = img.width;
        const h = img.height;
        const solid = new Uint8Array(w * h);
        let opaque = 0;
        for (let y = 1; y < h - 1; y++) {
          for (let x = 1; x < w - 1; x++) {
            const k = y * w + x;
            if (img.data[k * 4 + 3] === 0) continue;
            opaque++;
            if (
              img.data[(k - 1) * 4 + 3] > 0 &&
              img.data[(k + 1) * 4 + 3] > 0 &&
              img.data[(k - w) * 4 + 3] > 0 &&
              img.data[(k + w) * 4 + 3] > 0
            ) {
              solid[k] = 1;
            }
          }
        }
        const seen = new Uint8Array(w * h);
        let n = 0;
        let solidPx = 0;
        const stack = [];
        for (let k = 0; k < w * h; k++) {
          if (!solid[k] || seen[k]) continue;
          n++;
          solidPx++;
          stack.push(k);
          seen[k] = 1;
          while (stack.length) {
            const q = stack.pop();
            const x = q % w;
            const y = (q - x) / w;
            if (x > 0 && !seen[q - 1] && img.data[(q - 1) * 4 + 3] > 0) (seen[q - 1] = 1), stack.push(q - 1);
            if (x < w - 1 && !seen[q + 1] && img.data[(q + 1) * 4 + 3] > 0) (seen[q + 1] = 1), stack.push(q + 1);
            if (y > 0 && !seen[q - w] && img.data[(q - w) * 4 + 3] > 0) (seen[q - w] = 1), stack.push(q - w);
            if (y < h - 1 && !seen[q + w] && img.data[(q + w) * 4 + 3] > 0) (seen[q + w] = 1), stack.push(q + w);
          }
        }
        return { parts: n, solidShare: opaque ? +(solidPx / opaque).toFixed(2) : 0 };
      };
      const gas = [];
      for (const seed of [1992559903, 388817, 51, 90210, 7]) {
        gas.push(toneStats(await load("gas", seed)));
      }
      const rocks = [];
      for (const seed of [1992559903, 388817, 51, 90210]) {
        rocks.push(components(await load("asteroid", seed)));
      }
      return { gas, rocks };
    });

    check(
      "no gas giant is a white ball, across seeds — one seed proved nothing here",
      shapes.gas.every((g) => g.top < 0.4 && g.spread > 60),
      shapes.gas.map((g) => `top ${g.top}/spread ${g.spread}`).join(", "),
    );
    // Connectivity only. A "solid enough" share was tried here and reported 0% for
    // every seed including obviously solid rocks, which means the share is measuring
    // something other than what its name says; a broken assertion is worse than no
    // assertion, so it is gone rather than left to look like coverage.
    check(
      "an asteroid's silhouette is one connected piece",
      shapes.rocks.every((r) => r.parts === 1),
      shapes.rocks.map((r) => `${r.parts} piece(s)`).join(", "),
    );

    /**
     * Map markers turn.
     *
     * The structure is asserted rather than the motion. Waiting long enough to
     * watch a frame change means either a slow test or a short period, and a short
     * period is a lie about how the chart behaves; the thing that can silently break
     * is the wiring — a missing clip window, a missing `<animate>`, an image that is
     * `size` wide instead of `size * frames` wide, which renders frame 0 forever and
     * looks like a still. All of that is checkable at once, in milliseconds.
     *
     * It is SMIL rather than a CSS sprite sheet because an SVG `<image>` has no
     * background to step. `calcMode="discrete"` is the SVG-native equivalent of
     * `steps()` and needs no extra layer.
     *
     * The offsets are the tell. A strip that is present but not wired shows the
     * authored `x` forever, so `animVal` is what a motion check would read — and
     * `getAttribute` is not, because SMIL overrides the presentation value and never
     * touches the attribute.
     */
    await setPlanets(page, true);

    const turning = await page.evaluate(() => {
      const imgs = [...document.querySelectorAll("[data-turning]")];
      const first = imgs[0];
      const an = first?.querySelector("animate");
      const size = first ? Number(first.getAttribute("height")) : 0;
      const offsets = (an?.getAttribute("values") ?? "").split(";").filter(Boolean);
      return {
        count: imgs.length,
        frames: offsets.length,
        calcMode: an?.getAttribute("calcMode") ?? "",
        width: first ? Number(first.getAttribute("width")) : 0,
        size,
        // Every marker needs its own clip window, or two of them share one and the
        // second disappears behind the first.
        clips: document.querySelectorAll("clipPath[id^='turn-']").length,
        clipped: imgs.filter((i) => i.parentElement?.getAttribute("clip-path")).length,
        offsetsAreSteps: offsets.every((v, k) => k === 0 || Number(v) < Number(offsets[k - 1])),
      };
    });

    check(
      "map markers turn",
      turning.count > 0 && turning.frames > 1 && turning.calcMode === "discrete",
      `${turning.count} markers, ${turning.frames} frames, calcMode=${turning.calcMode}`,
    );
    check(
      "and each one is a filmstrip behind its own clip window, stepping by whole frames",
      turning.clips === turning.count &&
        turning.clipped === turning.count &&
        turning.offsetsAreSteps &&
        Math.abs(turning.width - turning.size * turning.frames) < 1,
      `${turning.clips} clips for ${turning.count} markers, image ${turning.width}px = ` +
        `${turning.size}px x ${turning.frames}`,
    );
    // Not "the still is underneath": the strip REPLACES the still, which is
    // correct, because both are produced in the same memo and so a marker is never
    // briefly missing. What matters is the opposite — that a marker is never left
    // with no image at all, which is what an empty strip href would do.
    check(
      "every world marker has an image, turning or not",
      await page.evaluate(() => {
        const marks = document.querySelectorAll("image.planet-mark");
        if (!marks.length) return false;
        return [...marks].every((m) => (m.getAttribute("href") ?? "").length > 100);
      }),
      await page.evaluate(() => {
        const marks = [...document.querySelectorAll("image.planet-mark")];
        return `${marks.length} marks, ${marks.filter((m) => (m.getAttribute("href") ?? "").length <= 100).length} with no image`;
      }),
    );

    /**
     * A turning marker's strip must line up with the window it is shown through.
     *
     * This is the same failure as the sprite that never turned, one level up. The
     * strip's frames advance by exactly `-size`, so the animation loops perfectly
     * and `calcMode="discrete"` does its job — every one of those properties held
     * while each frame sat half a sprite to the left of the clip window, so every
     * marker showed empty space down one side and half a world down the other.
     *
     * Asserting "the offsets are a decreasing arithmetic sequence" would pass on
     * the broken version, because it was. What has to be pinned is the ALIGNMENT:
     * the first offset has to be the clip rect's own left edge.
     */
    const align = await page.evaluate(() => {
      const imgs = [...document.querySelectorAll("image.planet-mark.turning")];
      if (!imgs.length) return null;
      let worstGap = 0;
      let worstStepErr = 0;
      let worstId = "";
      for (const img of imgs) {
        const id = img.getAttribute("data-turning");
        const rect = document.querySelector(`#turn-${id} rect`);
        const anim = img.querySelector("animate");
        if (!rect || !anim) continue;
        const clipX = Number(rect.getAttribute("x"));
        const size = Number(rect.getAttribute("width"));
        const vals = (anim.getAttribute("values") ?? "").split(";").map(Number);
        const gap = Math.abs(vals[0] - clipX);
        if (gap > worstGap) {
          worstGap = gap;
          worstId = id;
        }
        // Every step must be exactly -size, or the strip slides within the window.
        for (let k = 1; k < vals.length; k++) {
          worstStepErr = Math.max(worstStepErr, Math.abs(vals[k - 1] - vals[k] - size));
        }
      }
      return { count: imgs.length, worstGap, worstStepErr, worstId };
    });
    check(
      "a turning marker's first frame lines up with its clip window",
      align !== null && align.worstGap < 0.5,
      align
        ? `${align.count} markers, worst offset error ${align.worstGap.toFixed(2)}px${align.worstGap >= 0.5 ? ` on ${align.worstId}` : ""}`
        : "no turning markers",
    );
    check(
      "and each frame advances by exactly one sprite, so the strip cannot slide",
      align !== null && align.worstStepErr < 0.01,
      align ? `worst step error ${align.worstStepErr.toFixed(4)}px` : "no turning markers",
    );

    check("a black hole is actually drawn on the chart", bh !== null && bh.drawn,
      bh === null ? "no [data-bh] marker in the DOM" : `${bh.w}px box, ${bh.px}px sprite`);
    // The ceiling is not "as dark as I could make it", it is "does not read as a
    // hole punched in the page". The reference's void is #272737, luminance 40, and
    // the check used to demand under 40 because the void used to be an invented
    // #0b0912 at 7. The reference's colour is the authority; the floor stays at 0
    // so a genuinely pure black still fails.
    check("its void is drawn, and is dark without being pure black",
      bh !== null && bh.voidLum > 0 && bh.voidLum < 48,
      bh === null ? "no marker" : `darkest opaque pixel at luminance ${bh.voidLum}`);
    check("and the photon ring is bright enough to hold the shape together",
      bh !== null && bh.ringLum - bh.voidLum > 60,
      bh === null ? "no marker" : `brightest ${bh.ringLum} vs void ${bh.voidLum}`);
    check("the accretion disc is there, and it is warm",
      bh !== null && bh.warm > bh.total * 0.02,
      bh === null ? "no marker" : `${bh.warm} warm pixels of ${bh.total} (${Math.round((100 * bh.warm) / bh.total)}%)`);
    check("it is not drawn as a planet sprite",
      bh !== null && bh.isPlanetMark === false,
      bh === null ? "no marker" : `class is ${bh.isPlanetMark ? "planet-mark" : "its own"}`);
    check("and its label clears the disc",
      bh !== null && bh.labelClearance !== null && bh.labelClearance > bh.w / 2,
      bh === null || bh.labelClearance === null
        ? "no label"
        : `label ${bh.labelClearance}px from centre, sprite reaches ${Math.round(bh.w / 2)}px`);

    // Three per half, not one: a single flat band has no interior for the eye to
    // model and sits on the disc like a sticker. The count has to match on both
    // sides, which is what says the two halves are drawn to the same recipe.
    check(
      "a ringed system draws a layered far half and a layered near half",
      ring.far >= 3 && ring.far === ring.near,
      `${ring.far} far slices, ${ring.near} near slices`,
    );
    check(
      "the sprite occludes: far half before the image, near half after",
      ring.ordered && ring.nonEmpty,
      `ordering ${ring.ordered}, both halves have geometry ${ring.nonEmpty}`,
    );
    // The CHART's rings, measured here. The overlay has its own check above, which
    // reads the canvas ring's pixels rather than these SVG halves — this one is
    // about the 16-40px markers, where there is no canvas and no noise, only the
    // simplified geometry.
    check(
      "a world with no rings contributes no ring geometry",
      await page.evaluate(() => {
        // Every marker that is not ringed must have no ring path of its own.
        return document.querySelectorAll(".saturn-far").length === document.querySelectorAll(
          ".saturn-near",
        ).length;
      }),
      "far and near counts agree",
    );

    /**
     * Remnants: a pulsar and a quasar.
     *
     * Two properties each, because "the kind dispatched somewhere" is not the same
     * as "it drew a body", and the first version of this only checked the first.
     *
     * The pulsar has to have BEAMS and they have to be two, opposed, and different
     * brightnesses — a pulsar drawn as a bright dot is a white dwarf, and the whole
     * reason it is a separate kind is the beamed emission. Counting the two lobes is
     * what distinguishes them.
     *
     * The quasar has to have the black hole's disc AND jets, because a quasar with
     * no disc is just a black hole and one with no jets is just a black hole again.
     */
    const openSys = async (id) => {
      await page.evaluate(async (sys) => {
        const hit = document.querySelector(`.sys-hit[data-sys='${sys}']`);
        if (!hit) return;
        const r = hit.getBoundingClientRect();
        document
          .querySelector("svg.chart")
          .dispatchEvent(
            new MouseEvent("click", {
              bubbles: true,
              clientX: r.x + r.width / 2,
              clientY: r.y + r.height / 2,
            }),
          );
      }, id);
      await page.waitForSelector(".overlay", { timeout: 8000 });
      await page.waitForTimeout(800);
    };
    const closeSys = async () => {
      await page.evaluate(() => document.querySelector(".overlay-close")?.click());
      await page.waitForTimeout(250);
    };

    await openSys("pulsar-1");
    const pulsarShown = await page.evaluate(() => !!document.querySelector(".overlay .pulsar"));

    /**
     * Count the beams by walking out from the core along eight directions.
     *
     * Opposed matters as much as present: two lobes on the same side would be one
     * wide beam, and no lobes at all is a dot. So the count is of directions whose
     * OPPOSITE also found light, which is a stricter test than counting lit
     * directions and is the one that says "this is a pulsar" rather than "this is
     * something glowing".
     */
    const opposedBeams = await page.evaluate(async () => {
      const c = document.querySelector(".pulsar");
      if (!c) return -1;
      const im = new Image();
      im.src = c.toDataURL();
      await im.decode();
      const off = document.createElement("canvas");
      off.width = c.width;
      off.height = c.height;
      const g = off.getContext("2d");
      g.drawImage(im, 0, 0);
      const d = g.getImageData(0, 0, off.width, off.height).data;
      const mid = off.width / 2;
      const lit = (x, y) => {
        const i = (Math.round(y) * off.width + Math.round(x)) * 4;
        return d[i + 3] > 40;
      };
      const dirs = [];
      for (let k = 0; k < 8; k++) {
        const a = (k * Math.PI) / 4;
        for (let t = mid * 0.18; t < mid * 0.92; t += 2) {
          if (lit(mid + Math.cos(a) * t, mid + Math.sin(a) * t)) {
            dirs.push(Math.round((a * 180) / Math.PI));
            break;
          }
        }
      }
      return dirs.filter((d0) => dirs.includes((d0 + 180) % 360)).length / 2;
    });
    await closeSys();

    await openSys("quasar-1");
    const quasar = await page.evaluate(() => {
      const o = document.querySelector(".overlay");
      return {
        jets: o.querySelectorAll(".quasar-jets path").length,
        disc: !!o.querySelector(".blackhole .world-gl, .blackhole .world-still"),
      };
    });
    await closeSys();

    check("a pulsar draws a pulsar canvas", pulsarShown, ".pulsar present in the overlay");
    check(
      "with two OPPOSED beams, which is the only reason it is not a white dwarf",
      opposedBeams >= 1,
      `${opposedBeams} opposed pair(s) of lit lobes either side of the core. A pulsar drawn as a ` +
        `bright dot is a white dwarf, and the beamed emission is the entire difference, so ` +
        `counting the beams is the assertion that means anything.`,
    );
    check(
      "a quasar draws the black hole's disc AND jets",
      quasar.disc && quasar.jets === 2,
      `disc ${quasar.disc}, ${quasar.jets} jet paths`,
    );

    /**
     * The eighth planet type: rivers.
     *
     * Asserted by counting the reference's own water colour rather than by looking,
     * because "is this a river world" is not a shape question and a shape question
     * would pass on a world with one large lake in it.
     *
     * The control is terran, which uses the SAME river pass with the default
     * threshold. Without it this would pass on any world that happened to have water
     * on it, which is all of them.
     */
    const rivers = await page.evaluate(async () => {
      const count = async (type) => {
        const uri = window.__galaxyStill({ seed: 4242, type, px: 200, dpr: 1 });
        const im = new Image();
        im.src = uri;
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const g = c.getContext("2d");
        g.drawImage(im, 0, 0);
        const d = g.getImageData(0, 0, c.width, c.height).data;
        let opaque = 0;
        let water = 0;
        for (let i = 0; i < d.length; i += 4) {
          if (d[i + 3] < 128) continue;
          opaque++;
          const r = d[i];
          const g2 = d[i + 1];
          const b = d[i + 2];
          // The river type's two water steps, #4fa4b8 and #404973. Terran's water is
          // a different blue entirely, which is why this measures the river palette
          // specifically rather than "bluish pixels".
          if (Math.abs(r - 79) < 14 && Math.abs(g2 - 164) < 14 && Math.abs(b - 184) < 14) water++;
          else if (Math.abs(r - 64) < 14 && Math.abs(g2 - 73) < 14 && Math.abs(b - 115) < 14) water++;
        }
        return { pct: (100 * water) / opaque, water };
      };
      return { river: await count("river"), terran: await count("terran") };
    });
    check(
      "a river world has rivers, in its own palette",
      rivers.river.pct > 3,
      `${rivers.river.pct.toFixed(1)}% of the disc is river water`,
    );
    check(
      "and a terran world does not — the control, without which the one above means nothing",
      rivers.terran.pct < 0.5,
      `${rivers.terran.pct.toFixed(1)}%, from the same pass at its default threshold`,
    );

    check(
      "an ice world has melt water on its sheet, not just one continent-sized ocean",
      lake.wetOn > lake.wetOff * 1.2,
      `wet pixels ${lake.wetOn} with the second field, ${lake.wetOff} without ` +
        `(${lake.on} vs ${lake.off} separate bodies, reported but not asserted)`,
    );
    check(
      "and a world with no lakes is untouched by the flag — the control",
      lake.terranSame,
      lake.terranSame ? "byte-identical" : "terran changed, so the flag is not inert",
    );

    check(
      "an airless world draws craters",
      crat.barren.changed > crat.barren.total * 0.01 && crat.lava.changed > crat.lava.total * 0.01,
      `barren ${crat.barren.changed}/${crat.barren.total}px, lava ${crat.lava.changed}/${crat.lava.total}px`,
    );
    // Barren only, and the exclusion is the point rather than an oversight. A
    // crater legitimately contains BOTH a shadowed floor and a lit rim, so the
    // sign of the change is a property of the type's palette and not of craters:
    // on grey rock the floor dominates and 99% of changed pixels go darker, while
    // on a lava world the rim is molten rock and is the brightest thing on the
    // planet, which puts this at 46%. Asserting "darker" for both was asserting a
    // fact about grey.
    check(
      "and on grey rock a crater is a shadow, not a highlight",
      crat.barren.darker / crat.barren.changed > 0.9,
      `${Math.round((100 * crat.barren.darker) / crat.barren.changed)}% of changed pixels went darker ` +
        `(lava ${Math.round((100 * crat.lava.darker) / crat.lava.changed)}%, and that is correct: its rim is molten)`,
    );
    check(
      "a world with an atmosphere is untouched by the flag — the control, without which the two above mean nothing",
      crat.terran.changed === 0,
      `${crat.terran.changed} pixels changed on a world with no craters to suppress`,
    );

    /**
     * An asteroid is not a disc.
     *
     * Measured as the spread of the outline's radius with angle. A circle gives
     * a spread of zero by definition, so this cannot pass for a round sprite
     * however it is shaded, and it does not care what the rock is made of — the
     * first version of this idea counted dark patches, which a crater also
     * produces and which therefore could not tell a lumpy outline from a smooth
     * one with holes in it.
     *
     * `terran` is the control, and it is a fair one: same renderer, same lighting
     * path, same dither, and a perfectly circular outline. If the measurement were
     * picking up the dither pattern or the banding, it would fire on terran too.
     */
    const outline = await page.evaluate(async () => {
      const spread = async (type) => {
        const im = new Image();
        im.src = window.__galaxyStill({ seed: 0x5eed1, type, px: 160, dpr: 1 });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        const N = 180;
        const radii = [];
        for (let k = 0; k < N; k++) {
          const a = (k / N) * Math.PI * 2;
          const ca = Math.cos(a);
          const sa = Math.sin(a);
          let last = 0;
          // Walk out to the box edge. An earlier version stopped at 0.9, which
          // silently clipped the measurement: a rock wider than that would have
          // reported the same radius as one exactly at the limit, and the
          // standard deviation — the only number this check reads — would have
          // been computed from truncated data without saying so.
          for (let t = 1; t <= 100; t++) {
            const r = t / 100;
            const x = Math.round(w / 2 + ca * r * w * 0.5);
            const y = Math.round(h / 2 + sa * r * h * 0.5);
            if (x < 0 || y < 0 || x >= w || y >= h) break;
            if (d[(y * w + x) * 4 + 3] > 0) last = r;
          }
          radii.push(last);
        }
        const mean = radii.reduce((a, b) => a + b, 0) / radii.length;
        const sd = Math.sqrt(radii.reduce((a, b) => a + (b - mean) ** 2, 0) / radii.length);
        return { mean, sd, min: Math.min(...radii), max: Math.max(...radii) };
      };
      return { asteroid: await spread("asteroid"), terran: await spread("terran") };
    });

    check(
      "an asteroid's outline is lumpy, not a circle",
      outline.asteroid.sd > 0.05,
      `radius ${outline.asteroid.mean.toFixed(2)} ± ${outline.asteroid.sd.toFixed(3)} ` +
        `(range ${outline.asteroid.min.toFixed(2)}–${outline.asteroid.max.toFixed(2)})`,
    );
    check(
      "and a world's outline still is — the control",
      outline.terran.sd < 0.02,
      `radius ${outline.terran.mean.toFixed(2)} ± ${outline.terran.sd.toFixed(3)}`,
    );

    /**
     * Gas giant bands run east-west.
     *
     * Measured as the ratio of vertical to horizontal colour change. Bands of
     * constant latitude change fast as you move up the disc and slowly as you move
     * across it, so the ratio is well above one. A star's structure is the other
     * way round — concentric, so it changes fastest across — which makes it the
     * control, and a control that has to come out BELOW the threshold is a much
     * stronger one than a second type that merely agrees.
     *
     * This exists because a wrong conclusion about the gas giant survived a round
     * of screenshots: bands removed, weather only, and the swirls looked plausible
     * enough to keep. Measuring the direction of the structure cannot be
     * satisfied by isotropic mottle.
     */
    const aniso = await page.evaluate(async () => {
      const ratio = async (type) => {
        const im = new Image();
        // Dither OFF. This measures the band field, not the band field plus the
        // ordered dither: with it on, the per-pixel dither contributes the same
        // variance in both directions and swamps the structure entirely — the
        // first run of this check returned 1.04 for a visibly banded gas giant
        // and 1.00 for a star, i.e. both perfectly isotropic, which is what a
        // measurement of noise looks like.
        im.src = window.__galaxyStill({ seed: 0x5eed1, type, px: 128, dpr: 1, dither: false });
        await im.decode();
        const c = document.createElement("canvas");
        c.width = im.width;
        c.height = im.height;
        const cx = c.getContext("2d");
        cx.drawImage(im, 0, 0);
        const d = cx.getImageData(0, 0, im.width, im.height).data;
        const w = im.width;
        const h = im.height;
        const L = (x, y) => {
          const i = (y * w + x) * 4;
          if (d[i + 3] === 0) return null;
          return 0.299 * d[i] + 0.587 * d[i + 1] + 0.114 * d[i + 2];
        };
        // Central 60% only: the limb is where the sphere projection compresses
        // everything, and a compression artefact would swamp the measurement.
        const x0 = Math.round(w * 0.2);
        const x1 = Math.round(w * 0.8);
        const y0 = Math.round(h * 0.2);
        const y1 = Math.round(h * 0.8);
        let nv = 0;
        let sv = 0;
        let nh = 0;
        let sh = 0;
        for (let y = y0; y < y1; y++) {
          for (let x = x0; x < x1; x++) {
            const a = L(x, y);
            const b = L(x, y + 1);
            if (a !== null && b !== null) {
              sv += Math.abs(a - b);
              nv++;
            }
            const cc = L(x + 1, y);
            if (a !== null && cc !== null) {
              sh += Math.abs(a - cc);
              nh++;
            }
          }
        }
        return sv / nv / (sh / nh);
      };
      return { gas: await ratio("gas"), star: await ratio("star"), terran: await ratio("terran") };
    });

    check(
      "gas giant bands run east-west, not around the planet",
      aniso.gas > 1.3,
      `vertical/horizontal colour change ${aniso.gas.toFixed(2)}`,
    );
    check(
      "and a star's structure is the other way round — the control, which has to come out below the threshold",
      aniso.star < 1.15,
      `star ${aniso.star.toFixed(2)}, gas ${aniso.gas.toFixed(2)}`,
    );

    check(
      "a world is drawn from a small discrete palette, not a smooth gradient",
      look.worldColours < 400,
      `${look.worldColours} distinct colours in ${look.px} pixels`,
    );
    check(
      "a star is banded too, not a gradient",
      look.starColours < 400,
      `${look.starColours} distinct colours`,
    );
    // The corona is the one thing allowed outside the disc. Guards the rule that
    // a planet is exactly its sprite: an atmosphere bleeding past the edge was
    // what made the first sprite set read as stickers.
    check(
      "a planet is exactly its disc — no halo outside",
      look.worldCorner === 0 && look.worldCentre === 255,
      `corner alpha ${look.worldCorner}, centre ${look.worldCentre}`,
    );
    check(
      "a star does bleed past its edge",
      look.starEdge > 0 && look.starCentre === 255,
      `annulus alpha ${look.starEdge}, centre ${look.starCentre}`,
    );
    check(
      "and it is a corona, not a halo: the annulus is dimmer than the disc",
      look.starEdge < look.starCentre,
      `${look.starEdge} vs ${look.starCentre}`,
    );
    void look.worldEdge;

    // The locale toggle must actually translate the chrome and the content.
    const uk = await page.evaluate(async () => {
      const pick = [...document.querySelectorAll(".locpick button")].find(
        b => b.textContent.trim() === "УКР",
      );
      if (!pick) return { found: false };
      pick.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 150));
      return {
        found: true,
        grid: document.querySelector(".toolbar button")?.textContent?.trim() ?? "",
        paint: document.querySelector(".paintlabel")?.textContent?.trim() ?? "",
        legend: document.querySelector(".legend b")?.textContent?.trim() ?? "",
        // A territory title, which is content rather than chrome.
        territory: document.querySelector(".terr-name")?.textContent?.trim() ?? "",
        legendText: document.querySelector(".legend")?.textContent ?? "",
      };
    });
    check("the locale picker offers Ukrainian", uk.found);
    check("chrome is translated", uk.grid === "СІТКА" && uk.paint === "ФАРБА", `${uk.grid} / ${uk.paint}`);
    check("the chart title is translated", uk.legend === "РУКАВ ОРІОНА", uk.legend);
    check(
      "content names are translated",
      /[А-ЯІЇЄҐ]/.test(uk.territory),
      uk.territory,
    );
    check(
      "counts use Ukrainian units and plurals",
      /св\.р\./.test(uk.legendText) && !/ LY /.test(uk.legendText),
      uk.legendText.replace(/\s+/g, " ").slice(0, 64),
    );
    // Back to English, so the brushes below start from a known state.
    await page.evaluate(async () => {
      const pick = [...document.querySelectorAll(".locpick button")].find(
        b => b.textContent.trim() === "EN",
      );
      pick?.dispatchEvent(new MouseEvent("click", { bubbles: true }));
      await new Promise(r => setTimeout(r, 120));
    });

    const overlay = await page.evaluate(() => {
      const layer = document.querySelector(".contested-layer path");
      return (layer?.getAttribute("d") ?? "").length;
    });
    check("contested ground is drawn on the chart", overlay > 0, `${overlay} chars of path`);

    check("no page errors", errors.length === 0, errors.join(" | "));
    await ctx.close();
  }
} finally {
  await browser.close();
  await vite.close();
}

console.log(`\n${failures === 0 ? "all dom checks passed" : `${failures} DOM CHECK(S) FAILED`}\n`);
process.exit(failures === 0 ? 0 : 1);
