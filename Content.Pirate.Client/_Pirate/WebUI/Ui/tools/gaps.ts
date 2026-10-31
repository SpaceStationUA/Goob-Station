/**
 * Reports how close each pair of territory claims is, and whether they are
 * close enough to end up sharing a border.
 *
 * The authoring rule this makes concrete: cells are assigned by their centre,
 * so two claims only need to be closer than one cell (minus a little) to end
 * up adjacent. Leave roughly half a cell of daylight between them and they will
 * still meet — which is what lets claims be hand-drawn without having to share
 * an exact boundary line.
 *
 *   npm run gaps
 */
import { CLAIMS } from "../src/GalaxyMap/lib/devmap";
import { DEFAULT_MAP } from "../src/GalaxyMap/lib/source";
import type { Vec2 } from "../src/GalaxyMap/lib/hex";

const cell = DEFAULT_MAP.hexSizeLy;

function pointSegDist(p: Vec2, a: Vec2, b: Vec2): number {
  const vx = b.x - a.x;
  const vy = b.y - a.y;
  const L = vx * vx + vy * vy;
  let t = L > 0 ? ((p.x - a.x) * vx + (p.y - a.y) * vy) / L : 0;
  t = Math.max(0, Math.min(1, t));
  return Math.hypot(a.x + vx * t - p.x, a.y + vy * t - p.y);
}

function polyDist(A: Vec2[], B: Vec2[]): number {
  let m = Infinity;
  for (const p of A) for (let i = 0; i < B.length; i++) m = Math.min(m, pointSegDist(p, B[i], B[(i + 1) % B.length]));
  for (const p of B) for (let i = 0; i < A.length; i++) m = Math.min(m, pointSegDist(p, A[i], A[(i + 1) % A.length]));
  return m;
}

const named = CLAIMS.filter(c => c.id !== "unclaimed");
let sharing = 0;

console.log(`\ncell size ${cell} LY — claims closer than this can still share a border\n`);
for (let i = 0; i < named.length; i++) {
  for (let j = i + 1; j < named.length; j++) {
    const d = polyDist(named[i].polygon, named[j].polygon);
    const shares = d < cell * 0.75;
    if (shares) sharing++;
    const verdict = shares ? "shares border" : d < cell ? "close" : d < cell * 3 ? "near" : "far";
    console.log(
      `  ${named[i].id.padEnd(10)} ${named[j].id.padEnd(10)} ${d.toFixed(2).padStart(6)} LY  ${verdict}`,
    );
  }
}

console.log(`\n${sharing} bordering pair(s). A political map wants several — a chart whose`);
console.log(`nations only ever border empty space has nothing to show.\n`);
