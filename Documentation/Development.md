# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.4.7f1** GUI Editor. Scene construction, tests, input checks,
captures and Console inspection all run through the live Pipeline connection.
Do not use direct Editor `-batchmode` / `-executeMethod` invocations or cold
`unity run` / `unity test` jobs for this workflow. Fix a failed Pipeline connection
before continuing. Reuse the connected Editor instead of reopening it.
Parallel agents may edit isolated worktrees, but consolidate verification in one
owned resident Editor. Finish or cancel tests and stop Play before changing C#;
wait for builds to finish before importing code or rebuilding scenes.
Keep Unity's completed URP material-upgrade record in version control; resetting
it can reopen a startup confirmation dialog and block Pipeline commands.
Do not restore the Unity Hub one-time `HubForceResolve.cs` bootstrap. Its delayed
package resolve and self-deletion can trigger an Editor assembly reload during the
first Play Mode test run. The Editor resolves the committed package manifest itself.
On macOS, launch a new agent Editor hidden and without activating it:

```sh
open -g -j -n -a /Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app --args -projectPath "$PWD"
```

This macOS launch only controls window activation; all Editor operations below
use the official Unity CLI and Pipeline. `unity open` currently has no background
launch option. Check the active app before and immediately after launch; these
flags alone do not prove that Unity stayed in the background. Report an unexpected
activation. Do not activate Unity or restore another app after stealing focus.
Modal startup dialogs still require attention; report them instead of foregrounding
the Editor. The dedicated CI runner may continue using `unity open --wait`.

Run from the project directory, or pass `--project-path` to target it explicitly.
With multiple Editors open, pass `--project-path <project-root>` on every
command so a domain reload cannot select another connected project.
Discover the connected package's command parameters with `unity command --query`.

```sh
unity status --until-ready --json
unity command set_autotick -- --interval_ms 16
unity command run_script -- --file Verification/BackgroundEditor.cs --entry BackgroundEditor.Run
unity command recompile -- --focus false
unity command recompile_status
unity command open_scene -- --path Assets/Scenes/PhaseSandbox.unity
unity command run_tests -- --mode editor --filter ControllerTests --async_tests true
unity command test_status
unity command run_tests -- --mode editor --filter PixelTests --async_tests true
unity command test_status
unity command run_tests -- --mode playmode --filter SandboxTests --async_tests true
unity command test_status
```

To recreate the sandbox, call its existing builder through the connected Editor:
`unity command eval 'PhaseSandboxBuilder.Create();'`. Save any intentional scene
edits first; the builder preserves Unity's unsaved-scene confirmation.

Wait for each `test_status` result to be `completed` and inspect its summary before
starting the next test run. There are 48 controller cases, 9 laser cases, 17 demo-route/authoring cases,
15 crystal cases, 26 crystal-room cases, 18 pixel cases, 6 Grid authoring cases, 7 custom-map validation cases,
3 key-binding cases (validation, PlayerPrefs persistence, key caps) and 12 level-route cases in Edit Mode. The 29 Play Mode cases cover
each level's keyboard route, bindings, pause/release gating, room persistence, multiple authored platforms, crystal triggers, imported maps, the
title/editor flow including editor platforms and gears, the pause menu with key settings, and level select from the
title, pause and victory screens. Platform checks cover
vertical return travel, independent routes/reset, rider and climb-hop attachment,
crush/hazard sweeps, native collider synchronization and rendering.
The three immutable reference files preserve 40 movement/action/climb traces.
The exhaustion case uses explicit revised Y/speed expectations for its final17
frames under the new45px/s held-grab cap; all earlier frames and other traces
retain their original expectations. None of the immutable reference files are edited.
Production steps at 1/60 s; reference replays use the recorded 0.0166667 s.

Before tests or live Play checks, `BackgroundEditor.Run` sets every existing
Game/Simulator view to Play Unfocused. This is an Editor layout preference, not
a game setting; Unity’s public `SetPlayModeFocused(false)` maximizes the view
instead. The script fails if the installed Editor API changes or no view exists.
Run it with Play stopped. It also initializes Unity6000.4's local
`UserSettings/Search.index` before Play, preventing first-use
`SearchDatabase.EnumerateAll` exceptions. The index stays untracked.
Never pass `--focus true` to `recompile` or call `editor_focus`. Wait for a
successful `recompile_status` before tests; a connection drop during domain reload
is not a reason to activate the Editor.
Auto-tick keeps the unfocused Editor responsive and survives recompiles; disable
it after verification with `unity command set_autotick -- --enable false`.

For a live input check, enter Play mode and run the verification script. It queues
keyboard events on the player loop and temporarily permits unfocused input, then
restores the prior settings and removes its test keyboard. It requires no game
code changes or domain reload. Its virtual keyboard does not send keys to your
foreground application. The screen capture command reads the Game View render
target directly; it does not need the Unity window on screen.

```sh
unity command open_scene -- --path Assets/Scenes/PhaseSandbox.unity
unity command eval 'UnityEngine.Application.runInBackground = true;'
unity command editor_play
unity status --until-ready --json
unity command eval 'UnityEngine.Application.runInBackground = true;'
unity command wait_for -- --condition '{"member":"UnityEngine.Time.frameCount","op":"changed"}' --timeout_s 10
unity command wait_for -- --condition '{"findType":"Phase.PhaseSandbox","member":"Controller.time","op":"changed"}' --timeout_s 10
unity command run_script -- --file Verification/LiveInput.cs --entry LiveInput.Run
unity command capture_game_view -- --source screen --save_path Temp/phase-live.png --width 960 --height 540
unity command console -- --level error --tail 20
unity command editor_stop
unity status --until-ready --json
unity command eval 'UnityEngine.Application.runInBackground = false;'
```

Record `Application.runInBackground` before this sequence and restore that value
afterwards (the default above is false). Both waits must return `met: true`; the
script must return `passed`. Inspect the capture's returned `savedPath`: Pipeline
0.8.0-exp.1 places this file under `Assets/Temp/`. Keep diagnostic captures out of Git.

## Coordinates

The scene root uses Unity's rectangular `Grid` with 8px cells. Terrain and moving
mechanism authoring snaps with `WorldToCell`/`CellToWorld`; `BoxCollider2D.bounds`
defines each model's collision rectangle. The deterministic pixel-step controller
resolves contacts, preserving the recorded movement; Unity's physics solver does
not move the player. Native colliders follow mechanism motion, phase/deferred
solidification, respawn and viewport extension. Motion between authored stops
remains continuous at pixel resolution.

Use **PHASE > Paint Grid terrain** in the Scene view to paint 8px cells in the
selected color (permanent white, red, blue, green, yellow, magenta or cyan). Turn painting off to use
Unity's normal selection, Inspector and Delete; terrain `size` sets a rectangle
in whole cells. Save the scene normally. The painter supports Undo and skips
occupied cells. Demo JSON changes still require rebuilding the generated scenes.
`GridGeometryTests` checks native Grid/collider geometry and rejects off-grid demo
authoring; run it through Pipeline with the other Editor tests.

The controller uses native pixels, Y down, a 60 Hz accumulator, 8×11 collision
(8×6 crouched), and X then Y pixel stepping. Terrain transforms use Unity X/Y as
pixel coordinates with Y negated; spawn uses screen coordinates and platform
nodes use native X/Y-down offsets from their parent.
`PhaseSandbox.rightScene` links to `PhaseRoom2`; its `leftScene` returns to the
sandbox. Crossing an assigned horizontal edge loads that scene asynchronously,
places the player at the opposite edge at the same height, and carries color,
velocity, jump/dash state and counters. Empty links keep the side closed. Both
scenes must be in Build Settings and in the Web build's explicit scene list.
Deaths respawn in the current room. The controller’s top/bottom clamp and immutable reference
fixtures stay unchanged; there is no player text.
Default movement, dash aiming and climb bindings accept arrow keys or WASD.
Each action has two independent key slots; logical `Shift` activates both physical
Shift keys. `PhaseSandbox.ApplyControls` takes 14 key names in left/right/up/down/
jump/dash/grab order (two adjacent slots per action, "" for an empty slot). It accepts
only the keys the settings screen can label (letters, digits, arrows, Space, Shift,
Enter, Backspace and common punctuation), rejects duplicate physical keys and actions
with no key, then applies the entire map atomically. Settings save the map to
PlayerPrefs `phase.keys.v1` as comma-separated names; startup loads it and falls back
to the defaults when it is missing or invalid.

## Dash platforms

Add or duplicate the dash platform Prefabs under the scene's PHASE root;
each active `PhaseMovingPlatform` component creates its own platform. Keep Transform rotation at zero
and scale at one. `Size` controls the body independently of its route: a horizontal
40×8 platform stays horizontal while moving up or down.

Set each platform's `Nodes` relative to its parent, with native Y increasing down.
For example, a Prefab root at Unity `(80,-144)` with nodes `(0,0)` → `(0,-64)`
moves upward at the same X. Move the Prefab root to move the whole route; the child
Transform follows the current platform position. Use the native8px Grid for sizes
and stops.

Every valid player dash activates all platforms, each with its own route and
timing. With `Returns` enabled, use two distinct horizontal or vertical stops:
the platform travels to node1 in0.2s, waits0.2s, then returns in1.2s. Without
`Returns`, each dash queues one move to the next node. Respawn restores every
platform to its authored start. Riders and climb hops stay attached to the
specific platform they contact. Custom maps and the in-game editor also place dash platforms. The editor stores a drawn line as a one-way route with the 24×8 default body, so consecutive stops may differ on both axes.

## Climb gear demo

`Assets/Scenes/PhaseGearDemo.unity` is a standalone experiment, with no room links
or progression. Recreate it through Pipeline with
`unity command eval 'PhaseSandboxBuilder.CreateGearDemo();'`.
`PhaseClimbGear` creates a neutral wall and horizontal platform outside the static
terrain cache. Actual upward/downward climb displacement drives the wall in the
same direction and the platform between its X endpoints, carrying riders.
Hanging, release, slipping, blocked climbing and dashing do not drive it.
Both parts stop at the travel limits or an obstruction; reset/respawn restores
the authored start positions. White gears and racks are visual only.
The horizontal rack fills three pixels inside the 56-pixel platform,
on its upper face for floor-level bridges and lower face for elevated bridges,
with no overhang. Matching 24-pixel wall/platform travel keeps the fixed output
pinion engaged. The vertical rack translates rigidly and can enter its lower
housing. Gear carrying also checks spike sweeps. Newly solid terrain waits while
a player, gear solid or dash platform occupies it.

Open the demo in the resident Editor, apply `BackgroundEditor.Run`, enter Play,
then run `Verification/GearLiveInput.cs` via Pipeline `run_script`, entry
`GearLiveInput.Run`, with CLI `--timeout 90` and script `--timeout_ms 90000`.
It checks up/down/hold/release
and replays a complete keyboard route through the white platform, green/red
landings and resting ledge. Captures go to `TestResults/GearDemo/`; the script
restores its keyboard, input focus and fixed capture timing settings.

## Eight-room demo

Open `Assets/Scenes/PhaseDemo1.unity`. Rebuild the eight scenes from
`Assets/Phase/DemoRooms.json` through Pipeline with
`unity command eval 'PhaseSandboxBuilder.CreateDemoRooms();'`.
Each room uses `checkpointOnEntry`, resetting player color/dash/stamina and
mechanism positions while preserving elapsed time and action/death counts.
Built-in levels link in both directions: Demo1–8, Crystal1–3, then Demo12. Backward travel enters a safe standing position near the previous room’s right side. Level12’s right exit freezes play and shows the victory screen; jump restarts level1 in native Play, and Web also offers replay or level selection. Custom maps have no level links. The original sandbox links retain their momentum/color carry behavior.
See [room designs and live input verification](Demo.md).
`BuildWeb.py` verifies these tests and builds `PhaseDemo1` through
`PhaseDemo8`, followed by `PhaseCrystal1` through `PhaseCrystal3` and the hand-authored `PhaseDemo12`. `DemoWebSmoke.cjs` replays keyboard routes through
all twelve Web scenes and checks in-game level select, victory, key settings, sizing, fullscreen and loading failure.
The older `WebSmoke.cjs` remains a verifier for the original two-sandbox build.

## CI

GitHub Actions runs the production controller and the same controller/demo
route tests on .NET 10, including the 40 immutable reference traces.
The Unity-only authoring-deserialization case runs through Pipeline.
The test fixture parser uses Json.NET, already included by Unity Pipeline.
The hosted runner requires no Unity license or repository secrets.

```sh
dotnet test Verification/ControllerTests.csproj --configuration Release
```

CI runs on every pull request (including stacked PRs into feature branches) and pushes
to `main`. It uploads TRX test results and runs `WebPageBrowser.cjs` in headless
Chromium against a mock Unity bridge; that covers page events, not native loading.
Unity scene, keyboard and rendering checks remain live CLI/Pipeline checks;
they are not covered by this hosted controller job. The separate
[Web build and preview](Web.md) workflow runs them through a licensed GUI runner
and deploys only after browser verification. It requires owner setup before use.

The view always contains at least the original 320x180 native-pixel play area.
At wider aspects, height stays180 and width grows; at taller aspects, width
stays320 and height grows around the vertically centered180-pixel play area.
Stone fills the area above the native top boundary; terrain touching the last
complete 8px row (bottom176) extends below. Gaps and voids remain open, with a
short fade to black between native Y180 and the existing Y194 death boundary, followed by quiet receding quarry terraces. This extension is visual padding. Each built-in demo also has a visible, solid 8px ceiling at Y0; custom maps use their authored terrain.
Both axes fill the viewport without stretching native pixels.
Terrain ending at the right room edge and room bounds expand above320pixels. Resizing preserves
controller state and rebuilds the pixel surface/cache; resizing during a slide
completes its handoff, including a height-only resize.

## Phase masks and spikes

`Block(..., phase: -1, phaseMask: 0)` keeps the existing legacy phase behavior.
An explicit mask overrides phase metadata: R=1, B=2, G=4, Y=5, M=3, C=6, W=7.
Masks3/5/6 use paired inlays showing their two active phases; mask7 keeps the
current white permanent-solid palette. Only newly solid overlapping blocks defer
materialization; previously solid mixed blocks do not become ghosts on a shared
phase transition.

`PhaseTerrain.phaseMask` uses0 for the legacy phase field or1–7 for an explicit
mask. `PhaseSpike` uses `direction` (Up/Down/Left/Right), `length` in8px cells and
`phaseMask`1–7. Its transform follows the existing native-X/negative-Unity-Y
convention. The pure controller `Spike(x,y,length,direction,phaseMask)` takes its
length in native pixels (positive multiples of8).

Pass `spikes:` by name when constructing `PhaseController`; the gear branch also
adds a `gears:` parameter and positional fifth arguments are not interchangeable.
Spikes are hazards, never solid blocks. Their lethal strip is the drawn3px root:
Up offsets Y by5; Left offsets X by5; Down/Right use the top/left3px. All four
orientations kill on swept overlap in an active phase, including dash, phase
changes and moving-platform carry. Inactive spikes keep dark teeth and a first
mixed-color marker. Respawn restores the original player/platform state.
`CheckSpikes(before)` checks spikes alone. Movement hooks call
`CheckHazards(before)` inside each step with a snapshot of the pre-move player
bounds so both spikes and lasers are checked.

For an isolated connected-Editor probe, run `Verification/HazardScene.cs` with
entry `HazardScene.Run`, then enter Play and run `Verification/HazardLiveInput.cs`
with entry `HazardLiveInput.Run`. The first helper creates only
`Assets/Temp/PhaseHazardProbe.unity`; it does not replace a product scene or add
levels to Build Settings. The live script checks component collection, keyboard
jump activation/death, respawn and phase-preserving dash.

## Runtime map bridge

The active `PHASE` object accepts `SelectDemoRoom("1".."12")` and
`LoadCustomMap(json)`. Scene selection uses `PhaseSandbox.Levels` (Demo1–8, Crystal1–3, Demo12), which also defines both horizontal links.
Custom maps use the `PhaseDemoRoom` fields `name`, `blocks`, `spikes`, `spawnX`,
`spawnY` and the optional arrays `lasers`, `crystals`, `platforms`, `gears`; blocks require
explicit `phaseMask`1–7. Spike direction is Up0/Down1/Left2/Right3; length is native pixels.
A platform is `{w,h,nodeX[],nodeY[],returns}` with at least two map-coordinate stops, starting
at the first. A gear is `{wall:{x,y,w,h},platform:{x,y,w,h},top,endX}`, the
`PhaseClimbGear` wall start, wall top limit, platform start and platform end X.

Custom geometry occupies non-overlapping8px cells within320×176, at most880cells.
Spawn X is0–312 and Y0–160 on the same grid, with an8×11 player clear of initially
red-active terrain, spike hitboxes, platforms and gears. Platform and gear start positions
occupy cells like other objects; every platform stop, the wall top and the bridge end must
also lie on the grid inside the map, and gears must satisfy `ClimbGear`'s equal-travel and
pinion rules. Empty terrain, invalid numbers/masks, non-grid geometry and the built-in single
mover/gear fields (`nodeX`, `gearWall`, …) are rejected. JSON is limited to
200000characters. Validation completes before replacing the current runtime root;
no saved scene changes. The runtime map has no room-edge links.

Web completion dispatches `window` event `phase-map-result` with
`detail:{success:boolean,message:string}`. Scene success waits for asynchronous
loading; custom-map success waits for the new root's controller and pixel surface.
Invalid input preserves the current map and produces a failure callback without
logging a gameplay error. `PhaseSandbox.LastMapError` exposes the last failure
message for native verification and clears on success. `PhaseCustomMap.ToJson` writes the
five base fields plus each optional array only when it is used, so older maps load unchanged.
The runtime uses the installed Newtonsoft.Json parser with a strict schema, rejecting
unknown/duplicate fields and non-numeric coordinates before constructing the room; it
preserves absent arrays as null. `CreateCustom` turns platforms and gears into
`PhaseMovingPlatform`/`PhaseClimbGear` children of the map root, so their nodes and limits
are root-relative. Parsing and controller validation run in the seven `CustomMapTests`;
`MapBridgeTests` adds one Play Mode import/keyboard/respawn/scene-selection case.

For live verification, enter a demo in Play mode and run
`Verification/MapLiveInput.cs`, entry `MapLiveInput.Run`, with CLI timeout90seconds
and script timeout_ms90000. It loads a static map, uses queued keyboard input for
move/jump/dash and spike death, verifies local respawn and then selects demo2.
It requires at least100advancing frames plus controller time, writes captures and
a report to `TestResults/CustomMap`, and restores temporary input/background/timing
settings. Inspect the live Console separately and capture the actual Game View
through Pipeline as in the normal live check.

## Continuous lasers

`PhaseLaser` authors an 8×8 emitter with `LaserDirection` Up/Down/Left/Right
and `phaseMask` 1–7. Its transform uses native X and negative Unity Y, like
terrain and snaps to the same8px Grid. `DemoLaser` stores the same fields in `DemoRooms.json`; rebuilding
the rooms collects these into the saved scenes. All seven masks appear in
the eight-room demo.

Lasers are continuous, with no timer or pulse. The 4px beam starts at the
emitter cell's mouth and ends at the nearest current solid or the room edge.
White is always lethal; other masks use the same active phases as terrain
and spikes. Inactive beams remain visible at reduced opacity. Ghost and
deferred terrain do not block beams. Dash platforms and both gear parts do;
decorative racks/gears do not. Moving cover updates the beam immediately.
The emitter artwork is not an additional solid collider.

`Laser.Beam` supplies the same bounds to rendering and swept hazard checks.
Every player movement hook uses `CheckHazards`, including jumps that change
phase, dash correction, moving-platform carry and gear displacement. Death
uses the existing checkpoint reset and preserves the authored emitters.
The renderer adds a dark 1px surround, bright 2px core and static contact cap;
safe beams omit the surround and blend into the backdrop. No flashing or text.

Run `LaserTests` through Pipeline `run_tests --mode editor`, then `PixelTests`.
`Verification/LaserLiveInput.cs`, entry `LaserLiveInput.Run`, verifies the
authored white gear shield and a colored beam's keyboard-triggered activation
and respawn. It restores its temporary input/settings and controller content.

## Mechanism Prefabs

Paint static terrain with `PHASE/Paint Grid terrain`. Place reusable mechanisms
from `Assets/Phase/Prefabs/` under the scene's `PHASE` Grid, or use `PHASE/Add`:
`DashPlatformHorizontal`, `DashPlatformVertical`, `ClimbGear`, `Laser`, `Spikes`,
`JumpCrystal`, `DashCrystal`.
Both dash platform prefabs keep a horizontal body; only their travel differs.
The menu places new instances at native (80,120), with Undo support.

Move or duplicate the **Prefab root** to move the complete mechanism and route.
Keep rotation0/scale1 and use Unity's8px move snapping. Select its `Platform` or
`Mechanism` child to edit sizes and routes in the Inspector. Platform `nodes`
are native X/Y-down offsets from their parent Prefab root, not world positions.
For example, `(0,0)`→`(0,-64)` moves upward64pixels from the placed root.
Gear `wallTop`, `platformStart` and `platformEndX` use that same parent-relative
coordinate system. Laser and spike prefabs use their own Transform position.
Native collider shapes and all route endpoints still snap through the Unity Grid.

Each instance can override its route, size, direction and phase mask independently.
Crystal Prefabs expose `kind` and `phaseMask`; both use an8×8 native trigger.
Apply overrides only when the change should affect the shared Prefab.
The demo builders instantiate these assets and preserve the existing room geometry;
hand-authored scenes are never rebuilt. Existing direct children of the origin
`PHASE` root retain their coordinates. The in-game editor places single-cell lasers
and crystals, a one-way dash-platform line, and two-step climb gears; these Prefabs are the Unity authoring tools.

## Title and level editor

`PhaseTitle` is build scene0 and `PhaseEditor` follows the levels. Recreate both and
their Build Settings entries with `unity command eval 'PhaseSandboxBuilder.CreateScreens();'`.
Each has a `PHASE` root (`PhaseScreen`) answering the same Web bridge messages as
`PhaseSandbox`; any key held when a screen opens is ignored until released.

The title draws the PHASE banner and four icons: left/right (arrows or movement keys)
chooses, jump or Enter confirms (or click an icon). Play loads level1, the grid opens
level select, the pencil opens the editor and the keyboard opens key settings.
Escape in a level (not an editor playtest) pauses play like the Web dialog gate, with
resume, level select, key settings and back-to-title icons; Escape resumes, and keys
held to close it stay ignored until released. The level12 victory banner has replay-level1
and level-select icons.
Level select is a 4×3 grid of pixel-digit tiles; every level is always selectable, it
opens at the current level from pause, and Escape goes back. Key settings show seven
action icons with two key caps each, labelled in a tiny pixel font. Arrows and Enter
always navigate. Enter makes a slot blink and binds the next key; Escape cancels and
Backspace clears. The bottom icon restores the defaults. A rejected change flashes a red
border and keeps the old map; Escape closes the screen.
The editor paints the custom-map grid (40×22 cells, terrain, four spike directions,
four laser directions, jump and dash crystals, dash platforms, climb gear,
spawn, erase; seven masks) and renders it with the game's own pixels in the starting
red phase. Mouse: left paints, right erases, the toolbar flips away from the cursor.
Keyboard: movement keys move the cursor, jump paints, grab erases, dash cycles the
tool, 1–7 pick the mask, Enter playtests, Escape returns to the title.
A dash platform starts with one click or jump on the 8px grid. That cell is the 24×8 origin.
Each later click adds the next stop, and a stop may differ on both axes. Those stops are one
platform, `{w:24,h:8,nodeX,nodeY,returns:false}`; each dash advances it to the next stop.
Escape, grab or right-click ends the line, or drops a start that has no stop yet. A gear's first
point is the 8×56 wall's top-left; the second picks the 56×8 bridge's row and end column. The bridge
starts two cells right of the wall and travels 8–24px, matching the wall's upward travel. Escape, grab
or right-click also cancels a pending gear wall. Bodies refuse occupied cells (red flash); erasing any
cell of a body removes it. Dotted segments and faint outlines mark the platform line, the wall top and
the bridge end. An invalid
map flashes a red border instead of starting. The playtest runs a `PhaseSandbox`
child named `Playtest`; Escape destroys it and returns to the unchanged draft. A Web
`LoadCustomMap` sent in the editor scene plays that map the same way. The draft
autosaves to PlayerPrefs `phase.editor.v1` as one tool/mask pair per cell
(tool codes 1 terrain, 2–5 spikes, 7–10 lasers, 11–12 crystals), followed by the platforms and
gears as compact `{platforms,gears}` JSON; drafts saved before that suffix still load.
The two arrow icons after the colors import (up) and export (down) custom-map JSON.
On Web, export calls `PhaseExportMap` and the page downloads `phase-custom-map.json`;
import calls `PhaseImportMap`, the page opens a file picker and sends the text to
`PHASE.ImportDraft`. In the Unity Editor and other players both use the clipboard.
Export validates first; an invalid map or file flashes the red border and keeps the draft.

With `PhaseTitle` in Play mode, run `Verification/MenuLiveInput.cs`, entry
`MenuLiveInput.Run`. It drives title→editor→paint terrain, a laser and a crystal→draw a diagonal platform
line and a gear→clipboard export/import→playtest with a dash along that diagonal→back→level1 by keyboard,
restores the developer's draft and clipboard and saves captures to `TestResults/Menu/`.
`Verification/KeysLiveInput.cs`, entry `KeysLiveInput.Run`, also starts from `PhaseTitle`
in Play: title key settings→bind J to jump (a duplicate is rejected)→title level
select→level1 jumps with J→Escape pause freezes play→pause level select→level2→
restore defaults→back to title. It restores the saved bindings and writes captures to
`TestResults/Keys/`.

## Hand-authored scenes

`Assets/Scenes/PhaseDemo1.1.unity` is a separate authoring scene. It uses the same
8px Grid and native collider shapes; imported sizes, phase masks and scene links
are retained, with positions snapped by at most half a cell. Edit it with the Grid
painter and normal Inspector tools. It is not included in the explicit
Web build or the in-game level select. Use `PhaseSandboxBuilder` only to rebuild
generated demo scenes; preserve hand-authored layouts.

## Level routes

Each level in `PhaseSandbox.Levels` has a recorded keyboard route in
`Verification/Routes/<Scene>.json`: an array of held-key steps
(`{"frames","x","y","jump","dash","grab"}`, y -1 is up), pressed from a reset room;
jump and dash fire when their key goes down. After the last step the checks hold
right. `LevelRouteTests` (Edit Mode, controller) and `LevelRoutePlayTests` (Play Mode,
keyboard) require the right-edge exit, or the level-12 victory, with no deaths.
The optional `<Scene>.bypass.json` lists shortcut inputs as
`[{"name","steps":[...]}]`; each must not reach the right exit (replay stops at
the first death). Rerecord a route whenever its scene changes.

`Verification/RouteTool.cs` builds the `PhaseController` from a scene, as Play does,
at 320px with the right exit open. Pass a level name or a scene path:

```sh
unity command run_script --project-path "$PWD" --result-only -- --file Verification/RouteTool.cs --entry RouteTool.Validate --args '["PhaseDemo12"]'
unity command run_script --project-path "$PWD" --timeout 900 --result-only -- --file Verification/RouteTool.cs --entry RouteTool.Search --timeout_ms 900000 --args '["Assets/Scenes/Drafts/Color1.unity","","{\"feet\":136,\"minX\":96,\"dash\":true}","x:-1,0,1 y:-1,0 j:0,1 d:0,1 g:0",4,25,"Temp/route.json"]'
unity command run_script --project-path "$PWD" --timeout 900 --result-only -- --file Verification/RouteTool.cs --entry RouteTool.Harden --timeout_ms 900000 --args '["PhaseDemo12","Temp/route.json",300,1,"Temp/route.json"]'
```

`Search` runs a breadth-first search from the end of a prefix route ("" starts at spawn)
and writes prefix plus found steps. The goal is `{"exit":true}` or a standing target
using `minX`/`maxX` (player X), `feet` (platform top), `dash` (dash ready),
`movers` (0 authored, 1 toggled; all settled) and `minY`/`maxY` to prune.
Keys list the values tried per action; each choice is held for the given frames,
up to the given depth. Search cost grows quickly; chain short segments by
passing each output as the next prefix. `Harden` keeps the route winning while
reducing the one-frame step shifts that break it; browser timing can shift a step.

## Colored crystals

`PhaseCrystal` authors an8×8 native pickup trigger on the shared8px Unity Grid,
using native-X/negative-Unity-Y coordinates. Its collider tracks active/collected state.
`kind` is Jump (round) or Dash (square); `phaseMask` is1–7 using the same seven
colors as terrain and lasers. White works in every phase; other colors can only
be collected in an active phase and appear faint otherwise. Pickups never change
phase. A round crystal stores at most one air jump; releasing and pressing jump
spends it and advances phase through the usual jump action. Ground, coyote and
wall jumps take priority. The cloak's small hollow ring shows the stored jump.
A square crystal unconditionally sets the dash charge to1, even when it was
already full or a dash is in progress, without cancelling that dash or refilling
stamina. Actual landing clears unused air jump and rearms consumed crystals;
death/reset also restores them. There is no respawn timer. Swept movement and
platform/gear carry collect pickups; lethal hazards take priority.

The three rooms in `Assets/Phase/CrystalRooms.json` follow `PhaseDemo8` and are
also independently playable as `Assets/Scenes/PhaseCrystal1.unity` through `PhaseCrystal3.unity`.
They introduce airborne recoloring, a ten-emitter ascent and a same-color
midair dash relay. Rebuild only these three scenes through the connected Editor:
`unity command eval 'PhaseSandboxBuilder.CreateCrystalRooms();'`.
The manually edited `PhaseDemo1.1` is separate from these builders.
`DemoLiveInput.Run` checks either demo or crystal scenes and saves keyboard-route
captures under `TestResults/DemoRooms/` or `TestResults/CrystalRooms/`.

`PhaseDemo12` is the classmate-authored level formerly named `PhaseDemo1.2`; it is included in the Web build and selectable as **12**. Its fourteen platform instances retain their authored routes, sizes and start nodes as scene overrides. Shared dash-platform Prefabs keep their original grid-aligned horizontal defaults; edit instance overrides for a single level. Preserve this handmade scene when rebuilding generated demos.
