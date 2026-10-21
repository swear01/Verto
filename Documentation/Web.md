# Web preview

[Play online](https://verto.5334601.xyz). This is a
manually published Cloudflare Pages preview; automatic CI/Pages still needs the owner
setup below.

Unity **6000.4.7f1**, Web Build Support and Unity CLI are required. Open the GUI
Editor as described in [Development](Development.md), then run:

```sh
python3 Verification/BuildWeb.py
cd Verification
npm ci
npx --no-install playwright install chromium
cd ..
node Verification/WebPageBrowser.cjs
node Verification/DemoWebSmoke.cjs Builds/Web
python3 -m http.server 8765 --bind 127.0.0.1 --directory Builds/Web
```

Open <http://127.0.0.1:8765/>. Click the canvas for keyboard focus: ←/→ or A/D
move and Space/Z jumps. Cross each room’s right
edge to continue through the fifteen rooms. Each arrival resets the phase to red
while preserving run statistics. Room changes use a
0.18-second horizontal pixel slide; input/physics pause until it finishes.
The Unity splash screen and logo are disabled in PlayerSettings.
The build verifies both flags and records them in `TestResults/Web/splash.json`;
browser checks preserve initial canvas frames before the first player frame.
The hood and scarf are amber; the cloak stays bone-white and the eyes indicate phase.
Fast landings add neutral white dust; respawns end with a brief pixel convergence.
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
start level1, open the level select, the in-game level editor or settings (key bindings
and fullscreen). Escape in a level pauses. Bindings are stored with Unity PlayerPrefs (`phase.keys.v1`) in this
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
`PHASE.SelectDemoRoom("1".."15")` and `PHASE.LoadCustomMap(json)` remain Unity bridge
methods, but the page no longer sends them; `phase-map-result` still reports each
completed load and the browser smoke test waits for it.
The in-game editor's import/export icons raise `phase-map-import` / `phase-map-export`
from `PhaseMaps.jslib`; `maps.js` opens the file picker or saves the download, and
sends the chosen file to `PHASE.ImportDraft`.
`WebPageBrowser.cjs` verifies page events using a mock Unity bridge; it does not
prove native map loading. Verify the native bridge through Pipeline and the final
built Web Player before publishing.
`DemoWebSmoke.cjs` selects all fifteen rooms through the pause menu's level select,
reaches the victory banner and replays through its level select, rebinds keys in the
in-game settings and checks they persist across reload, then checks in-game editor
laser/crystal import and export, panel clicks, panel drags and grid strokes at 960×540, 2560×1080 and
390×844 (saving `demo-editor-*.png`), draft persistence across reload, playing that draft
as a custom map, and fullscreen on and off through the in-game settings by keyboard,
followed by movement. Room 1 synchronizes its staircase landings, Room 5 its low-ceiling jump/dash, and
Room 8 its gear lift and ferry stops with visible phase/position checkpoints.
Short walking pulses preserve narrow checkpoints; the final Room8 approach releases
right before the linked crystal-room exit.
For an isolated diagnostic, append `--room=1`, `--room=5` or `--room=8` to the
local verifier command; a calibration report is not a complete release check.

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

## Enable automatic builds and online play

`Web build and preview` runs only on `main`, after push or manual dispatch.
It uses Unity CLI + live Pipeline for 165 Editor tests, 35 Play Mode tests and
the Web build, then verifies browser rendering, keyboard routes through all fifteen rooms and fourteen scene-edge transitions,
adaptive viewport sizing, keyboard settings, fullscreen, visible load failures,
and absence of browser errors/warnings. Only a passing build is
uploaded and deployed. `web-verification` contains the reports/captures.

The repository owner must complete runner/Pages setup first. Write access cannot
register a runner for this personal repository:

1. Register a **dedicated macOS ARM64 runner** under Settings → Actions → Runners,
   with the custom label `gamejam1-unity`. Run it in a logged-in desktop session
   with a licensed Unity 6000.4.7f1, Web Build Support, Python 3, and Unity CLI at
   `~/.unity/bin/unity`. Runner jobs execute the repository's `main` code.
2. Set Settings → Pages → Source to **GitHub Actions**. Pages from this private
   repository requires the owner's GitHub Pro or a qualifying paid plan; do not
   change repository visibility to work around this requirement.
3. Set the Actions repository variable **`UNITY_WEB_CI_ENABLED=true`**, then run
   `Web build and preview` on `main`. The successful deployment reports the
   playable URL in its `github-pages` environment.

Until this setup is complete the Web build/deploy jobs are explicitly skipped.
The hosted setup job fails with this blocker in the workflow summary, so a
missing setup cannot produce a green Web workflow. The existing
GitHub-hosted controller/compatibility checks still run on pull requests and
`main`. A local build passing does not prove that the runner or Pages is enabled.

References: [Unity Web deployment](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-deploying.html),
[runner registration](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners),
[Pages publishing](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).

Rendering reference: [Unity Web canvas sizing](https://docs.unity.com/en-us/engine/6000.0/manual/platform-specific/webgl/develop/canvas-size).
The template keeps automatic sizing disabled and sets the backing store to the display size
(CSS size × `devicePixelRatio`) on resize; CSS covers the full viewport. Unity's `Screen`
therefore reports real pixels, and each scene picks its native-pixel view from the aspect,
including the in-game editor's wider view.

## Crystal course

The source build appends `PhaseCrystal1` through `PhaseCrystal3` after the eight demo scenes.
The in-game level select exposes them as10–12. The crystal rooms are authored through Unity
scenes and `CrystalRooms.json`; custom maps place single crystals from the in-game editor.
The three crystal routes and the last level use controlled browser time to prevent capture latency
from extending held keys. Existing visible-checkpoint routes and UI checks use
real time. This is functional verification, not a human timing/playtest.

The level select also includes **2**, the classmate color room `PhaseDemo1.1`, and **13**, the classmate dash-platform scene `PhaseDemo12` (formerly `PhaseDemo1.2`). `PhaseDemo13` and `PhaseDemo14` (select **14** and **15**) are the later laser rooms and stay after that course. Routes for levels 2 and 14 are recorded. Level 15 does not yet have a 0-death route. Crystal levels10–12 now require distinct jump/dash/refill combinations and keep safe permanent edge landings.

Built-in levels run in order1–15. Walking through the left edge returns to the previous level; level1’s left edge stays closed. Leaving level15 to the right shows “你贏了”, with replay-level1 and level-select icons.
