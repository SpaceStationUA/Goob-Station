/**
 * Spike: is a pixel planet an improvement on a dot?
 *
 * Throwaway comparison harness. The questions it exists to answer are
 * aesthetic, so they cannot be answered by reasoning:
 *
 *   - where does the sprite stop working, going down in size
 *   - is the dithered pixel look earning its place, or is a smooth shaded
 *     sphere doing all the work
 *   - which planet types are still distinguishable at map scale
 *   - can the palette be pulled far enough toward a nation colour that the
 *     planet reads as part of the chart instead of a sticker on it
 *   - and the real one: does the map get better or just busier
 *
 * The last one is answered by the PLANETS button, which swaps the markers on
 * the live chart.
 */

import { For, Show, createMemo, createSignal, onCleanup, onMount } from "solid-js";
import {
  PLANET_TYPE_LIST,
  planetUri,
  seedFromId,
  type PlanetType,
} from "./lib/planet";
import type { StarSystem } from "./lib/model";

/** Same sizes the map would realistically use, plus a couple above them. */
const SIZES = [12, 16, 24, 48, 96];
const SEEDS = [1, 2, 3, 4, 5, 6];

/**
 * Device pixel ratio, reactively.
 *
 * Two reasons this exists rather than a plain read. Under CEF the browser can
 * be zoomed, and a raster sprite generated at the old ratio silently blurs
 * under a scale — the same class of bug as the markers that froze at the
 * initial viewport. And the planet cache is keyed on size, so handing it a
 * stale ratio means it never regenerates at all.
 */
export function useDevicePixelRatio(): () => number {
  const [dpr, setDpr] = createSignal(
    typeof window === "undefined" ? 1 : window.devicePixelRatio || 1,
  );
  onMount(() => {
    let mq: MediaQueryList | null = null;
    const listen = () => {
      mq?.removeEventListener("change", onChange);
      mq = window.matchMedia(`(resolution: ${window.devicePixelRatio}dppx)`);
      mq.addEventListener("change", onChange);
    };
    const onChange = () => {
      setDpr(window.devicePixelRatio || 1);
      listen();
    };
    listen();
    onCleanup(() => mq?.removeEventListener("change", onChange));
  });
  return dpr;
}

function Planet(props: {
  seed: number;
  type: PlanetType;
  px: number;
  dpr?: number;
  dither?: boolean;
  tint?: string;
  tintAmount?: number;
  title?: string;
}) {
  // Built in a memo rather than inline in the JSX, for the same reason the
  // chart does all of its geometry: a reactive read hoisted into a JSX
  // attribute is evaluated once and then frozen.
  const uri = createMemo(() =>
    planetUri({
      seed: props.seed,
      type: props.type,
      px: props.px,
      dpr: props.dpr ?? 1,
      dither: props.dither,
      tint: props.tint,
      tintAmount: props.tintAmount,
    }),
  );
  return (
    <img
      class="pl"
      src={uri()}
      width={props.px}
      height={props.px}
      alt={props.title ?? props.type}
      title={props.title ?? props.type}
    />
  );
}

/** Deterministically spread types across systems, for the live chart. */
function typeForSystem(s: StarSystem): PlanetType {
  if (s.kind === "station" || s.kind === "outpost") return "asteroid";
  return PLANET_TYPE_LIST[Math.abs(seedFromId(s.id)) % PLANET_TYPE_LIST.length];
}

export function Spike(props: {
  systems: StarSystem[];
  ownerColour: (id: string) => string;
  onMap: boolean;
  setOnMap: (v: boolean) => void;
}) {
  // Closed by default. The panel sits over the bottom of the chart, and the
  // pointer checks in check-dom aim at fractions of the chart area — with it
  // open they hit the panel instead, which is a real obstruction, not just a
  // test artefact. Open it when you want to compare; the button is top centre.
  const [open, setOpen] = createSignal(false);
  const dpr = useDevicePixelRatio();

  return (
    <>
      <div class="spikebar">
        <button classList={{ on: open() }} onClick={() => setOpen(v => !v)}>
          PLANETS
        </button>
        <button
          classList={{ on: props.onMap }}
          onClick={() => props.setOnMap(!props.onMap)}
        >
          {props.onMap ? "PLANETS ON" : "PLANETS ON MAP"}
        </button>
      </div>

      <Show when={open()}>
        <div class="spike">
          <div class="spike-head">
            planet spike — disposable
            <button class="spike-x" onClick={() => setOpen(false)}>
              ×
            </button>
          </div>

          <div class="spike-scroll">
            <Group label="size, dithered">
              <For each={SIZES}>
                {px => (
                  <Cell label={`${px}px`}>
                    <Planet seed={0x5eed1} type="terran" px={px} dpr={dpr()} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="same, no dither">
              <For each={SIZES}>
                {px => (
                  <Cell label={`${px}px`}>
                    <Planet seed={0x5eed1} type="terran" px={px} dpr={dpr()} dither={false} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="types @ 24px">
              <For each={PLANET_TYPE_LIST}>
                {ty => (
                  <Cell label={ty}>
                    <Planet seed={0x5eed1} type={ty} px={24} dpr={dpr()} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="types @ 12px">
              <For each={PLANET_TYPE_LIST}>
                {ty => (
                  <Cell label={ty}>
                    <Planet seed={0x5eed1} type={ty} px={12} dpr={dpr()} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="seeds @ 24px">
              <For each={SEEDS}>
                {s => (
                  <Cell label={`${s}`}>
                    <Planet seed={s * 7919} type="terran" px={24} dpr={dpr()} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="tint toward a nation @ 24px">
              <For each={[0, 0.25, 0.45, 0.7, 1]}>
                {a => (
                  <Cell label={`${Math.round(a * 100)}%`}>
                    <Planet
                      seed={0x5eed1}
                      type="terran"
                      px={24}
                      dpr={dpr()}
                      tint={props.ownerColour("solarian")}
                      tintAmount={a}
                    />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="real systems @ 24px">
              <For each={props.systems.slice(0, 10)}>
                {s => (
                  <Cell label={s.importance === 3 ? "★" : "·"}>
                    <Planet
                      seed={seedFromId(s.id)}
                      type={typeForSystem(s)}
                      px={24}
                      dpr={dpr()}
                    />
                  </Cell>
                )}
              </For>
            </Group>
          </div>
        </div>
      </Show>
    </>
  );
}

function Group(props: { label: string; children: unknown }) {
  return (
    <div class="spike-group">
      <div class="spike-label">{props.label}</div>
      <div class="spike-cells">{props.children as never}</div>
    </div>
  );
}

function Cell(props: { label: string; children: unknown }) {
  return (
    <div class="spike-cell">
      <div class="spike-slot">{props.children as never}</div>
      <div class="spike-cap">{props.label}</div>
    </div>
  );
}
