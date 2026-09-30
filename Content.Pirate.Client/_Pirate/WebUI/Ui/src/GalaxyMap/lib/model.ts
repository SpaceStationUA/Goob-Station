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

/**
 * A name in every language we ship.
 *
 * The page resolves these itself rather than being handed a finished string,
 * because the client locale is a client-side preference: the bridge pushes the
 * text and the player's locale, and the page picks. Sending one pre-resolved
 * name would mean a locale change needs a round trip to the server.
 */
export interface LocalizedText {
  en: string;
  /** Omit to fall back to English. */
  uk?: string;
}

// `pick` itself lives in lib/i18n next to the rest of the localisation, so that
// everything which resolves a string for a locale does it the same way. Re-
// exported because model data is the other thing that carries names.
export { pick } from "./i18n";

export interface Territory {
  id: string;
  name: LocalizedText;
  /** Hex colour, "#rrggbb". */
  color: string;
  pattern: PatternId;
  /** Optional short descriptor shown in the side panel. */
  blurb?: LocalizedText;
  /** True for the automatic "everything nobody claimed" remainder. */
  unclaimed?: boolean;
}

export type SystemKind = "star" | "planet" | "station" | "gate" | "outpost";

/** 0 = minor, 3 = capital. Drives marker size and ring treatment. */
export type Importance = 0 | 1 | 2 | 3;

export interface StarSystem {
  id: string;
  name: LocalizedText;
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

/**
 * Cell keys where sovereignty is disputed.
 *
 * Seeded by the bake from cells that two hand-drawn claims both cover — the
 * depth rule picks a winner so the map has a definite owner, but the fact that
 * it was a coin toss is real information and belongs on the chart. Admins can
 * add to the set in-round, which is how a simmering row gets marked before
 * anyone moves a capital.
 */
export type Contested = Set<string>;

export interface GalaxyModel {
  /** Map extent in light-years. The chart frame and the graticule use this. */
  extentLy: { w: number; h: number };
  /** Centre-to-corner radius of one cell, in light-years. */
  hexSizeLy: number;
  territories: Territory[];
  systems: StarSystem[];
  routes: Route[];
  ownership: Ownership;
  contested: Contested;
  /** Bumped whenever ownership changes, so the view can key its caches. */
  revision: number;
}

/** A claim as authored by hand: a loose polygon, not a cell list. */
export interface TerritoryClaim {
  id: string;
  /** Polygon in light-year space. Deliberately imprecise. */
  polygon: { x: number; y: number }[];
}
