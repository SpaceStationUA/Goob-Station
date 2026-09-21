// Shared typed contract between the C# client systems and the pages.
//
// Mirrors the events/actions in Content.Pirate.Client/Radio:
//   PirateRadioClientSystem.OnAction  -> "playeraction" list below
//   WebRadioDriver.SetCatalog/SetState -> pushes below
// Keep both sides in sync; actions may carry data as JSON or raw strings.

import { postAction, onPush } from "./bridge";

export type Playing = boolean;

/** Station entry from the server catalog (mirrors PirateRadioStationPrototype
 * + pin fields; Relayed makes the server transcode to webm/opus in advance). */
export interface StationEntry {
  id: string;
  label: string;
  genre: string;
  url: string;
  featured: boolean;
  relay: boolean;
}

export interface RadioCatalog {
  /// PirateWebTheme id (entity's PirateWebUiThemeComponent); pages map
  /// ids to CSS theme classes with applyThemeId (see lib/theme.tsx).
  theme?: string;
  /** ids the device may switch to (server-gated list). */
  themes?: string[];
  stations: StationEntry[];
}

/** Current playing state pushed by the playback system. */
export interface RadioState {
  stationId: string;
  playing: boolean;
  relay: boolean; // true -> server is transcoding; expect relay-chunk push
}

/** Relay chunk push: base64-encoded webm/opus, order-sensitive. */
export type RelayChunk = string;

/** Player actions (page -> engine). The engine parses `{"id":"..."}`
 * with a minimal tokenizer (PirateRadioClientSystem.ExtractStationId). */
export function dbg(msg: string): Promise<unknown> {
  return postAction("dbg", String(msg));
}

export function playerAction(kind: "play" | "stop" | "relayready" | "volume" | "ready" | "theme", stationId?: string, volume?: number, themeId?: string): Promise<unknown> {
  if (kind === "play")
      return postAction(kind, { id: stationId ?? "" });
  if (kind === "volume")
      return postAction(kind, { v: volume ?? 0 });
  if (kind === "theme")
      return postAction(kind, { theme: themeId ?? "" });
  return postAction(kind, {});
}

/** Register handlers for the catalog/state/relay pushes. */
export function onRadioCatalog(handler: (c: RadioCatalog) => void): void {
  onPush((name, payload) => {
    if (name === "radio-catalog") handler(payload as RadioCatalog);
  });
}

export function onRadioState(handler: (s: RadioState) => void): void {
  onPush((name, payload) => {
    if (name === "radio-state") handler(payload as RadioState);
  });
}

export function onRelayChunk(handler: (b64: RelayChunk) => void): void {
  onPush((name, payload) => {
    if (name === "radio-relay-chunk") handler(payload as RelayChunk);
  });
}
