import type { Vec2 } from "./hex";
import { loopToPath } from "./geometry";

export interface MapTransform {
  /** Pixels per light-year. */
  scale: number;
  /** Pixel position of the map centre. */
  ox: number;
  oy: number;
  width: number;
  height: number;
  toPx(p: Vec2): Vec2;
  toLy(p: Vec2): Vec2;
}

/**
 * Fit the map into a pixel box, preserving aspect and centring.
 *
 * Everything downstream works in pixels. Converting here rather than leaning on
 * an SVG viewBox in light-year units means stroke widths, font sizes and marker
 * radii are real pixel values instead of numbers that silently scale with the
 * viewport — which is exactly how the first draft ended up with 24px borders
 * and 66px glows.
 */
export function makeTransform(
  extentLy: { w: number; h: number },
  width: number,
  height: number,
  pad = 0,
): MapTransform {
  const scale = Math.min((width - pad * 2) / extentLy.w, (height - pad * 2) / extentLy.h);
  const ox = width / 2;
  const oy = height / 2;
  return {
    scale,
    ox,
    oy,
    width,
    height,
    toPx: p => ({ x: p.x * scale + ox, y: p.y * scale + oy }),
    toLy: p => ({ x: (p.x - ox) / scale, y: (p.y - oy) / scale }),
  };
}

/** A loop in light-year space, emitted as an SVG path in pixel space. */
export function loopToPxPath(loop: Vec2[], t: MapTransform, close = true): string {
  return loopToPath(loop.map(t.toPx), close);
}

/** Several loops as one path — even-odd fill handles holes. */
export function loopsToPxPath(loops: Vec2[][], t: MapTransform): string {
  return loops
    .map(l => loopToPath(l.map(t.toPx), true))
    .filter(Boolean)
    .join(" ");
}
