// Starter UI kit: atoms + window shell built strictly on the theme tokens.
// Components are deliberately dumb: styling from kit.css (vars only), no
// domain logic, no imports from page code. Pages compose them as needed.

import { Show, type JSX } from "solid-js";
import "./kit.css";

/** Phosphor icon (solid-icons/ph): `<Icon p={Play} />`; inherits color. */
export function Icon(props: { p: JSX.Element }): JSX.Element {
  return <span class="pk-icon">{props.p}</span>;
}

/** Button: variant accent | plain | danger; disabled state respected. */
export function Button(props: {
  variant?: "accent" | "danger";
  onClick?: (e: MouseEvent) => void;
  disabled?: boolean;
  class?: string;
  children: JSX.Element;
}): JSX.Element {
  const cls = () =>
    `pk-btn pk-${props.variant ?? "plain"}${props.class ? " " + props.class : ""}` +
    (props.disabled ? " pk-disabled" : "");
  return (
    <button class={cls()} onClick={(e) => !props.disabled && props.onClick?.(e)}>
      {props.children}
    </button>
  );
}

/** Panel: section container with optional heading. */
export function Panel(props: { title?: string; children: JSX.Element }): JSX.Element {
  return (
    <div class="pk-panel">
      {props.title ? <div class="pk-panel-title">{props.title}</div> : null}
      {props.children}
    </div>
  );
}

/** Horizontal meter; color crosses into danger past the fraction. */
export function ProgressBar(props: { value: number; max: number; danger?: number }): JSX.Element {
  const pct = () => Math.max(0, Math.min(100, (props.value / props.max) * 100));
  const dang = () => props.danger !== undefined && pct() <= props.danger;
  return (
    <div class="pk-meter" role="progressbar" aria-valuenow={pct()}>
      <div class={"pk-meter-fill" + (dang() ? " pk-meter-danger" : "")} style={{ width: `${pct()}%` }} />
    </div>
  );
}

/** Window shell: themed watermark + title bar + optional close. */
export function GameWindow(props: {
  // Title is optional: the PDA nav bar already shows the program name,
  // so pages hosted there skip the in-page header entirely.
  title?: string;
  onClose?: () => void;
  titlebar?: JSX.Element; // extra widgets right-aligned before close
  children: JSX.Element;
}): JSX.Element {
  return (
    <div class="pk-window">
      <Show when={props.title || props.titlebar}>
      <div class="pk-titlebar">
        <span class="pk-title">{props.title}</span>
        {props.titlebar}
        <div class="pk-titlebar-spacer" />
        <Show when={props.onClose}>
          <button class="pk-btn pk-close" onClick={() => props.onClose?.()}>✕</button>
        </Show>
      </div>
      </Show>
      <div class="pk-content">
        <div class="watermark" />
        {props.children}
      </div>
    </div>
  );
}
