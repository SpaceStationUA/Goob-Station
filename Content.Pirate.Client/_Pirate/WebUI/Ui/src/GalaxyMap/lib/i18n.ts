/**
 * UI strings, and the seam the game will replace.
 *
 * Territory and system names are DATA — they come from the bridge, already
 * localised. The chrome around them is different: it belongs to this page, and
 * there are only a few dozen of them. They live here.
 *
 * ## Why the table is page-side, and what changes in game
 *
 * Two things pull in opposite directions. The game already owns localisation:
 * 2600+ `.ftl` files, and `nationality_name_bieselite` is right there. Putting
 * the chart's chrome in new `.ftl` entries means translators work in the tool
 * they already use and there is exactly one source of truth.
 *
 * But a page cannot read `.ftl` — no filesystem, no Fluent parser. So either the
 * page carries its own table and the game's copy becomes a duplicate that
 * drifts, or the bridge resolves the strings and pushes them.
 *
 * This module is written for the second and currently satisfies the first.
 * `installStrings()` replaces the built-in table, `setLocales()` replaces the
 * locale list, so a `BridgeSource` hands over the game's strings and nothing
 * below this line changes. The built-in table is the browser harness's fallback
 * and the shape the C# side should serialise.
 *
 * Locale codes are the game's (`en-US`, `uk-UA`), not bare `en`/`uk`, so nothing
 * has to be renamed at the boundary.
 */
import type { LocalizedText } from "./model";

export type LocaleId = string;

export interface LocaleOption {
  id: LocaleId;
  /**
   * The language's name in that language — an endonym, never translated.
   *
   * This is the one label in the UI that must NOT follow the active locale. A
   * player hunting for their own language scans for the script they recognise,
   * so "УКР" has to read "УКР" even while the interface is in English. Rendering
   * it as "UK" in an English session hides the one word the person is looking
   * for behind a pair of Latin letters.
   */
  label: string;
}

/** Locales offered in the picker. The bridge replaces this wholesale. */
export const DEFAULT_LOCALES: LocaleOption[] = [
  { id: "en-US", label: "EN" },
  { id: "uk-UA", label: "УКР" },
];

/**
 * A count-dependent string, one entry per CLDR plural category.
 *
 * Ukrainian needs all three and getting it wrong is instantly visible to a
 * reader: 1 клітинка, 2 клітинки, 5 клітинок. English needs two. Writing
 * "клітинки" and hoping is how you ship "2 клітинки" next to "5 клітинки".
 */
export interface PluralText {
  one: LocalizedText;
  few?: LocalizedText;
  many: LocalizedText;
}

export interface UiStrings {
  /* chrome */
  grid: LocalizedText;
  paint: LocalizedText;
  undo: LocalizedText;
  close: LocalizedText;
  showNamesIn: LocalizedText;

  /* brushes */
  brushPainting: LocalizedText;
  brushUnclaimed: LocalizedText;
  brushContesting: LocalizedText;
  brushClearingContest: LocalizedText;
  brushHint: LocalizedText;
  tipUnclaim: LocalizedText;
  tipContest: LocalizedText;
  tipUncontest: LocalizedText;

  /* side panel */
  sovereignTerritory: LocalizedText;
  unclaimedSpace: LocalizedText;
  labelCells: LocalizedText;
  labelSystems: LocalizedText;
  labelCapitals: LocalizedText;
  labelContested: LocalizedText;

  /* legend */
  orionSpur: LocalizedText;
  extent: LocalizedText;
  legendContested: PluralText;
  legendEdits: PluralText;

  /* counts */
  pluralCell: PluralText;
  pluralContested: PluralText;
  pluralEdit: PluralText;
  pluralSystem: PluralText;
}

export const DEFAULT_STRINGS: UiStrings = {
  grid: { en: "GRID", uk: "СІТКА" },
  paint: { en: "PAINT", uk: "ФАРБА" },
  undo: { en: "UNDO", uk: "СКАСУВАТИ" },
  close: { en: "CLOSE", uk: "ЗАКРИТИ" },
  showNamesIn: { en: "Show names in {$locale}", uk: "Показати назви мовою {$locale}" },

  brushPainting: { en: "PAINTING {$name}", uk: "ФАРБУЄМО: {$name}" },
  brushUnclaimed: { en: "PAINTING UNCLAIMED SPACE", uk: "ФАРБУЄМО: НЕЗАЙНЯТІ СХОДИ" },
  brushContesting: { en: "MARKING CONTESTED", uk: "ПОЗНАЧАЄМО СПІРНУ ДІЛЯНКУ" },
  brushClearingContest: {
    en: "CLEARING CONTESTED",
    uk: "ЗНИМАЄМО ПОЗНАЧКУ СПОРУ",
  },
  brushHint: { en: "— click cells, ESC to stop", uk: "— клацніть клітинки, ESC для виходу" },
  tipUnclaim: {
    en: "Return the cell to unclaimed space",
    uk: "Повернути клітинку до незайнятих сходу",
  },
  tipContest: {
    en: "Mark the cell as contested — two claims, one owner",
    uk: "Позначити клітинку як спірну — дві претензії, один власник",
  },
  tipUncontest: {
    en: "Settle the dispute — the owner is not in question",
    uk: "Зняти позначку спору — власник не змінюється",
  },

  sovereignTerritory: { en: "SOVEREIGN TERRITORY", uk: "СУВЕРЕННА ТЕРИТОРІЯ" },
  unclaimedSpace: { en: "UNCLAIMED SPACE", uk: "НЕЗАЙНЯТІ СХОДИ" },
  labelCells: { en: "CELLS", uk: "КЛІТИНКИ" },
  labelSystems: { en: "SYSTEMS", uk: "СИСТЕМИ" },
  labelCapitals: { en: "CAPITALS", uk: "СТОЛИЦІ" },
  labelContested: { en: "CONTESTED", uk: "СПІРНІ" },

  orionSpur: { en: "ORION SPUR", uk: "РУКАВ ОРІОНА" },
  extent: {
    en: "{$w} × {$h} LY · {$cells} cells @ {$size} LY",
    uk: "{$w} × {$h} св.р. · {$cells} клітинок по {$size} св.р.",
  },
  legendContested: {
    one: { en: "{$n} contested", uk: "{$n} спірна клітинка" },
    few: { en: "{$n} contested", uk: "{$n} спірні клітинки" },
    many: { en: "{$n} contested", uk: "{$n} спірних клітинок" },
  },
  legendEdits: {
    one: { en: "{$n} local edit", uk: "{$n} локальна зміна" },
    few: { en: "{$n} local edits", uk: "{$n} локальні зміни" },
    many: { en: "{$n} local edits", uk: "{$n} локальних змін" },
  },

  pluralCell: {
    one: { en: "{$n} cell", uk: "{$n} клітинка" },
    few: { en: "{$n} cells", uk: "{$n} клітинки" },
    many: { en: "{$n} cells", uk: "{$n} клітинок" },
  },
  pluralContested: {
    one: { en: "{$n} contested", uk: "{$n} спірна" },
    few: { en: "{$n} contested", uk: "{$n} спірні" },
    many: { en: "{$n} contested", uk: "{$n} спірних" },
  },
  pluralEdit: {
    one: { en: "{$n} local edit", uk: "{$n} локальна зміна" },
    few: { en: "{$n} local edits", uk: "{$n} локальні зміни" },
    many: { en: "{$n} local edits", uk: "{$n} локальних змін" },
  },
  pluralSystem: {
    one: { en: "{$n} system", uk: "{$n} система" },
    few: { en: "{$n} systems", uk: "{$n} системи" },
    many: { en: "{$n} systems", uk: "{$n} систем" },
  },
};

export type PluralCategory = "one" | "few" | "many";

/**
 * CLDR plural category for a count.
 *
 * Only the rules the shipped locales need. Ukrainian is the awkward one: "few" is
 * not simply 2-4, it is "last digit 2-4 AND last two digits NOT 12-14". So
 * 2 клітинки and 22 клітинки, but 12 клітинок and 14 клітинок.
 */
export function pluralCategory(n: number, locale: LocaleId): PluralCategory {
  if (!isUkrainian(locale)) return n === 1 ? "one" : "many";
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return "one";
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return "few";
  return "many";
}

export function pick(text: LocalizedText | undefined, locale: LocaleId): string {
  if (!text) return "";
  // Take the translation only when the locale actually IS that language.
  // Testing "not English" instead would hand Ukrainian to a German client, which
  // is worse than useless — it looks like a working translation. Anything we
  // have not translated falls back to English rather than to a blank label.
  if (isUkrainian(locale) && text.uk) return text.uk;
  return text.en ?? "";
}

export function isUkrainian(locale: LocaleId): boolean {
  return locale.toLowerCase().startsWith("uk");
}

/** Substitute `{$name}` placeholders. Unknown placeholders are left alone. */
export function fill(text: string, args: Record<string, string | number>): string {
  return text.replace(/\{\$(\w+)\}/g, (whole, k) => (k in args ? String(args[k]) : whole));
}

/* ------------------------------------------------------------------ *
 * Runtime state + the bridge seam
 * ------------------------------------------------------------------ */

let strings: UiStrings = DEFAULT_STRINGS;
let locales: LocaleOption[] = DEFAULT_LOCALES;

export function currentStrings(): UiStrings {
  return strings;
}

export function currentLocales(): LocaleOption[] {
  return locales;
}

/** Look up a plain (non-count) string. */
export function t(key: keyof UiStrings, locale: LocaleId, args?: Record<string, string | number>): string {
  const entry = strings[key] as LocalizedText | undefined;
  const text = pick(entry, locale);
  return args ? fill(text, args) : text;
}

/** Look up a count-dependent string and apply the right plural form. */
export function tp(key: keyof UiStrings, n: number, locale: LocaleId): string {
  const entry = strings[key] as PluralText | undefined;
  const cat = pluralCategory(n, locale);
  const form = entry?.[cat] ?? entry?.many ?? entry?.one;
  return fill(pick(form, locale), { n });
}

/** Replace the table, e.g. with strings the game resolved from its own locale. */
export function installStrings(next: Partial<UiStrings>): void {
  strings = { ...DEFAULT_STRINGS, ...next };
}

export function setLocales(next: LocaleOption[]): void {
  locales = next.length > 0 ? next : DEFAULT_LOCALES;
}

/** Restore the built-ins. Used by the browser harness and by the checks. */
export function resetStrings(): void {
  strings = DEFAULT_STRINGS;
  locales = DEFAULT_LOCALES;
}

/**
 * Keys with no translation for `locale`. Should always be empty.
 *
 * Plural entries are checked form by form. A missing "few" is a real hole: it is
 * the form that shows up on 2, 3 and 22, which is most of the counts anyone
 * reads.
 */
export function missingTranslations(locale: LocaleId): string[] {
  const out: string[] = [];
  for (const [k, v] of Object.entries(strings)) {
    if (!v) {
      out.push(k);
      continue;
    }
    const anyPlural = "one" in v || "many" in v;
    if (anyPlural) {
      const forms = [v.one, v.few, v.many].filter(Boolean) as LocalizedText[];
      // "few" is optional — it is meaningless in a two-form language.
      const required = isUkrainian(locale) ? forms : [v.one, v.many].filter(Boolean);
      if (required.length < 2 || required.some(f => !pick(f, locale))) out.push(k);
    } else if (!pick(v as LocalizedText, locale)) {
      out.push(k);
    }
  }
  return out;
}
