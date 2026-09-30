import type { Route, StarSystem, Territory, TerritoryClaim } from "./model";

/**
 * Placeholder content for the browser harness.
 *
 * Shaped like the Spur we actually have — a central power, a large northern
 * federation, a coalition to one side, and a lot of unclaimed frontier — so
 * the visuals get judged against a realistic spread of territory sizes rather
 * than three tidy blobs. Replaced by real content once the bake lands.
 */

const p = (x: number, y: number) => ({ x, y });

export const TERRITORIES: Territory[] = [
  {
    id: "biesel",
    name: "REPUBLIC OF BIESEL",
    color: "#3f6fd8",
    pattern: "grid",
    blurb: "Tau Ceti and its annexations. Formon, now nominally sovereign.",
  },
  {
    id: "nralakk",
    name: "NRALAKK FEDERATION",
    color: "#2f9e5c",
    pattern: "crosshatch",
    blurb: "The Skrell homeworld and the Traverse beyond it.",
  },
  {
    id: "coalition",
    name: "COALITION OF COLONIES",
    color: "#31b0c4",
    pattern: "hatch",
    blurb: "Liberty's Cradle and the Weeping Stars.",
  },
  {
    id: "solarian",
    name: "SOLARIAN ALLIANCE",
    color: "#b9c2cc",
    pattern: "horizontal",
    blurb: "A shrinking core. Earth, Mars, the Jewel Worlds.",
  },
  {
    id: "adhomai",
    name: "PEOPLE'S REPUBLIC OF ADHOMAI",
    color: "#c2419a",
    pattern: "dots",
    blurb: "Frontier world turned regional power.",
  },
  {
    id: "elyra",
    name: "REPUBLIC OF ELYRA",
    color: "#d8a72b",
    pattern: "vertical",
    blurb: "Persepolis and the Badlands trade.",
  },
  {
    id: "unclaimed",
    name: "UNCLAIMED",
    color: "#4a5568",
    pattern: "solid",
    unclaimed: true,
    blurb: "Wild space. Nobody's problem, yet.",
  },
];

/**
 * Hand-drawn claims. Deliberately loose — this is tier 1, the intent. The
 * baker turns these into cells, and only the cells are ever shown.
 *
 * Two rules, and `npm run check` enforces the second:
 *   - claims MAY overlap. Hand-drawn outlines cannot tile perfectly, and a
 *     fraction-of-a-light-year gap is worse than useless: one unclaimed cell in
 *     it severs the shared border along its whole length. Overlap is resolved
 *     per cell in favour of the claim the cell sits deepest inside, which splits
 *     a contested band cleanly down the middle.
 *   - cells no claim covers are unclaimed space. That is the frontier, and it is
 *     meant to be large.
 */
export const CLAIMS: TerritoryClaim[] = [
  {
    id: "solarian",
    polygon: [
      p(-11, 9), p(-5.5, 18), p(4, 19.5), p(11, 12.5), p(9.5, 4), p(2, -0.5), p(-9, 2),
    ],
  },
  {
    // Overlaps Solarian along its north-west edge, so the two share a border
    // and the contested band between them exercises the depth rule.
    id: "biesel",
    polygon: [
      p(10.2, 5.2), p(15, 4), p(24, 0.5), p(28, -9), p(20.5, -15.5), p(11, -14), p(3.2, 0.8),
    ],
  },
  {
    id: "nralakk",
    polygon: [
      p(-58, 3.5), p(-42.5, 22), p(-25, 24.5), p(-17, 11), p(-22.5, -5.5), p(-37, -17), p(-53.5, -10),
    ],
  },
  {
    id: "coalition",
    polygon: [
      p(20, 26.5), p(39, 30), p(55, 18.5), p(53, 4.5), p(39, -1), p(26, 6), p(18.5, 16),
    ],
  },
  {
    // Reaches up into Biesel's southern edge.
    id: "adhomai",
    polygon: [
      p(-17, -32.5), p(2, -28), p(13.4, -20), p(11.4, -12.6), p(-2, -15.5), p(-16, -21.5),
    ],
  },
  {
    // Reaches up into Biesel's south-east corner.
    id: "elyra",
    polygon: [
      p(25.4, -10), p(43, -16), p(52, -26), p(43, -34), p(28, -31), p(19.4, -14.4),
    ],
  },
];

const sys = (
  id: string,
  name: string,
  xLy: number,
  yLy: number,
  kind: StarSystem["kind"],
  importance: StarSystem["importance"],
  territory: string,
): StarSystem => ({ id, name, xLy, yLy, kind, importance, territory });

export const SYSTEMS: StarSystem[] = [
  sys("tau-ceti", "Tau Ceti", 17, -5, "star", 3, "biesel"),
  sys("mictlan", "Mictlan", 22, -11, "planet", 2, "biesel"),
  sys("port-antilla", "Port Antilla", 12, -10, "station", 1, "biesel"),
  sys("qerrbalak", "Qerrbalak", -30, 9, "star", 3, "nralakk"),
  sys("xanu", "Xanu", -41, 13, "planet", 2, "nralakk"),
  sys("himeo", "Himeo", -50, 3, "planet", 1, "nralakk"),
  sys("vysoka", "Vysoka", -26, -1, "planet", 1, "nralakk"),
  sys("tattuqig", "Tattuqig", -46, -9, "outpost", 0, "nralakk"),
  sys("persepolis", "Persepolis", 39, 19, "star", 3, "coalition"),
  sys("gadpathur", "Gadpathur", 29, 21, "planet", 1, "coalition"),
  sys("burzsia", "Burzsia", 50, 15, "planet", 2, "coalition"),
  sys("meropis", "Meropis", 52, 7, "planet", 0, "coalition"),
  sys("sol", "Sol", -2, 11, "star", 3, "solarian"),
  sys("earth", "Earth", 3, 5, "planet", 2, "solarian"),
  sys("mars", "Mars", -6, 12, "planet", 1, "solarian"),
  sys("epsilon-eridani", "Epsilon Eridani", 7, 8, "star", 2, "solarian"),
  sys("adhomai", "Adhomai", -1, -24, "star", 3, "adhomai"),
  sys("hrozamal", "Hro'zamal", 8, -25, "planet", 1, "adhomai"),
  sys("moghes", "Moghes", -45, -27, "star", 2, ""),
  sys("sunreach", "Sunreach", -33, -24, "planet", 0, ""),
  sys("assunzione", "Assunzione", 37, -21, "star", 2, "elyra"),
  sys("valley-hale", "Valley Hale", 28, -19, "outpost", 0, "elyra"),
  sys("harradon", "Harradon", 32, -29, "planet", 0, "elyra"),
];

export const ROUTES: Route[] = [
  { from: "tau-ceti", to: "persepolis", kind: "gate", allowed: ["biesel", "elyra"] },
  { from: "qerrbalak", to: "epsilon-eridani", kind: "gate", allowed: ["nralakk", "solarian"] },
  { from: "sol", to: "tau-ceti", kind: "gate", allowed: [] },
  { from: "adhomai", to: "moghes", kind: "hyperlane", allowed: [] },
  { from: "persepolis", to: "burzsia", kind: "hyperlane", allowed: [] },
];
