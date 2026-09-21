// Theme plumbing. Default nt; `#theme=<name>` in the URL wins for dev
// testing. The engine will push the environment's chosen theme later
// (ro-theme push) - onThemePush is the prepared consumer.

import { createContext, createEffect, onCleanup, createSignal, useContext, type Accessor, type ParentProps } from "solid-js";
import "./tokens.css";

type ThemeName = "nt" | "syndi";

/** Theme-id -> CSS class map: the set of known page theme classes must
 * match tokens.css entries; server ids map to their PirateWebTheme. */
const THEME_FROM_ID: Record<string, ThemeName> = {
  PirateNtWeb: "nt",
  PirateSyndiWeb: "syndi",
};

interface ThemeCtxValue { theme: () => ThemeName; }
const ThemeCtx = createContext<ThemeCtxValue>();

export function useTheme(): () => ThemeName {
  return useContext(ThemeCtx)!.theme;
}

function bootstrapTheme(): ThemeName {
  try {
    const m = /(?:^|[#&])theme=([^&#\s]+)/.exec(decodeURIComponent(location.hash));
    if (m) {
      const v = m[1].toLowerCase();
      if (v === "syndicate" || v === "syndi") return "syndi";
      return "nt";
    }
  } catch { /* ignore */ }
  return "nt";
}

let pushListenerInstalled = false;

export function onThemePush(): void {
  if (pushListenerInstalled) return;
  pushListenerInstalled = true;
  window.addEventListener("tui-push", (ev) => {
    const detail = (ev as CustomEvent).detail ?? {};
    if ((detail.name ?? "") !== "ui-theme") return;
    applyThemeId(String((detail.payload as { theme?: string })?.theme ?? "nt"));
  });
}

const [theme, setTheme] = createSignal<ThemeName>(bootstrapTheme());

/** Apply a server-emitted theme id (PirateWebTheme proto); unknown ids
 * fall back to nt. */
export function applyThemeId(id: string | undefined): void {
  if (!id) return;
  setTheme(THEME_FROM_ID[id] ?? "nt");
}
export const themeName = theme;

/** Provider applies the theme class so CSS vars + watermark cascade. */
export function ThemeProvider(props: ParentProps): ParentProps["children"] {
  onThemePush();
  createEffect(() => {
    const cls = `theme-${theme()}`;
    document.documentElement.classList.remove("theme-nt", "theme-syndi");
    document.documentElement.classList.add(cls);
    onCleanup(() => document.documentElement.classList.remove(cls));
  });
  return <ThemeCtx.Provider value={{ theme }}>{props.children}</ThemeCtx.Provider>;
}
