import type { LocalizedText, Route, StarSystem, Territory, TerritoryClaim } from "./model";

/**
 * Placeholder content for the browser harness.
 *
 * The nations here are the real ones — they match
 * `Resources/Prototypes/_Pirate/Contractors/nationality.yml` and their ids line
 * up with the `nationality_name_*` locale keys — so that switching between
 * prototype data and this fixture is a rename away rather than a rewrite.
 * GoldenDeep is left out only to keep the map readable; it is a seventh nation
 * and adds nothing to judging the visuals.
 *
 * Star positions and the polygon shapes are still invented. That is deliberate
 * for now: this file exists to be art-directed, and the real content arrives
 * with the bake.
 *
 * Ukrainian names come from `Resources/Locale/uk-UA/_Pirate/contractors/
 * nationality.ftl`. That file holds GENITIVE forms — "Республіки Бізеліт" reads
 * as "of the Republic of Biesel", because the locale slot it was written for
 * follows a label like "Національність:". A map label stands alone and needs
 * the nominative, so the forms below are converted by hand:
 * "Республіки Бізеліт" -> "Республіка Бізеліт". Worth a native speaker's eye.
 *
 * SYSTEM names are transliterations, not translations — the repo has no locale
 * entries for them. They need the maintainer to confirm before this ships.
 */

const p = (x: number, y: number) => ({ x, y });
const L = (en: string, uk: string): LocalizedText => ({ en, uk });

export const TERRITORIES: Territory[] = [
  {
    id: "biesel",
    name: L("REPUBLIC OF BIESEL", "РЕСПУБЛІКА БІЗЕЛІТ"),
    color: "#3f6fd8",
    pattern: "grid",
    blurb: L(
      "Tau Ceti and its annexations. Formon, now nominally sovereign.",
      "Тау Цеті та її приєднання. Формон — нибито суверенний.",
    ),
  },
  {
    id: "nralakk",
    name: L("NRALAKK FEDERATION", "ФЕДЕРАЦІЯ НРАЛЛАК"),
    color: "#2f9e5c",
    pattern: "crosshatch",
    blurb: L("The Skrell homeworld and the Traverse beyond it.", "Скрелльська ріджина та Траєрвер за нею."),
  },
  {
    id: "solarian",
    name: L("SOLARIAN ALLIANCE", "СОЛАРІАНСЬКИЙ АЛЬЯНС"),
    color: "#b9c2cc",
    pattern: "horizontal",
    blurb: L("A shrinking core. Earth, Mars, the Jewel Worlds.", "Скорочуває ядро. Земля, Марс, Перлинові Світи."),
  },
  {
    id: "eridian",
    name: L("ERIDANIAN FEDERATION", "ФЕДЕРАЦІЯ ЕРІДАНУ"),
    color: "#31b0c4",
    pattern: "hatch",
    blurb: L("Corporate holdings strung along the Tradeband.", "Корпоративні володіння вздовж Трейдбанду."),
  },
  {
    id: "adhomai",
    name: L("PEOPLE'S REPUBLIC OF ADHOMAI", "НАРОДНА РЕСПУБЛІКА АДХОМАЙ"),
    color: "#c2419a",
    pattern: "dots",
    blurb: L("Frontier world turned regional power.", "Прикордонний світ, що став регіональною силою."),
  },
  {
    id: "izweski",
    name: L("IZWESKI HEGEMONY", "ІЗВЕСКІ-ГЕГЕМОНІЯ"),
    color: "#d8a72b",
    pattern: "vertical",
    blurb: L("Old empire, and it would like its borders back.", "Стара імперія, і вона хоче свої кордони назад."),
  },
  {
    id: "unclaimed",
    name: L("UNCLAIMED SPACE", "НЕЗАЙНЯТІ СХОДИ"),
    color: "#4a5568",
    pattern: "solid",
    unclaimed: true,
    blurb: L("Wild space. Nobody's problem, yet.", "Дикече поле. Поки що не чиїсь проблема."),
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
 *   - cells no claim covers are unclaimed space. That is the frontier, and it
 *     is meant to be large.
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
    id: "eridian",
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
    id: "izweski",
    polygon: [
      p(25.4, -10), p(43, -16), p(52, -26), p(43, -34), p(28, -31), p(19.4, -14.4),
    ],
  },
];

const sys = (
  id: string,
  name: LocalizedText,
  xLy: number,
  yLy: number,
  kind: StarSystem["kind"],
  importance: StarSystem["importance"],
  territory: string,
): StarSystem => ({ id, name, xLy, yLy, kind, importance, territory });

export const SYSTEMS: StarSystem[] = [
  sys("tau-ceti", L("Tau Ceti", "Тау Цеті"), 17, -5, "star", 3, "biesel"),
  sys("mictlan", L("Mictlan", "Міктлан"), 22, -11, "planet", 2, "biesel"),
  sys("port-antilla", L("Port Antilla", "Порт Антілья"), 12, -10, "station", 1, "biesel"),
  sys("qerrbalak", L("Qerrbalak", "Керрбалак"), -30, 9, "star", 3, "nralakk"),
  sys("xanu", L("Xanu", "Ксану"), -41, 13, "planet", 2, "nralakk"),
  sys("himeo", L("Himeo", "Гімео"), -50, 3, "planet", 1, "nralakk"),
  sys("vysoka", L("Vysoka", "Висока"), -26, -1, "planet", 1, "nralakk"),
  sys("tattuqig", L("Tattuqig", "Таттуквіг"), -46, -9, "outpost", 0, "nralakk"),
  sys("persepolis", L("Persepolis", "Персеполіс"), 39, 19, "star", 3, "eridian"),
  sys("gadpathur", L("Gadpathur", "Гадпатур"), 29, 21, "planet", 1, "eridian"),
  sys("burzsia", L("Burzsia", "Бурзія"), 50, 15, "planet", 2, "eridian"),
  sys("meropis", L("Meropis", "Меропіс"), 52, 7, "planet", 0, "eridian"),
  sys("sol", L("Sol", "Соль"), -2, 11, "star", 3, "solarian"),
  sys("earth", L("Earth", "Земля"), 3, 5, "planet", 2, "solarian"),
  sys("mars", L("Mars", "Марс"), -6, 12, "planet", 1, "solarian"),
  sys("epsilon-eridani", L("Epsilon Eridani", "Епсилон Ерідани"), 7, 8, "star", 2, "solarian"),
  sys("adhomai", L("Adhomai", "Адхомай"), -1, -24, "star", 3, "adhomai"),
  sys("hrozamal", L("Hro'zamal", "Хро'замаль"), 8, -25, "planet", 1, "adhomai"),
  sys("moghes", L("Moghes", "Моггес"), -45, -27, "star", 2, ""),
  sys("sunreach", L("Sunreach", "Санріч"), -33, -24, "planet", 0, ""),
  sys("assunzione", L("Assunzione", "Ассунціоне"), 37, -21, "star", 2, "izweski"),
  sys("valley-hale", L("Valley Hale", "Валлі Гейл"), 28, -19, "outpost", 0, "izweski"),
  sys("harradon", L("Harradon", "Гаррадон"), 32, -29, "planet", 0, "izweski"),
];

export const ROUTES: Route[] = [
  { from: "tau-ceti", to: "persepolis", kind: "gate", allowed: ["biesel", "eridian"] },
  { from: "qerrbalak", to: "epsilon-eridani", kind: "gate", allowed: ["nralakk", "solarian"] },
  { from: "sol", to: "tau-ceti", kind: "gate", allowed: [] },
  { from: "adhomai", to: "moghes", kind: "hyperlane", allowed: [] },
  { from: "persepolis", to: "burzsia", kind: "hyperlane", allowed: [] },
];
