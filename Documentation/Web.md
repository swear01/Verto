# Web preview

[Play online](https://phase-gamejam1-20261016.swear01.chatgpt.site). This is a
manually published eight-room demo; automatic CI/Pages still needs the owner
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

Open <http://127.0.0.1:8765/>. Click the canvas for keyboard focus: arrows/WASD
move, Space/Z jumps, X dashes, and C/Shift grabs. Cross each room’s right
edge to continue through the twelve rooms. Each arrival resets phase, dash, stamina
and mechanisms while preserving run statistics. Room changes use a
0.18-second horizontal pixel slide; input/physics pause until it finishes.
The Unity splash screen and logo are disabled in PlayerSettings.
The build verifies both flags and records them in `TestResults/Web/splash.json`;
browser checks preserve initial canvas frames before the first player frame.
The hood and scarf are amber with one dash available and dark orange after use;
landing refills them. Eye colors continue to indicate phase.
Below55 stamina, the whole helmet gradually reddens and shakes by one native
pixel with increasing frequency; at20 the red gently pulses. The cloak stays
bone-white, larger eyes indicate phase, and the scarf independently indicates
dash charge. Landing restores the helmet. There is no detached stamina gauge.
Fast landings add neutral white dust; respawns end with a brief pixel convergence.
Holding grab against a wall after exhaustion caps downward speed at45native
pixels/second; releasing, moving away or pressing down restores ordinary falling.
[Celeste’s original player source](https://github.com/NoelFB/Celeste/blob/1b0ce45c75e05649ae91b44a8bb6b196684e4352/Source/Player/Player.cs)
flashes its sprite below20 and releases climbing at0; its normal wall slide ramps
from20 to160 over1.2seconds. The progressive tint and sustained exhausted-grab
cap are intentional PHASE tuning for this user request, not exact Celeste parity.
Touch controls are not provided.
The canvas fills the entire viewport and always shows at least the original
320x180 native-pixel play area. Wide views extend horizontally; tall views keep
320pixels across and center the original180-pixel play area. Built-in rooms have a visible, solid 8px ceiling; stone fills above the top boundary;
stone touching the last complete grid row continues below. Gaps fade into dark void
from Y180 to the existing Y194 death limit, rather than suggesting more playable space. Point/pixelated sampling preserves
square pixels without cropping the room to enlarge the player. Wide rooms extend
quarry scenery, full-width floor collision and their right exit. Resizing preserves
movement/color state and clamps positions within a shortened room; resizing during
a slide completes the handoff safely. Fixed obstacles and movers retain their
world coordinates. The toolbar includes fullscreen and keyboard settings. Fullscreen
needs a user click and browser/iframe permission; denied requests show a recovery
message. Loading failures remain visible.

The top-right settings button opens keyboard bindings and pauses the game. Select
either of an action’s two slots, then press a key; the other slot is preserved.
Shift is one logical binding that accepts either left or right Shift. During capture,
Esc clears the selected slot; each action must keep at least one key. Tab cancels
capture and moves focus. Outside capture, Esc closes settings. Duplicate keys and browser modifier shortcuts
are rejected. Changes apply immediately and are stored on this browser/origin;
blocked storage displays a warning while keeping the current session usable.
Release held keys after closing settings to resume. Each action has its own Restore
button; it resets only that action and is disabled when already at its defaults.
If another action uses a default key, the message names that action and asks you to
change it first. Other bindings are preserved. Bindings survive room travel.
`phase.controls.v2` stores 14 strings (two per action, empty means unused).
Existing `phase.controls.v1` single overrides migrate into the first slot; untouched
actions keep both defaults. The old data remains unchanged. Invalid or conflicting
saved data falls back to defaults with a visible notice.
The settings interface lives in the Web page; the game view stays free of prose.

The build opens on the PHASE title; jump starts level1 and the pencil opens the in-game
level editor (see [Development](Development.md)).
The top-right room button selects any of the twelve rooms. Custom maps are made only
in the in-game editor; the page has no map editor of its own.

Custom maps contain `name`, `blocks`, `spikes`, `spawnX` and `spawnY`, plus optional
`lasers` (`x`, `y`, `direction` 0–3 up/down/left/right, `phaseMask`) and `crystals`
(`x`, `y`, `kind` 0 jump/1 dash, `phaseMask`), each one cell, `platforms`
(`w`, `h`, `nodeX`, `nodeY`, `returns`) and `gears` (`wall`, `platform`, `top`, `endX`);
see [Development](Development.md). Exports omit optional lists when empty, so older
five-field maps and readers keep working.
Geometry occupies at most 880 non-overlapping cells inside 320×176; spawn uses the
8-pixel grid, stays inside 312×160, and avoids solids/hazards active in red,
including any red laser beam not stopped by red-active terrain.
Masks are 1 red, 2 blue, 4 green, 3 magenta, 5 yellow, 6 cyan and 7 permanent white.
The built-in single-mover/gear fields and JSON over 200,000 characters are rejected. The room
selector sends `PHASE.SelectDemoRoom("1".."12")` and closes only after
`phase-map-result` reports success. All dialogs share the gameplay pause.
`PHASE.LoadCustomMap(json)` remains a Unity bridge method, but the page no longer sends it.
The in-game editor's import/export icons raise `phase-map-import` / `phase-map-export`
from `PhaseMaps.jslib`; `maps.js` opens the file picker or saves the download, and
sends the chosen file to `PHASE.ImportDraft`.
`WebPageBrowser.cjs` verifies page events using a mock Unity bridge; it does not
prove native map loading. Verify the native bridge through Pipeline and the final
built Web Player before publishing.
`DemoWebSmoke.cjs` checks native selection of all twelve rooms, in-game editor
laser/crystal import and export, draft persistence across reload, playing that draft
as a custom map, and movement after fullscreen exit. Room 1 synchronizes its staircase landings, Room 5 its low-ceiling jump/dash, and
Room 8 its gear lift and ferry stops with visible phase/position checkpoints.
Short walking pulses preserve narrow checkpoints; the final Room8 approach releases
right before the linked crystal-room exit.
For an isolated diagnostic, append `--room=1`, `--room=5` or `--room=8` to the
local verifier command; a calibration report is not a complete release check.

The custom `Phase` template uses gzip with Unity's decompression fallback, so a
static host does not need custom `Content-Encoding` headers. The 2D renderer has
post-processing disabled. A scoped WebGL guard returns no MSAA samples for the
six SNORM formats that Unity 6000.4 queries incorrectly in Chromium; ordinary
queries and warnings remain intact. Revisit it after an engine/browser fix.
The build warning about an absent `RuntimePipelineConfig` is expected: release
Players do not need Pipeline's remote-control server. Do not enable it to silence
the warning.

## Enable automatic builds and online play

`Web build and preview` runs only on `main`, after push or manual dispatch.
It uses Unity CLI + live Pipeline for 143 Editor tests, fourteen Play Mode tests and
the Web build, then verifies browser rendering, keyboard routes through all twelve rooms and eleven scene-edge transitions,
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
The template keeps automatic sizing disabled and sets both native backing dimensions
explicitly on resize; CSS covers the full viewport.

## Crystal course

The source build appends `PhaseCrystal1` through `PhaseCrystal3` after the eight demo scenes.
The existing room selector exposes them as9–11, with the same native loading
confirmation and draft preservation. The crystal rooms are authored through Unity
scenes and `CrystalRooms.json`; custom maps place single crystals from the in-game editor.
The three crystal routes and level12 use controlled browser time to prevent capture latency
from extending held keys. Existing visible-checkpoint routes and UI checks use
real time. This is functional verification, not a human timing/playtest.

The selector also includes **12**, the classmate-authored `PhaseDemo12` scene (formerly `PhaseDemo1.2`). It is appended to the Web build after the original eleven level numbers. Crystal levels9–11 now require distinct jump/dash/refill combinations and keep safe permanent edge landings.

Built-in levels run in order1–12. Walking through the left edge returns to the previous level; level1’s left edge stays closed. Leaving level12 to the right shows “你贏了”, with replay and level selection. The chooser can replace a paused arrival slide; a late load acknowledgement restores the chooser after a timeout.
