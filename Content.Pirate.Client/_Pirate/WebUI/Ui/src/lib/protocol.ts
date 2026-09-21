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
  stations: StationEntry[];
}

/** Current playing state pushed by the playback system. */
export interface RadioState {
  stationId: string;
  playing: boolean;
  relay: boolean; // true -> server is transcoding; expect relay-chunk push
}

export type RelayChunk = {
  // base64-encoded webm/opus chunk from the ffmpeg relay pump
  chunkB64: string;
};

export type PlayerAction =
  | { type: "play" } // play/pause/stop source id? (see send code)
  | { type: "report_proc" } // hearing loop thread report
  | { type: "send_stdout" };

/** Player button-bar actions (page -> engine). */
export function playerAction(kind: "play" | "stop" | "relayready" | "volume", stationId?: string, volume?: number): Promise<unknown> {
  // The engine parses the raw fields tolerantly; see
  // PirateRadioClientSystem.ExtractStationId.
  return postAction(kind, stationId ?? (volume !== undefined ? `v=${volume}` : undefined));
}

/** Register handlers for the catalog/state pushes. */
export function onRadioCatalog(handler: (c: RadioCatalog) => void): void {
  onPush("radio-catalog", (payload) => handler(payload as RadioCatalog));
}

export function onRadioState(handler: (s: RadioState) => void): void {
  onPush("radio-state", (payload) => handler(payload as RadioState));
}

export function onRelayChunk(handler: (chunk: RelayChunk) => void): void {
  onPush("radio-relay-chunk", (payload) => handler(payload as RelayChunk));
}
