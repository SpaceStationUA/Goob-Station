# Pirate WebUI — Spike Plan (Phase 0)

Goal: prove that content-side web UIs on top of the in-tree `Robust.Client.WebView`
(CEF) module work end-to-end: page loading from `res://`, JS -> C# bridge calls,
C# -> JS state pushes, before the real framework (SolidJS + Vite bundle) exists.

## What we are NOT doing yet

- No Vite/SolidJS/npm tooling yet — this spike uses a single hand-written HTML file.
- No server <-> netbridge integration (no `BoundUserInterface` binding) yet.
- No packaging/ACZ work yet.

Phase 0 acceptance: run the client, run the `webuispike` command, see a web window
whose page exchanges messages with C# live in-game.

## Engine facts we rely on (verified in this repo)

- `RobustToolbox/Robust.Client.WebView` is the optional CEF module. It is loaded
  only when `Resources/manifest.yml` lists it under `modules:`.
- Content projects opt-in by importing `RobustToolbox/Imports/WebView.props`
  (adds the `Robust.Client.WebView.csproj` project reference).
- `WebViewControl` (Robust.Client.WebView) is a normal UFC `Control` with:
  - `Url` — set to `res://...` (built-in scheme served out of the resource
    manager when `web.res_protocol` cvar is on, default true);
  - `AddResourceRequestHandler(handler)` — intercept any request (this is our
    bridge endpoint);
  - `AddBeforeBrowseHandler(handler)` — navigation fence;
  - `ExecuteJavaScript(code)` — C# -> JS push channel.
- CEF resources (`cef_resources`, `Robust.Client.WebView` subprocess binary)
  must be present next to the client binaries — they are built with the engine
  (`dotnet build` on Content.Client via the engine deps).

## Phase 0 spike (this branch)

1. `Content.Pirate.Client/_Pirate/WebUI/`:
   - `WebUiSpikeCommand.cs` — client console command `webuispike`.
   - `WebUiSpikeWindow.cs` — `DefaultWindow` hosting a `WebViewControl`.
   - `WebUiSpikeBridge.cs` — request handler for the bridge endpoint.
2. `Resources/_Pirate/WebUI/spike.html` — test page:
   - renders a state object pushed from C#;
   - buttons POST JSON actions back to C#;
   - shows a live counter ticking from the game loop.

Bridge protocol (draft, versioned envelope):

```json
{ "uiKey": "...", "type": "state" | "ack" | "pong", "data": { ... } }
```

- JS -> C#: HTTP POST to `res://_Pirate/WebUI/__bridge__` — intercepted by the
  per-control request handler. Body: `{ "type": "...", "data": { ... } }`.
  Response: `{ "status": "ok" }` or `{ "status": "error", ... }`.
- C# -> JS: `ExecuteJavaScript("window.__bridge.dispatch(<json>)")`.

Cookiecutter security fence: all non-`res://` navigations are cancelled via
`AddBeforeBrowseHandler`.

## Phases after this spike

- Phase 1: real bridge (`WebUiManager` resembling `BoundUserInterface`,
  state serialization, batched updates ~1 update per UI event), window chrome
  in XAML, ESC/hotkey handling, dev-server cvar (`webui.dev_url`).
- Phase 2: `Content.Pirate.WebUI/` Vite + SolidJS package, shared `bridge.ts`,
  route-per-`uiKey`, MSBuild `BuildWebUI` target into Resources.
- Phase 3: port one real machine UI end-to-end, keep the XAML UI as fallback
  behind a client cvar.

---

# Phase 3+ content roadmap (candidate features, 2026-09-17)

Systems already in this fork that push structured UI state periodically
(`UpdateUIState`-style) map 1:1 onto the bridge push + action model.

Policy caveat (verified 2026-09-17 in upstream docs): the module was built
for OpenDream's SS13 HTML interfaces and Space Wizards do not recommend
CEF for *new* game UIs ("too huge", "may be subject to change"). Our
answer for this fork: every WebUI is feature-gated behind an engine
capability check with the existing XAML UI kept as fallback, so the
content build stays valid on any engine.

## Tier 1 — existing systems, high wow per line of code

1. **HoloTVs / wall TVs / surveillance camera monitors** — a wall TV whose
   screen is a shared-synced WebView. Today: `SurveillanceCameraMonitorSystem`
   + Pirate TV stack (`Resources/Prototypes/_Pirate/Shaders/surveillance_vhs.yml`,
   `_Pirate/SurveillanceCamera/SurveillanceCameraMonitorNavMapSystem.cs`,
   `Content.Goobstation/.../handheld_television.yml`) renders a static sprite +
   XAML monitor. Browser: HTML "channel" grids, CRT/VHS overlays in CSS,
   multi-view screens, station-wide ads/news slides. The flagship feature.
2. **Arcade cabinets as real games** — `ArcadeSystem.cs`, `BlockGameArcadeSystem.cs`.
   Today: turn-based XAML menus. In CEF: canvas games with real physics,
   WebAudio SFX, persistent leaderboards over bridge state. One cabinet =
   one `res://` page; instant mini-game platform.
3. **TapeRecorder as in-game social media studio** —
   `Content.Goobstation.Shared/TapeRecorder/*.cs`. Recorded voice cassettes
   with waveform playback UI, shareable "podcasts", HTML transcripts.
4. **Casino floor** — SlotMachine / ClawGame / Roulette
   (`Content.Goobstation.Shared/SlotMachine/*`, `_Pirate/Furniture/Tables/RouletteSystem.cs`).
   Canvas reels, live odds tables, shared jackpot state on a wall screen.
5. **Engineering consoles** — `Content.Goobstation.Server/Supermatter/…`
   (gas composition pie charts, temperature-over-time lines), Psionics
   Records (`_EinsteinEngines/PsionicsRecords/*`) with glimmer trend graphs.
   Dense engineering UIs are the classic payoff of a real rendering engine.

## Tier 2 — bridge unlocked
6. **ObraDinn "mortician's ledger"** — detective case book UI over
   `Content.Goobstation.Shared/ObraDinn/*`: sketches, timelines, deduction
   UI. Real narrative layer impossible in XAML.
7. **PDA cartridge ecosystem as a real app store** —
   `Content.Pirate.Client/CartridgeLoader/Cartridges/*.cs`: every cartridge
   becomes a SolidJS page (mail, news reader, weather, glimmer monitor).
8. **GameDirector station-status dashboards** — threat/story-beat charts for
   admins, crew-facing news ticker on wall TVs.
9. **Polls / storefront reusing the uplink craft** — live poll bars and item
   catalogs; the uplink prototype is already the template.
10. **LightPaint station** — HTML canvas painting mirrored through the bridge
    to existing `LightPaintSystem.cs` sprite mirrors.

## Tier 3 — new feature classes only CEF enables
11. **Shared synced web surfaces** — TV where everyone watches the same video
    at the same server-owned playhead; server publishes media descriptors
    `{id, startTime, playing, rate}`, clients compute drift and correct via
    seek / playbackRate tweaks. Server-owned playhead means fair round-state.
12. **AI/MCP-assisted admin/crew terminals** — full websocket/http in CEF lets
    an MCP/LLM endpoint power "Ask Central Command" terminals (server-gated,
    rate-limited, cached). Server proxies, client only renders.

## CEF game-UI inspiration index (patterns -> games -> our use)

Nobody has done this class of content in SS14 yet because the module only
became fork-loadable with engine 277.0.0 (2026-05). These shipped products
are proven-pattern mines (all CEF or embedded-web-UI users):

| Pattern | Proven by | Our use |
|---|---|---|
| Whole game shell as one web app (store/missions/profile/friends in 1 surface) | League of Legends client | Lobby, currency store, main menu, crew consoles as one consistent web app family |
| In-game overlay browser (browser+docs+screenshots w/o alt-tab) | Steam in-game overlay | Admin "web console" over the game; docs/wiki while briefing |
| Big-data game screens | World of Tanks garage / WOWS post-battle | Engineering consoles (supermatter), round-end reports, crew hauls |
| Vanilla JS + canvas at shipping quality, perf-tuned | V Rising (clan/class UI, map) | Arcade, LightPaint, PDA toy cartridges |
| Inventory/craft/market drag&drop, tooltips, scanners | Escape From Tarkov flea/azerty UI | Surplus crates, uplink, toolbox crafting |
| Browser pinned to world objects (TV on a wall) | GTA V: FiveM server TV screens, samp-cef object textures | HoloTV/holo-cine/arcade screens in the 3D world |
| Watch-party sync (server-owned playhead, drift correction) | Watch2gether-style apps | Cinema station, shared ads |
| NPC-ish "mission board" with live progress + rewards | LoL event hub / tickers | GameDirector crew news, event consoles |
| Overlay chat/notifications over gameplay | Discord in-game overlay | Admin chat/ahelp overlays |
| Product-grade "feels native" desktop apps in web | Spotify, Discord | Lobby screens, game settings |

Note to self: upstream module docs (OpenDream rationale) mark CEF as
"not for new game UIs upstream" — but shipped games above prove WebUI
industrial-grade UI is fine when gated well (perf budget: OSR surface,
delta-push, pooled/virtualized DOM).

### Per-game idea harvest (verified sources, 2026-09-17)

From FiveM (the single deepest web-UI ecosystem in gaming):
- `kibook/pmms` — networked media-player *entities*: radios/TVs/cinemas that
  play HLS/YouTube with **server-synced playback per entity**, per-room
  sound attenuation, permission locks, preset site allowlists, audio
  visualizations, remote admin panel, per-player volume. Whole feature =
  our station-wide jukebox/cinema/ads system; URL allowlist is a security
  must (each client loads the URL individually -> IP exposure risk otherwise).
- `HZ-Television V3` — a smart-TV OS per screen: platform browser with
  favorites/history/home screen, per-screen remembered theme/wallpaper,
  remote admin (push URL to all screens, power off idle ones). Maps to our
  HoloTV: per-TV config + admin dashboard from CDP-style panel.
- `cody-raves/cr-3dnui` — world-space panels: raycast -> UV mouse routing,
  strict focus (blocks game binds), arcade demo pinned to a **moving
  entity** (snake on a car roof), whiteboard demos: our TV/arcade screens
  need the same focus/exit-key design; also "wall whiteboard" is an easy
  LightPaint translation.
- `Advanced Billboards` resource — in-game free-fly *placement tool* for
  ad boards on arbitrary surfaces (flat/corners/curves) with Ken Burns
  animations and playlists: apply to our signage/ad screens in-world.
- FiveM's own main menu is a React app (`ext/cfx-ui`) and loading screens
  get server handover data (`deferrals.handover`) — precedent for our
  lobby/menu and loading screen going web.

From Guild Wars 2 (ArenaNet engineering blog, 2023): CEF replaced
CoherentUI; a page render went 19.2ms -> 7.0ms per frame; they iterate
UIs faster, and (lesson) they kept a rollback path when launch-day issues
appeared — mirrors our own capability-check/XAML-fallback stance. Their
in-game books/Trading Post budgets validate lore-book terminals and
searchable markets on our stack.

From League of Legends (Riot "League Client Update" architecture notes):
- Platform holds ONE canonical state server-side ("foundation"); the UI is
  stateless and rebuilt from REST GETs at any moment -> crash-proof UIs.
  Ours: always support "re-render the whole UI from one snapshot" (`GET uplink_state`).
- Client shipped as *plugins* with explicit dependency graph; some took
  years to migrate — matches our cartridge-per-app PDA model.

From Camelot Unchained (CU wiki): UI modding was a first-class citizen —
community-authored web widgets with JSON manifests (`.ui` files) loaded
from disk with permission flags. That's a *community UI marketplace*
feature no other fork has: our PDA cartridge format as a user-authored
web cartridge pool, reviewed + signed server-side.
From Tarkov: flea-market-style filters/sort/multi-select that our
surplus-crate & shipment UIs can adopt.

(Transport note: FiveM's NUI uses `fetch` to virtual per-resource
endpoints — upstream Robust's CEF uses scheme handlers + our bridge;
equivalent capability, different plumbing — nothing to copy there.)

## Sequencing plan (post v277)

Uplink (done) -> TV/VHS surveillance + engineering console graph UI proof ->
PDA app store -> arcade. Feature-gated behind engine-capability check so
players on engines without the module are unaffected.

---

# SS13 browser-UI research (2026-09-17)

Background: BYOND games don't embed CEF — they render HTML inside their own
built-in browser surface (`/datum/browser` + `src/*.html` files served to a
DUE-style panel). So SS13 does not teach us the *plumbing*; it gives us a
massive catalog of interface *designs* that run the exact kind of web stack
we now have.

References worth mining:
- `tgstation/tgstation` — TGUI (React 18 + Inferno). Richest interface
  catalog: `tgui/packages/tgui/interfaces/`. Transport notes verified in
  their code (code/modules/tgui/*):
  - BYOND loads the whole React bundle via `client << browse(html)`; all
    server->client traffic is JSON messages routed per window by
    `client << output(msg, "[id].browser:update")`; client->server is
    JSON-over-Topic (`tgui_Topic` middleware).
  - Windows are **pooled** (`SStgui.request_pooled_window`, hard limit) and
    reused across UIs; `suspend`/`resume` instead of close/reopen for
    cheap UI toggling.
  - Payloads support **chunked oversize messages**
    (`oversizedPayloadRequest`/`payloadChunk`) — we hit the same URL-size
    problem in our bridge; adopt the pattern.
  - Static server assets (fonts, spritesheets, CSS/JS) are delivered via a
    single asset-system URL mapping, injected at page boot.
  - Backing model per machine: `ui_interact` / `ui_data` / `ui_act` +
    autoupdate each tick — direct analog of our `UpdateUIState` push +
    action dispatch; their "strict mode" turns UI runtime failures into a
    clean in-window BSOD instead of glitches.
  - Their `Uplink.tsx` layout matches our prototype 1:1 (sidebar, tabs,
    search, buy) — safe to copy look-and-feel conventions.
- `goonstation/goonstation` — their `tgui/packages/tgui/interfaces/Uplink`
  (PR #26846 "TGUI Syndicate Uplinks") is already referenced above; also
  their `browserassets/` shows a full hand-rolled HTML/CSS/JS stack for
  specialty windows ("browser asset" panels) with encodings/fonts/themes.
- `Aurorastation/Aurora.3` — migrated to **modern TG-congruent TGUI**:
  React 18 + TypeScript + Bun (commit 0d92359 "Bun, Inferno->React
  migration"). Historical experiments worth noting: an early Vue.js UI
  (PR #4868) with server/client state sync, and a from-DB cargo console
  with tabs/search/details. Conclusion: same stack as TG — no separate
  Svelte runtime — so TG notes above cover them too.
- `OpenDreamProject/OpenDream` — **the reason this module exists**:
  Space Wizards' own module docs state `Robust.Client.WebView` was created
  "to allow OpenDream to run SS13's HTML-based interfaces" (docs:
  spacestation14.com -> Robust Modules). That is the production precedent
  for in-engine web UIs and our closest architecture reference; the same
  docs also warn it is not the recommended path for *new* game UIs
  upstream ("too bloody huge", "may be subject to change") — a policy
  caveat we ship around via the engine-capability check + XAML fallback.
- Other in-game CEF precedents outside SS13 worth skimming for patterns:
  `Pycckue-Bnepeg/samp-cef` (SA:MP: object-texture CEF browser frames +
  spatial audio — the "browser as a texture in world" pattern = our TV
  project), `ArtemIyX/WebUserInterfaceUnreal` (host process + shared
  texture stream + local websocket/protobuf bridge = architecture matches
  how we'd do TV/arcade big screens), `roydejong/chromium-unity-server`
  (named-pipe CEF host with bidirectional messaging).
- Shipped-engines cross-checks (2026-09-17):
  - `dsh0416/godot-cef` — GPU-accelerated OSR texture node in 3D scenes
    (in-game screens/VR), automatic software fallback, typed CBOR IPC
    (no JSON!), DevTools remote debugging. Built for a commercial game.
    Directly mirrors our needs: OSR texture = TV screen; CBOR-like typed
    bridge idea for Phase-1 protocol; software fallback = the mac/GPU
    quirks we already handle.
  - `autumngmod/cream` (Garry's Mod) — React + Vite web UIs in CEF with a
    community codec-fix (fresh Chromium in GMod's old CEF). Precedent for
    shipping modern JS frameworks in an embedded browser at scale, and
    shows players *do* patch their client for rich media — consistent
    with our "fork ships whatever the engine ships" model.
  - `madsystem/WebView` + UCefView/DSXForge (Unreal) — h264/h265/AV1
    decode inside CEF, WebRTC cloud streaming, mouse penetration control,
    IME/keyboard focus interplay with game hotkeys (their notes match our
    spike's focus quirks), 4K/60 video rendering on a single GPU.
  - General CEF doc confirmation (chromiumembedded.github.io): scheme
    handler + sync XHR is the blessed synchronous path; CefMessageRouter
    for async JS<->host. Our sync-dispatch hook is equivalent to the
    scheme-handler sync pattern; keep it off the game thread and it's
    legitimately CEF-canonical.
- `ss14-art/web_UI_ss14` (`nova-ui`, Robust fork): hidden-iframe `ev://`
  bridge (chunked base64), `http://webui.local/` interception, `page-ready`
  handshake. The best documented in-Robust reference for the content-side
  transport tricks. NOTE: their client ships its own patched engine, which
  we are not allowed to require; we adopt the technique, not the
  environment. Our `tui://` rework deliberately uses a distinct scheme name.

## Engine 277.2.1 migration (2026-09-17, DONE)

Submodule bumped `68f8d0093` (270.1.0) → `f47f6b02b` (277.2.1); solution
builds 0 errors in Client + Server. CEF version on this line is 141.0.11
(chromium 141.0.7390.123) — matching mac natives rebuilt (see below).
Upstream/our own content needed these migrations (patterns to reuse when
the fork re-syncs with upstream):
- Grid instance methods removed → `SharedMapSystem` system calls:
  `map.TileIndicesFor(coords)` → `mapSys.TileIndicesFor(gridUid, grid, coords)`,
  likewise `GetAnchoredEntities`, `GetAnchoredEntitiesEnumerator`,
  `GetTileRef`, `TryGetTileRef`, `WorldToLocal`, `MapToGrid`.
- Systems are not IoC-resolvable anymore in some contexts; use
  `IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>()`
  (see `NodeHelpers.MapSys`).
- `Prototype<EntityPrototype>` toolshed args → `ProtoId<EntityPrototype>`
  + `_protoMan.TryIndex(...)` (`BasicStationEventSchedulerSystem`).
- `[Virtual]` classes may not be `abstract`/`sealed` (RA0053): removed the
  attribute憋 from sealed/abstract UI + system classes (CustomOutputPanel,
  AlternativeJobSelector, SlotControl, ItemSlotUIContainer,
  RadialMenuButtonBase, NanotrasenStylesheet, SyndicateStylesheet,
  SharedCrayonSystem, RitualTemperatureBehavior).
- Engine removed obsolete `TimerComponent`/`Lifecycle`-ish extension:
  `EntityUid.SpawnTimer` is GONE (engine #6475). Shim added content-side:
  `Content.Shared/Timing/SpawnTimerExtensions.cs` — drop-in
  `uid.SpawnTimer(ms, action)` / `SpawnTimer(TimeSpan, …)` /
  `SpawnRepeatingTimer(...)` that guards `EntityExists` before firing.
- Yaml: all `- type: Timer` components removed (no-op anyway; the
  component no longer exists) across Prototypes + Maps.
- Services break when stale: an OLD content server (engine v270) refuses
  handshake with the new v277 client — relaunch the server after any
  engine bump or you get "unacceptable net message before handshake"
  ("connect to server" failure).

### Mac CEF natives (dev-only, non-blocking for prod)

v277 pins cefglue `26-05-08-update-147`?? — actually no: our cefglue in
v277.2.1 is CEF **141.0.11** (`version.g.cs`), same as space-wizards/cef-rs
rev `10afdf90bcafb0491adc4ef535530eeee77f4ef9` ("141.6.0+141.0.11"). The
crash `CefRuntime.CheckVersionByApiHash() NotSupportedException` came from
our stale hand-built CEF 131 mac framework. Fix applied:
1. Download spotify binary distribution
   `cef_binary_141.0.11+g7e73ac4+chromium-141.0.7390.123_macosarm64_minimal`
   (from cef-builds.spotifycdn.com index → macosarm64).
2. Copy its `Release/Chromium Embedded Framework.framework` into
   `bin/Content.Client/` (helpers symlink from `macos_make_appbundle --webview`).
3. `brew install ninja` (cef-dll-sys build.rs needs CMake+Ninja), then in
   `RobustToolbox/native`:
   `CEF_PATH=<dist dir> cargo build -p robust-native-webview --release`
   and copy `librobust_native_webview.dylib` to `bin/Content.Client/`.
Verified: CEF 141 loads at runtime, `web.cef` CEF Runtime Version equals
the DLL constant; YouTube streams in a webui window.

## Transport rework: tui:// iframe (Nova-style) — DONE, verifies stock engine

Goal: no content-critical engine diffs in production. The old
fetch-on-res:// bridge required two engine hunks (scheme flags + 
host-as-path re-assembly). Both are now OPTIONAL — dev niceties only.
- **JS** (`Ui/src/lib/bridge.ts`): `postAction()` navigates a hidden
  iframe to `res://…/tui_bridge/<tx>?action=…&data=…`. Promise resolves on
  the `tui-dispatch` CustomEvent carrying `{tx, payload}` (JSON-parsed by
  the engine). Reply payload embeds its own receiver per-response (no
  load-order race). `onPush(handler)` exports server-initiated push events
  (`tui-push` CustomEvent). 10s timeout → `{ok:false, error:"bridge-timeout"}`.
  Window.__tuiFetchProbe = postAction (CDP diagnostics hook).
- **C#** (`WebUiTuiIpc.cs`): before-browse hook — `…/tui_bridge/<tx>` URLs
  are canceled and enqueued (tx parsed from the tail before `?`);
  everything not `res://|usr://|data:|blob:` is fenced. `Pump()` (window
  FrameUpdate) dispatches queued actions on the main thread; responses
  execute `window.dispatchEvent(new CustomEvent('tui-dispatch', {detail:
  {tx, payload}}))` in the main frame — receiver is embedded per response
  so no preload race. Public API unchanged: `window.Bridge.SyncDispatch = …`
  still drives mockuplink; `webuiuplink`/`webuidev`/`webuispike` work.
- `spike.html`'s old `fetch()` bridge calls were rewritten to the same
  transport; listener-based push replaces polling.
- Verified live (chrome/CDP driven): page loads, `__tuiFetchProbe
  ('uplink_state')` returns snapshot JSON, `Buy` button click credits
  `2 → 0` round-trips; lock & self-destruct intact. Bugs found & fixed
  during bring-up: duplicate tx in `postAction` vs `tuiUrl` (pending key
  mismatch — resolve never fired), engine execute-before-frame-loaded
  receiver race (fixed by embedding receiver per response).
- Engine patches STILL REQUIRED for dev (re-applied on v277):
  1. `Robust.Shared/ContentPack/ModLoader.cs` — `TryLoadExtra` before the
     `_useLoadContext` gate in `DefaultOnResolving` (dev content-start
     path needs module assemblies resolvable; launcher builds don't).
  2. `Robust.Client.WebView/Cef/WebViewManagerCef.cs` `ResourceScheme-
     FactoryHandler.Create` — re-attach CEF URL "host" as leading path
     segment (`res://_pirate/...` → path `/_Pirate/`) so res files resolve.
  (Removed 2026-09-17: the `CorsEnabled|FetchEnabled` scheme flags patch —
  the legacy fetch transport is retired; `res` is back to stock
  `Secure | Standard`.) The remaining 2 hunks are archived in
  `WebUIEngineDiff_v277.patch` (41 lines).
  After Phase-2 lands nothing above gate must touch the engine — tier
  work continues with the plain 277 zip.

## Next up
- Save `.patch` snapshots of the three dev engine hunks to
  `WebUIEngineDiff_v277.patch` at repo root (same recipe as v270 archive).
- Push `Push()/onPush` into real screens (TV state owner changes →
  server-initiated refresh without re-fetch loop).
- Phase-1 protocol: chunked base64 payload for big replies (outline:
  C# `Push("tui-payload", …)` per chunk, js accumulator reassembly) —
  only needed once bridges exceed URL length limits.

## TV channel sources — licensing + codec verdicts (2026-09-17)

### The codec layer (verified, engine-level)

Every proprietary-codec conclusion here assumes our verified runtime picture:
- **Stock engine ships no proprietary codecs anywhere.** Windows/game-server
  CEF module packages up `Robust.Natives.Cef.win-x64/linux-x64` NuGet from
  upstream's `chromiumembedded/cef` **"Standard Binary Distribution"**
  (v147, chromium 147.0.7727.118, readme checked inside
  `robust.natives.cef.win-x64.147.0.10.nupkg`), and standard dist has no
  H.264/AAC. Space-Wizards native-build does not compile CEF at all.
- **Our mac dev natives** (CEF 141, `macosarm64_minimal` from Spotify) are
  the same class — no proprietary codecs either.
- Our runtime `canPlayType('video/mp4; codecs="avc1…"')` = `""` and
  `MediaSource.isTypeSupported('avc1') = false` — verified live both on
  our codecheck target and on a real uakinogo window (probe chaining probe).
- Consequence: **YouTube (VP9/AV1/Opus) and any open-codec source work on
  vanilla 277**; HLS/MP4/H.264 stacks (almost every commercial UA streamer
  incl. ads) die immediately (`bufferAppendError`, `err:4`). Codec-complete
  output **only** exists if the fork assembles its own proprietary-codecs
  CEF dists (`proprietary_codecs=true, ffmpeg_branding=Chrome`) per
  platform and ships them as a *fork packaging variant* — never via engine
  source. Until that conversation is settled, treat those sources as
  unavailable (each card degrades politely, we don't fake it).

### The legal layer (Ukrainian/UA content)

Live verdicts, used to define what we ship:
- **Megogo** — full ToS review done (Ukrainian agreement, en mirror too):
  `п.2.1/3.5 personal non-commercial only`; `п.3.5` forbids enabling
  third-party access incl. **group viewing**; `п.2.4` forbids
  **ad-blocking**, `п.7.5/9.3` DRM; `п.7.4` 5 simultaneous streams; TV
  channels: `п.6.1.5` (rightsholder-dependent); B2B path exists
  ("Передписка для закладiв", `b2b@…`). Technical probe: free VOD plays a
  HLS/MP4 h264 stack (`b/450_900_1350_1500_2000_5000/…/playlist.m3u8`),
  dies instantly on our codec-less CEF (`player_error` fires right away,
  even the pre-roll ad video `err:4`). **Verdict: unusable today; only
  possible with codec-enabled native bundle or venue-B2B contract.**
- **Sweet.tv** — same class, 21+ gated, scraped contents unfetchable
  anonymous; B2B (`b2b@sweet.tv`). Термінальний верdict pending their
  ToS text; treat as **B2B-only** channel.
- **Kyivstar TV** — "Абонент — фізична особа … кінцевий споживач" (ToS
  multi-profile within ISP ecosystem); no external-embed form; treat
  as **B2B-only**, no in-game slot.
- **1+1 / TET** — own catalog ("Моє кіно" etc. — state-funded content);
  B2B contact (`digital.sale@1plus1.tv`); no embed path today.
- **Dovzhenko Centre Online** — digitized UA classics; theatre-style paid
  platform (85 UAH/title, "Лекторій" free); open/public archive subsets
  exist; **most promising "culture venue" partner** for licensing, plus
  some classic themes on their YouTube with embeddable players already.
- **uakinogo.is / Kinogo-family** — technically worked (stiven-king player
  ships non-H.264 rendition pipelines we COULD drive: season/episode
  dropdowns + `Укр. Дубльований` voice), **but**:
  1) belongs to the Kinogo/kinopoisk.rozlook blacklists (кiberpolitsia
  /myhackerstories/apo.kiev.ua `blacklist-uapa`), also mirrors blocked by
  Cyberpolice actions; 2) only "played" because its CDN happens to serve
  a variant our codec-less runtime can decode (VP9 family) — 90% of their
  episodes still mp4/h264 → ghost-broken.
  **Verdict: banned as a production channel.**
- **YouTube** — the *licensed and stable* webbed source row (IFrame Player
  API documents our whole transport for this case). Uses same playbook on
  channel as our library: URL-driven, no scraping. **Verdict: adopt as the
  default "embeddable external content" slot (slot 2).**
- **Archive.org / CC / public-domain + our own UA subs** — the "own
  cinema" front: our engine-side `<video>` driver = simplest possible
  control plane; we produce our own subtitles (community PRs, attribution
  always credited). Past that "legally clean by construction" zone.

### Ship matrix (current, engine=stock 277)

| Slot | Content | Codec | Legal path |
|---|---|---|---|
| 1. media-pack | Our library (PD/CC films + our own UA subs) | our own encodes (WebM/VP9) | clean by construction |
| 2. YouTube playlist | YT embeds incl. UA content where rights allow | open codecs (VP9/AV1) | clean via official embed API |
| 3. web-pinned | Neutral player-to-URL parity (Megogo etc.) user-supplied session | **codec-required** (is blocked on codec-less runtimes) | gray but defensible; good-faith letter pending |

Pinned-channel behavior (slot 3): admin-pinned **domains** (never
fork-cataloged titles), each client's own Megogo/Sweet session, ads and
DRM untouched, sync = pause/play/seek over the *player the page itself
provides*. No fork authored "season/series UI" over walled-garden sites —
that's the line Megogo's `п.3.5` matters about, and it's what makes the
whole shape different from "generic sync remote is a dumb browser" claims.

Gray-tier key: **curation = license; user-pasted link = user's own act.**
Never ship a scraped/EPG/CSS-hiding "curated-list" experience for
walled-garden sites; those need a contract. Never auto-reform the
playback model (skip ads, bypass DRM); any fork-provided UI with content
choices replicated from a commercial vendor = "editor channel" requiring
their permission, not just "users happen to watch the same link".

### TV-1 implementation state

**Pivot (2026-09-17):** walled-garden/codec walls lead nowhere today, so the
TV feature is now a **YouTube + Twitch movie theater**:

- `WebTvChannel.cs` — parse+host whitelist: pick on YouTube (`watch?v=`,
  `youtu.be`, `/shorts/`, `/embed/`, `/live/` → rendered as
  `youtube-nocookie.com/embed/ID?autoplay=1&enablejsapi=1…`) or Twitch
  (site `/videos/N`, `/channel`, `player.twitch.tv?channel/video` forms →
  rendered as the plain site page). TikTok/shorts-style clips (flat
  `clips.twitch.tv/…`) rejected by the parse-here flow.
- `WebTvPickerWindow.cs` — the room "browser": browses inside the
  whitelist-only fence, tick-tracks `_web.Url`, button "▶ Поставити на ТБ"
  — if the room clock is fresh (<15 min since pick) a **confirm panel**
  appears ("Зараз грає… Так/Ні"), confirming re-publishes the channel.
- `WebTvWindow.cs` — the viewer: shows whatever channel was published
  (embed iframe for YT, site for Twitch), native remote: Пауза/Грати,
  ±60/±10 seek/rest-mute, status (label + mm:ss + ▶/⏸ + 🔇). NO "publish
  everything that plays" loop (that yanked the room); publish only on
  remote actions; every window follows via `FrameUpdate` drift check
  (±5s auto-resync; Twitch live seek is a no-op by its own policy).
- `WebUiTvDriver.cs` — generic `<video>` hook (idempotent inject,
  state reports `{t,dur,playing,muted,err}`, control commands
  play/pause/seek/seekTo/mute) — no more Megogo/Kinogo iframe walking.
  Embed YT as top-level nocookie page is enough for plain DOM control
  (iv_load_policy=3 to drop hints).
- `WebTvCommand.cs` — `webuitv [url]` (no url → viewer on room channel;
  url → owner override) and `webuitvpick` for the picker window.
- Yank guard: publish-on-events only. Someone opening TV mid-room sees
  video at "expected wall time" (shared `Pos` + elapsed while playing)
  thanks to seek-to-target in `FollowRoomClock` when opening at `0`.

Connect flow for a new viewer: `OpenCenteredTv` navigates to the shared
room channel; ceiling sync seeks them to wall-clock `expected` (drift
>2s on change, >5s per-frame). Publish on pick: `_pendingPlaybackUrl` in
the picker applies to Backend; every open TV window gets the broadcast
in `OnBackendBroadcast` and navigates. Remote button presses mutate the
shared clock and drive their own video; the ceiling follows.

Engine unchanged (the 2 dev hunks); sandbox lesson: no
`System.Timers.Timer` in content — engine-driven `FrameUpdate` tick
instead; whitelists (YT/Twitch CDNs) remain content-side config in
`WebTvChannel.AllowHosts`.

Kept excluded from production channels entirely: uakinogo/Kinogo family
(voice-pin probes remain archived evidence only — the S2E3-style
per-episode voice rendition availability varies, so those pages are
ghost-broken even before licensing is considered).
- Probe evidence (uakinogo): the voice track pin after rebuild works
  (S1E1 = active) but **part-of-season availability varies per episode**
  (S2E3 rendition = Рен-ТВ only), so uakinogo channel is marked
  experimental regardless.

## Mac players on the official launcher cannot join webui forks (upstream gap)
The fork manifest lists `modules: - Robust.Client.WebView`; the launcher
RESOLVES the required module from upstream's modules.json, which for
277.0.0 ships win-x64/linux-x64 only, and hard-fails connecting mac
clients (NoModuleForPlatformException, EngineManagerDynamic.cs). No
skip/optional mechanism exists in the launcher; the engine zip is
signature-checked by SS14.Loader on every launch, so grafting our CEF
framework into the engines/*.zip is impossible without upstream's key.
Workaround for mac devs/testers: run a locally-built dev client
(Scripts/sh/runclient-webui.sh <server>) and authenticate with the
`launchauth <wizden-username>` console command — it reads the same
login row (UserId/UserName/Token) from the launcher's own settings.db
(Robust.Client/Console/Commands/LauncherAuthCommand.cs, compiled under
TOOLS which Debug/DebugOpt/Tools all have). CEF works because the dev
client's mac bundle embeds it. Long-term fix: ask upstream to publish
a macOS package of Robust.Client.WebView 27x — Tools/package_webview.py
already supports PLATFORM_MACOS.

## WebArcade sessions state (2026-09-18, committed & pushed)

### Shipped (see commits 99dd517fdec / 2da734f6049 / a6bac1feb4d)
- **Six games** in one cabinet, server-authoritative game switcher:
  id/label/path single-source in `PirateArcadeGames.List` (Shared).
  Server keeps `Session.Game`; pick allowed from a free cabinet or
  seated player; unknown ids rejected server-side (no arbitrary
  res:// pointing). Broadcast `Game` lives in `PirateArcadeStateEvent`
  — every open window mirrors it, player's window hot-reloads, list
  shows the loaded game green (Positive style).
- **Seat/mirror semantics** (user-verified): open claims via
  `Seat(true)`; server flips losers to spectators; «Встати» releases
  the seat and the window STAYS OPEN as a mirror view; spectators see
  an idle line + «Почати гру» button (explicit claim, no auto-grab);
  per-cabinet window slot releases on window close, NOT Dispose
  (SS14 windows hide; Dispose never fires on close).
- **Streaming**: capture hook downscale ~520px wide / JPEG q0.45 /
  100 ms (~10fps). Measured at full frames earlier: ~0.46 MB/s in
  per player, ~0.6 MB/s per spectator out, ZERO when idle; the
  downscale cut legs ~4x at 1.5x the fps (≈0.12 MB/s per leg).
  Server = dumb per-spectator relay, no tick cost.
- **Spectator status line**: `spectator.html` has `__tuiStatus(text)`
  idle-message channel; client pushes hints («чекаю…», «вільний —
  можеш почати гру»).

### CEF-mac keyboard bridge (content-only, engine untouched)
Upstream mac keyboard forwarding is broken three ways (verified live
via CDP key logger on the page): KeyUp NEVER reaches pages; OS
autorepeat arrives as repeated keydowns without `repeat=true`; letter
keys arrive as `Unidentified`. Engine hunks were tried and REVERTED
(user push-back: plain engine; patch stays 2 hunks: ModLoader +
res:// host re-attach). The working fix is entirely in the capture
hook inside `WebArcadeWindow.cs`:
1. window-capture filter swallows duplicate keydowns by held-key
   tracking (`__tuiDowns`) — works despite missing repeat flag;
2. `Unidentified` keys rewritten from `keyCode` in-page with proper
   DOM `code` ("KeyW") — games match on `event.code` (tomato.js etc.)
   so both key and code must be synthesized;
3. the CLIENT subscribes `InputManager.FirstChanceOnKeyEvent` and on
   every physical keyup calls `__tuiKeyUp(key, code)` in the page —
   synthesizes the missing DOM keyup (tomato.js holds state until the
   matching keyup);
4. a 300 ms per-key latch (`__tuiLatch`) swallows the stale repeat
   queue that CEF flushes right AFTER each release (re-arms held
   state otherwise; keys lowercase — the latch was first stored under
   C# "ArrowUp" names and never engaged until lowercased);
5. focus: our panel buttons steal keyboard focus → `__tuiDrop` 
   (synthetic keyups for anything held) + `GrabKeyboardFocus` return
   on every panel button and on window open.
Debugging pattern that cracked it: CDP attach at localhost:9222 with
capture-phase recorders on window AND document (kp3/kpfull scripts in
/tmp/opencode) — events show up ONLY at window-capture, no keyups, so
log both. Re-instrument after any page reload (game switch wipes it).

### Engine/server packaging notes (mapper test server)
- `StripWebViewModuleOutOfBin` (mac dev keeps the module in bin/modules
  so it isn't loaded twice) MUST NOT run for server-side client
  assembly bakes: it deleted Robust.Client.WebView.dll from
  bin/Content.Client and the StatusHost died packaging downloads for
  every connecting client. Gated behind `-p:PirateStripWebView=1`
  (set by runclient-webui.sh now).
- Launcher-side mac gap documented in the section above (launchauth
  workaround).

### Known follow-ups
- Witchcat loads slowly (roadroller-blob page); maybe pre-shrink dist.
- Spectator fps/q knobs live in HookScript const if re-tuning needed.
- Upstream mac module build of Robust.Client.WebView (PLATFORM_MACOS
  in package_webview.py) lifts the launcher gate for all forks; engine
  stays on v277.2.1 meanwhile (upstream CDN is on 28x/289).
- License-ask list: goblins (no license), Black Hole Square,
  remvst titles (all rights), Non-mewtonian Cat assets (third-party).

### Session disconnect incident (2026-09-19, resolved)
- Server "went down" mid-session: NO crash in server log — it was an
  accidental `pkill -f Content.Server` during a cleanup command. Lesson:
  never blanket-pkill Content.Server/Robust.Client; use scoped patterns.
- Real bug found by the log dump: after disconnect, the arcade window
  kept pumping Frame/Seat/Watch events every frame ("Tried to send
  message while not connected" spam; SendSeat also fired late from
  OnClose). Fixed: all arcade client sends connection-gated
  (INetManager.IsConnected) and the window auto-closes in FrameUpdate
  once disconnected (commit dc880bce48d).
- Client log DOMINO lesson: post-disconnect IPC spam obscured the real
  cause in the log flow (tons of stack-trace ERROR lines between real
  events); expect truncation-heavy logs — grep selectively.
