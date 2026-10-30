# Web preview

[Play online](https://verto.5334601.xyz/), published by hand; see [Online play](#online-play).

Unity **6000.6.7f1**, Web Build Support and Unity CLI are required. Open the GUI
Editor as described in [Development](Development.md), then run:

```sh
python3 Verification/BuildWeb.py
cd Verification
npm ci
npx --no-install playwright install chromium
cd ..
node Verification/WebPageBrowser.cjs
node Verification/WebLoadSmoke.cjs Builds/Web
python3 -m http.server 8765 --bind 127.0.0.1 --directory Builds/Web
```

Open <http://127.0.0.1:8765/>. Click the canvas for keyboard focus: ←/→ or A/D
move and Space/Z jumps. Cross each room’s right
edge to continue through the seven levels. Each arrival resets the phase to red
while preserving run statistics. Room changes use a
0.18-second horizontal pixel slide; input/physics pause until it finishes.
The Unity splash screen and logo are disabled in PlayerSettings.
The build verifies both flags and records them in `TestResults/Web/splash.json`;
browser checks preserve initial canvas frames before the first player frame.
The player is a blue cat-eared robot with a pink scarf; the eyes indicate phase.
Fast landings add neutral white dust; respawns end with a brief pixel convergence.
Audio is synthesized at startup (no audio files): jump, land, death and crystal blips, plus a looping
Am-F-C-G background track whose hat, arpeggio and lead layers crossfade with the red, blue and green phase.
Browsers start it after the first key press.
Pressing toward a wall while falling slides down it, ramping from 20 to 160 native
pixels/second over 1.2 seconds, as in
[Celeste’s original player source](https://github.com/NoelFB/Celeste/blob/1b0ce45c75e05649ae91b44a8bb6b196684e4352/Source/Player/Player.cs).
Touch controls are not provided.
The canvas fills the entire viewport and always shows at least the original
320x180 native-pixel play area. Wide views extend horizontally; tall views keep
320pixels across; a room shorter than the view sits at its bottom with sky above. Built-in rooms have a visible, solid 8px ceiling;
stone touching the last complete grid row continues below. Gaps fade into dark void
from Y180 to the existing Y194 death limit, rather than suggesting more playable space. Point/pixelated sampling preserves
square pixels without cropping the room to enlarge the player. Wide rooms extend
quarry scenery, full-width floor collision and their right exit. Resizing preserves
movement/color state and clamps positions within a shortened room; resizing during
a slide completes the handoff safely. Fixed obstacles and movers retain their
world coordinates. The page has no controls of its own. Fullscreen is the last row
of the in-game settings, Unity's `Screen.fullScreen` on the canvas; the browser applies
it within the confirming key press or click, and an embedding iframe must allow
fullscreen. itch.io's own fullscreen button enlarges the iframe instead, and the canvas
follows that resize like any other. Loading failures remain visible.

Level select, pause and keyboard bindings are in-game screens, the same as in native
builds (see [Development](Development.md)). Opening level select or settings replaces the
screen behind it. Both scale by whole pixels so the screen fills most of the view; each
level tile is a thumbnail of that room. The build opens on the Verto title; its icons
start level1, open the level select, the in-game level editor or settings (key bindings, effect and music volume
and fullscreen). Escape in a level pauses. Bindings (`phase.keys.v1`) and volume levels (`phase.volume.v1`) are stored with Unity PlayerPrefs in this
browser's IndexedDB; bindings saved by the old page dialog (`phase.controls.v2`) are not
migrated. Right-click on the canvas never opens the browser menu (it erases in the
editor). Only the focused game canvas sends keys to Unity, and leaving the page
(blur or hidden tab) pauses play through `PHASE.SetControlsOpen`.
Custom maps are made only in the in-game editor; the page has no map editor of its own.

Custom maps contain `name`, `blocks`, `spikes`, `spawnX` and `spawnY`, plus optional
`lasers` (`x`, `y`, `direction` 0–3 up/down/left/right, `phaseMask`) and jump `crystals`
(`x`, `y`, `phaseMask`; older files may add `kind` 0), each one cell;
see [Development](Development.md). Exports omit optional lists when empty, so older
five-field maps and readers keep working; `width` and `height` are written only for a
room larger than one screen.
Geometry occupies non-overlapping cells inside the room, 320×176 unless `width` and `height`
(multiples of 8 up to 1280×720) say otherwise, so a larger room scrolls; spawn uses the
8-pixel grid, stays inside the room (312×160 at the default size), and avoids solids/hazards active in red,
including any red laser beam not stopped by red-active terrain.
Masks are 1 red, 2 blue, 4 green, 3 magenta, 5 yellow, 6 cyan and 7 permanent white.
Dash platforms (`platforms`), gears (`gears`), dash crystals (`kind` 1) and JSON over 2,000,000 characters are rejected.
`PHASE.SelectDemoRoom("1".."7")` and `PHASE.LoadCustomMap(json)` remain Unity bridge
methods, but the page no longer sends them; `phase-map-result` still reports each
completed load and the browser smoke test waits for it.
The in-game editor's import/export icons raise `phase-map-import` / `phase-map-export`
from `PhaseMaps.jslib`; `maps.js` opens the file picker or saves the download, and
sends the chosen file to `PHASE.ImportDraft`.
`WebPageBrowser.cjs` verifies page events using a mock Unity bridge; it does not
prove native map loading. Verify the native bridge through Pipeline and the final
built Web Player before publishing.
The custom `Phase` template uses Brotli with Unity's decompression fallback, so a
static host does not need custom `Content-Encoding` headers. The build uses High
managed stripping, IL2CPP OptimizeSize and Web code optimization `DiskSize`, keeping
each file well under Cloudflare Pages' 25 MiB limit. The code optimization lives in
the Editor's local build settings, so `BuildWeb.py` sets it and refuses to build if
any of these differ (`TestResults/Web/size.json`). The 2D renderer has
post-processing disabled. A scoped WebGL guard returns no MSAA samples for the
six SNORM formats that Unity 6000.4 queries incorrectly in Chromium; ordinary
queries and warnings remain intact. Revisit it after an engine/browser fix.
The build warning about an absent `RuntimePipelineConfig` is expected: release
Players do not need Pipeline's remote-control server. Do not enable it to silence
the warning.

## Online play

<https://verto.5334601.xyz/> is the Cloudflare Pages project `verto`. GitHub does
not deploy it: after merging a PR that changes the game, the merging session builds
the latest `main` in a fresh checkout as above, then publishes and checks the live site:

```sh
npx wrangler pages deployment list --project-name verto
npx wrangler pages deploy Builds/Web --project-name verto --branch main --commit-hash "$(git rev-parse HEAD)"
node Verification/WebLoadSmoke.cjs https://verto.5334601.xyz/
```

The list's Source column is the live commit; skip publishing when it is already this
commit or a newer one, so an older build never replaces a newer one. Run
`npx wrangler login` once per machine first. Pages caps each file at 25 MiB;
`Build/Web.wasm.unityweb` is the largest. `WebLoadSmoke.cjs` takes a build folder
or a URL, starts level1 from the title in headless Chromium, walks right and
requires a clean console.

References: [Unity Web deployment](https://docs.unity3d.com/6000.6/Documentation/Manual/webgl-deploying.html),
[Pages direct upload](https://developers.cloudflare.com/pages/get-started/direct-upload/),
[Pages limits](https://developers.cloudflare.com/pages/platform/limits/).

Rendering reference: [Unity Web canvas sizing](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/develop/canvas-size).
The template keeps automatic sizing disabled and sets the backing store to the display size
(CSS size × `devicePixelRatio`) on resize; CSS covers the full viewport. Unity's `Screen`
therefore reports real pixels, and each scene picks its native-pixel view from the aspect,
including the in-game editor's wider view.

## Level order

The level select lists **1** `PhaseDemo1`, **2** the classmate color room `PhaseDemo1.1`, **3** and **4** the laser rooms `PhaseDemo13` and `PhaseDemo14`, **5** the chaos level `PhaseDemo15`, **6** the four-screen [Umbrella](Umbrella.md) level `PhaseUmbrella`, then **7** the moving-platform room `PhaseGear`.

Built-in levels run in order 1–7. Walking through the left edge returns to the previous level; level 1’s left edge stays closed. Leaving level 7 to the right shows “你贏了”, with replay-level1 and level-select icons.
