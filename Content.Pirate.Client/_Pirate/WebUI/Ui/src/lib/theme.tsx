// Theme plumbing. Default nt; `#theme=<name>` in the URL wins for dev
// testing. The engine will push the environment's chosen theme later
// (ro-theme push) - onThemePush is the prepared consumer.

import { createContext, createSignal, useContext, type Accessor, type ParentProps } from "solid-js";
import "./tokens.css";

type ThemeName = "nt" | "syndi";

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
    setTheme((((detail.payload as { theme?: string })?.theme ?? "nt") === "syndi" || (((detail.payload as { theme?: string })?.theme ?? "nt") === "syndicate")) ? "syndi" : "nt");
  });
}

const [theme, setTheme] = createSignal<ThemeName>(bootstrapTheme());
export const themeName = theme;

/** Provider applies the theme class so CSS vars + watermark cascade. */
export function ThemeProvider(props: ParentProps): typeof props.children {
  onThemePush();
  document.documentElement.classList.remove("theme-nt", "theme-syndi");
  document.documentElement.classList.add(`theme-${theme()}`);
  return <ThemeCtx.Provider value={{ theme }}>{props.children}</ThemeCtx.Provider>;
}
