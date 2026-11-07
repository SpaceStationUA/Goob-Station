/**
 * Pointy-top hexagonal grid, axial coordinates (q, r).
 *
 * The grid is *logical only* — it is never drawn as-is. It exists so that
 * territory membership, authoring and round-time painting are all integer
 * operations. What the player sees is a smoothed territory outline that
 * happens to be quantised to this lattice.
 */

export interface Axial {
  q: number;
  r: number;
}

export interface Vec2 {
  x: number;
  y: number;
}

/** Stable string key for a cell. Also the format used in the baked data files. */
export function key(q: number, r: number): string {
  return `${q},${r}`;
}

export function parseKey(k: string): Axial {
  const i = k.indexOf(",");
  return { q: +k.slice(0, i), r: +k.slice(i + 1) };
}

/** The six neighbours, in counter-clockwise order starting from +q. */
export const NEIGHBOURS: readonly Axial[] = [
  { q: +1, r: 0 },
  { q: +1, r: -1 },
  { q: 0, r: -1 },
  { q: -1, r: 0 },
  { q: -1, r: +1 },
  { q: 0, r: +1 },
];

/**
 * Which NEIGHBOURS index sits across edge `e`.
 *
 * Edge e runs from corner e to corner e+1. Corners sit at 60*e - 30 degrees, so
 * the edge's outward normal points at 60*e degrees. Matching that against the six
 * neighbour directions gives the table below.
 *
 * Verified by tools/check-hex.ts rather than trusted to the derivation. A wrong
 * entry makes boundary extraction pick the wrong edges, which surfaces as
 * borders fragmenting into hundreds of disconnected blobs instead of chains.
 */
export const EDGE_TO_NEIGHBOUR: readonly number[] = [0, 5, 4, 3, 2, 1];

export function neighbour(a: Axial, edge: number): Axial {
  const n = NEIGHBOURS[EDGE_TO_NEIGHBOUR[edge]];
  return { q: a.q + n.q, r: a.r + n.r };
}

/** Cell centre in light-year space. */
export function hexToPixel(a: Axial, sizeLy: number): Vec2 {
  return {
    x: sizeLy * Math.sqrt(3) * (a.q + a.r / 2),
    y: sizeLy * 1.5 * a.r,
  };
}

/** The six corners of a cell, counter-clockwise, starting at the top point. */
export function hexCorners(centre: Vec2, sizeLy: number): Vec2[] {
  const pts: Vec2[] = [];
  for (let i = 0; i < 6; i++) {
    const a = (Math.PI / 180) * (60 * i - 30);
    pts.push({ x: centre.x + sizeLy * Math.cos(a), y: centre.y + sizeLy * Math.sin(a) });
  }
  return pts;
}

function cubeRound(x: number, y: number, z: number): Axial {
  let rx = Math.round(x);
  let ry = Math.round(y);
  let rz = Math.round(z);
  const dx = Math.abs(rx - x);
  const dy = Math.abs(ry - y);
  const dz = Math.abs(rz - z);
  if (dx > dy && dx > dz) rx = -ry - rz;
  else if (dy > dz) ry = -rx - rz;
  else rz = -rx - ry;
  return { q: rx, r: rz };
}

/** Nearest cell to a point in light-year space. */
export function pixelToHex(p: Vec2, sizeLy: number): Axial {
  const q = ((Math.sqrt(3) / 3) * p.x - (1 / 3) * p.y) / sizeLy;
  const r = ((2 / 3) * p.y) / sizeLy;
  return cubeRound(q, -q - r, r);
}

/**
 * Every cell whose centre falls inside the map rectangle.
 *
 * Rows are walked with a one-cell margin either side so that territories
 * bleeding off the edge still produce closed outlines.
 */
/** Distance between two cells, in steps. */
export function hexDistance(a: Axial, b: Axial): number {
  const dq = a.q - b.q;
  const dr = a.r - b.r;
  return (Math.abs(dq) + Math.abs(dq + dr) + Math.abs(dr)) / 2;
}

/**
 * Every cell on the straight line from `a` to `b`, both ends included.
 *
 * Drag-to-paint needs this. Sampling only the cells the pointer happens to be
 * over during a drag leaves gaps: a fast flick crosses several cells between two
 * mousemove events, and the stroke comes out dashed instead of solid. Walking
 * the hex line and filling every cell on it is what makes a drag read as one
 * continuous mark.
 *
 * Lerped in cube space and rounded, because rounding in axial space alone walks
 * the wrong path on the diagonals.
 */
export function hexLine(a: Axial, b: Axial): Axial[] {
  const n = hexDistance(a, b);
  if (n === 0) return [a];
  const ax = a.q;
  const az = a.r;
  const ay = -ax - az;
  const bx = b.q;
  const bz = b.r;
  const by = -bx - bz;
  const out: Axial[] = [];
  // Nudge off the exact endpoints: at i=0 and i=N the fractional coordinates land
  // on a lattice point, and cubeRound can then pick a neighbouring cell.
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    out.push(
      cubeRound(
        ax + (bx - ax) * t + 1e-6,
        ay + (by - ay) * t + 2e-6,
        az + (bz - az) * t - 3e-6,
      ),
    );
  }
  return out;
}

export function cellsInExtent(wLy: number, hLy: number, sizeLy: number): Axial[] {
  const out: Axial[] = [];
  // Vertical extent of cell centres, plus margin.
  const rMin = Math.floor(-(hLy / 2) / (1.5 * sizeLy)) - 1;
  const rMax = Math.ceil(hLy / 2 / (1.5 * sizeLy)) + 1;
  for (let r = rMin; r <= rMax; r++) {
    // Half a row of horizontal shear per row step.
    const xOffset = (sizeLy * Math.sqrt(3) * r) / 2;
    const qMin = Math.floor((-wLy / 2 - xOffset) / (sizeLy * Math.sqrt(3))) - 1;
    const qMax = Math.ceil((wLy / 2 - xOffset) / (sizeLy * Math.sqrt(3))) + 1;
    for (let q = qMin; q <= qMax; q++) {
      const c = hexToPixel({ q, r }, sizeLy);
      if (c.x >= -wLy / 2 && c.x <= wLy / 2 && c.y >= -hLy / 2 && c.y <= hLy / 2) {
        out.push({ q, r });
      }
    }
  }
  return out;
}
