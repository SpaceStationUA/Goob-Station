/**
 * Verifies the edge -> neighbour mapping in hex.ts against the actual geometry.
 *
 * The mapping says which of a cell's six edges faces a given neighbour. Getting
 * it wrong makes boundary extraction pick the wrong edges, which shows up as
 * borders that fragment into hundreds of disconnected blobs rather than long
 * chains — a confusing failure that is much cheaper to catch here.
 */
import { EDGE_TO_NEIGHBOUR, NEIGHBOURS, hexCorners, hexToPixel, key, neighbour, type Axial } from "../src/GalaxyMap/lib/hex";

const SIZE = 2.0;
let failures = 0;

/** Signed area tells us which side of a->b a point falls on. */
function side(a: { x: number; y: number }, b: { x: number; y: number }, p: { x: number; y: number }) {
  return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
}

const cells: Axial[] = [];
for (let q = -4; q <= 4; q++) for (let r = -4; r <= 4; r++) cells.push({ q, r });

let checked = 0;
const report: string[] = [];

for (const cell of cells) {
  const centre = hexToPixel(cell, SIZE);
  const corners = hexCorners(centre, SIZE);

  for (let e = 0; e < 6; e++) {
    const a = corners[e];
    const b = corners[(e + 1) % 6];
    const nb = neighbour(cell, e);
    const nbCentre = hexToPixel(nb, SIZE);

    // The cell centre and the neighbour centre must be on opposite sides of the
    // edge line. If they are not, the mapping points at a different edge.
    const s1 = side(a, b, centre);
    const s2 = side(a, b, nbCentre);
    checked++;
    if (s1 * s2 >= 0) {
      failures++;
      if (report.length < 6) {
        report.push(
          `  cell ${cell.q},${cell.r} edge ${e}: centre side ${s1.toFixed(3)}, ` +
            `neighbour ${nb.q},${nb.r} side ${s2.toFixed(3)}`,
        );
      }
    }

    // The neighbour must also actually be one of the six, and must be the
    // NEAREST of them along the edge normal.
    const dx = nbCentre.x - centre.x;
    const dy = nbCentre.y - centre.y;
    const len = Math.hypot(dx, dy);
    const ux = dx / len;
    const uy = dy / len;
    let best = -1;
    let bestDot = -Infinity;
    for (let i = 0; i < 6; i++) {
      const n = NEIGHBOURS[i];
      const c2 = hexToPixel({ q: cell.q + n.q, r: cell.r + n.r }, SIZE);
      const ddx = c2.x - centre.x;
      const ddy = c2.y - centre.y;
      const dl = Math.hypot(ddx, ddy);
      const dot = (ddx / dl) * ux + (ddy / dl) * uy;
      if (dot > bestDot) {
        bestDot = dot;
        best = i;
      }
    }
    if (best !== EDGE_TO_NEIGHBOUR[e]) {
      failures++;
      if (report.length < 12) {
        report.push(
          `  cell ${cell.q},${cell.r} edge ${e}: table says NEIGHBOURS[${EDGE_TO_NEIGHBOUR[e]}] ` +
            `but geometry says NEIGHBOURS[${best}]`,
        );
      }
    }
  }
}

console.log(`\nedge->neighbour: checked ${checked} (cell, edge) pairs`);
if (report.length) console.log(report.join("\n"));

// Round-trip: the six edges must yield the six distinct neighbours.
const base: Axial = { q: 0, r: 0 };
const viaEdges = new Set<string>();
for (let e = 0; e < 6; e++) {
  const n = neighbour(base, e);
  viaEdges.add(key(n.q, n.r));
}
const distinct = viaEdges.size === 6;
if (!distinct) failures++;
console.log(`  six edges yield six distinct neighbours: ${distinct ? "ok" : `FAIL (${viaEdges.size})`}`);

console.log(`\nEDGE_TO_NEIGHBOUR = [${[...EDGE_TO_NEIGHBOUR].join(", ")}]`);
console.log(failures === 0 ? "edge mapping is correct\n" : `${failures} EDGE MAPPING FAILURE(S)\n`);
process.exit(failures === 0 ? 0 : 1);
