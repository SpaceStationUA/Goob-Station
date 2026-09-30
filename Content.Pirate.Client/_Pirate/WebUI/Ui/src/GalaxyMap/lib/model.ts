/**
 * The galaxy model, as the page sees it.
 *
 * Deliberately a plain data shape with no reference to the game. The page is
 * handed one of these and never asks where it came from, which is what lets
 * the identical code run in a browser (fixture) and in the game (bridge).
 */

export type PatternId =
  | "solid"
  | "hatch"
  | "crosshatch"
  | "dots"
  | "grid"
  | "horizontal"
  | "vertical"
  | "checker"
  | "starfield";

export interface Territory {
  id: string;
  /** Resolved server-side; already localised by the time it reaches the page. */
  name: string;
  /** Hex colour, "#rrggbb". */
  color: string;
  pattern: PatternId;
  /** Optional short descriptor shown in the side panel. */
  blurb?: string;
  /** True for the automatic "everything nobody claimed" remainder. */
  unclaimed?: boolean;
}

export type SystemKind = "star" | "planet" | "station" | "gate" | "outpost";

/** 0 = minor, 3 = capital. Drives marker size and ring treatment. */
export type Importance = 0 | 1 | 2 | 3;

export interface StarSystem {
  id: string;
  name: string;
  xLy: number;
  yLy: number;
  kind: SystemKind;
  importance: Importance;
  /** Empty when the system sits in unclaimed space. */
  territory: string;
}

/** A travel link. Drawn now as a line; gameplay wiring is a later concern. */
export interface Route {
  from: string;
  to: string;
  kind: "gate" | "hyperlane";
  /** Territory ids permitted to use it. Empty means anyone. */
  allowed: string[];
}

/**
 * Territory membership, as cell key -> territory id.
 *
 * This is the only mutable part of the model. The baseline comes from the
 * baked data; a round-time paint session layers sparse changes on top and
 * they are discarded at round restart.
 */
export type Ownership = Map<string, string>;

export interface GalaxyModel {
  /** Map extent in light-years. The chart frame and the graticule use this. */
  extentLy: { w: number; h: number };
  /** Centre-to-corner radius of one cell, in light-years. */
  hexSizeLy: number;
  territories: Territory[];
  systems: StarSystem[];
  routes: Route[];
  ownership: Ownership;
  /** Bumped whenever ownership changes, so the view can key its caches. */
  revision: number;
}

/** A claim as authored by hand: a loose polygon, not a cell list. */
export interface TerritoryClaim {
  id: string;
  /** Polygon in light-year space. Deliberately imprecise. */
  polygon: { x: number; y: number }[];
}
