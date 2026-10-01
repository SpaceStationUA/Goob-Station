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
  planetSheet,
  planetUri,
  seedFromId,
  type PlanetType,
} from "./lib/planet";
import { blackHoleUri } from "./lib/blackhole";
import type { StarSystem } from "./lib/model";

/**
 * Sizes the map could plausibly use, plus the detail panel's working size, plus
 * two above it to see where the sprite stops gaining anything. 256 is the point
 * of interest: past roughly 128 the extra octaves stop being visible and the
 * sprite is just a bigger disc.
 */
const SIZES = [12, 16, 24, 48, 96, 128, 192, 256];
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
  spin?: number;
  cloudThreshold?: number;
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
      spin: props.spin,
      cloudThreshold: props.cloudThreshold,
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

/**
 * A rotating sprite.
 *
 * `frames` frames go into one horizontal strip at render time and CSS steps
 * through them. The alternative — re-rendering per animation frame — means a
 * canvas and a `toDataURL` per system per frame, which is not something you can
 * do sixty times a second with eighteen systems on screen.
 *
 * `image-rendering: pixelated` plus integer `steps()` is what keeps the frames
 * crisp; a non-integer step count or a smoothed scale is what turns this back
 * into mush.
 */
function Spinning(props: {
  seed: number;
  type: PlanetType;
  px: number;
  frames?: number;
  /** Seconds for one full turn. */
  period?: number;
  dpr?: number;
  cloudThreshold?: number;
}) {
  const frames = () => props.frames ?? 12;
  const sheet = createMemo(() =>
    planetSheet(
      {
        seed: props.seed,
        type: props.type,
        px: props.px,
        dpr: props.dpr ?? 1,
        cloudThreshold: props.cloudThreshold,
      },
      frames(),
    ),
  );
  return (
    <div
      class="spin"
      style={{
        width: `${props.px}px`,
        height: `${props.px}px`,
        "background-image": `url(${sheet().uri})`,
        "background-size": `${props.px * frames()}px ${props.px}px`,
        "animation-duration": `${props.period ?? 24}s`,
        "animation-timing-function": `steps(${frames()})`,
        // Percentage background-position is measured against (container - image),
        // which is negative here, so the end offset has to be in real pixels.
        "--spin-end": `${-props.px * frames()}px`,
      }}
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

  // Test hook. check-dom needs to render a strip and compare frames, and a raw
  // dynamic import of the module does not survive Vite's URL rewriting — the
  // same reason the other checks go through window hooks.
  onMount(() => {
    const w = window as unknown as Record<string, unknown>;
    w.__galaxySheet = planetSheet;
    w.__galaxyStill = planetUri;
    w.__galaxyBlackHole = blackHoleUri;
  });

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

            <Group label="stars — self-lit, with corona">
              <For each={SEEDS}>
                {sd => (
                  <Cell label={`${sd}`}>
                    <Planet seed={sd * 7919} type="star" px={40} dpr={dpr()} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="star @ 256px">
              <Planet seed={0x501} type="star" px={256} dpr={dpr()} />
            </Group>

            <Group label="still vs rotating vs clouds, 96px">
              <Cell label="still, clear">
                <Planet seed={0x5eed1} type="terran" px={96} dpr={dpr()} cloudThreshold={2} />
              </Cell>
              <Cell label="rotating, clear">
                <Spinning seed={0x5eed1} type="terran" px={96} dpr={dpr()} cloudThreshold={2} />
              </Cell>
              <Cell label="still, cloudy">
                <Planet seed={0x5eed1} type="terran" px={96} dpr={dpr()} />
              </Cell>
              <Cell label="rotating, cloudy">
                <Spinning seed={0x5eed1} type="terran" px={96} dpr={dpr()} />
              </Cell>
            </Group>

            <Group label="cloud THRESHOLD — lower = more cloud">
              <For each={[0.3, 0.44, 0.54, 0.66, 0.8]}>
                {c => (
                  <Cell label={String(c)}>
                    <Spinning seed={0x5eed1} type="terran" px={96} dpr={dpr()} cloudThreshold={c} />
                  </Cell>
                )}
              </For>
            </Group>

            <Group label="types with clouds, rotating, 72px">
              <For each={PLANET_TYPE_LIST}>
                {ty => (
                  <Cell label={ty}>
                    <Spinning seed={0x5eed1} type={ty} px={72} dpr={dpr()} />
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
