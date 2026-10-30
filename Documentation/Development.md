# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.6.7f1** GUI Editor. Scene construction, tests, input checks,
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
open -g -j -n -a /Applications/Unity/Hub/Editor/6000.6.7f1/Unity.app --args -projectPath "$PWD"
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
starting the next test run. There are 18 controller cases, 7 laser cases, 8 crystal cases, 15 pixel cases, 12 platform cases,
4 Grid authoring cases, 11 custom-map cases (validation, version migration, newer-version refusal, round trips, the chaos route), 6 scrolling-room cases, 17 world cases
(map format v3, room changes, open top, respawns, goal, camera), 4 key-binding cases
(validation, PlayerPrefs persistence, key caps, volume levels) and 1 level-route case in Edit Mode. The 30 Play Mode cases cover a big-room camera
following a keyboard run, bindings, pause/release gating, room persistence, crystal triggers, imported maps and jump platforms, the
title/editor flow, the editor's fixed tool panel under mouse input, room creation, merging, splitting and moving in the editor, the pause menu with key
settings, the fullscreen control, and level select from the title, pause and victory screens.
The two reference files preserve 12 movement traces (running, jumps, buffers, coyote time, falling, wall
slide and wall jump) recorded before dash and climbing were removed; their expectations are unchanged.
Production steps at 1/60 s; reference replays use the recorded 0.0166667 s.

Before tests or live Play checks, `BackgroundEditor.Run` sets every existing
Game/Simulator view to Play Unfocused. This is an Editor layout preference, not
a game setting; Unity’s public `SetPlayModeFocused(false)` maximizes the view
instead. The script fails if the installed Editor API changes or no view exists.
Run it with Play stopped. It also initializes Unity 6000.6's local
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
unity command eval 'UnityEngine.Application.runInBackground = true;'
```

Project Settings keep Run In Background on, and an Edit Mode `Application.runInBackground`
assignment writes that setting, so end with `= true`; setting it false dirties
`ProjectSettings.asset`. Both waits must return `met: true`; the
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
and X then Y pixel stepping. Moving into a ledge while pressing toward it steps the player up onto it
when its top is at most 4px above the feet and the raised spot is clear of active solids, spikes and beams. Terrain transforms use Unity X/Y as
pixel coordinates with Y negated; spawn uses screen coordinates.
`PhaseSandbox.rightScene` links to `PhaseRoom2`; its `leftScene` returns to the
sandbox. Crossing an assigned horizontal edge loads that scene asynchronously,
places the player at the opposite edge at the same height, and carries color,
velocity, jump state and counters. Empty links keep the side closed. Both
scenes must be in Build Settings and in the Web build's explicit scene list.
Deaths respawn in the current room. The room top is open air and death is below room height+14
(194 for the default room) unless the player's center is in a neighboring room of a world; the immutable reference fixtures stay unchanged and there is no player text.
The game binds three actions: left (←/A), right (→/D) and jump (Z/Space); there is no crouch or fast fall.
Up and down are not actions: ↑/↓ move through menus and the editor cursor and cannot be bound.
Each action has two independent key slots; logical `Shift` activates both physical
Shift keys. `PhaseSandbox.ApplyControls` takes 6 key names in left/right/
jump order (two adjacent slots per action, "" for an empty slot). It accepts
only the keys the settings screen can label (letters, digits, ←/→, Space, Shift,
Enter, Backspace and common punctuation), rejects duplicate physical keys and actions
with no key, then applies the entire map atomically. Settings save the map to
PlayerPrefs `phase.keys.v1` as comma-separated names; startup loads it and falls back
to the defaults when it is missing or invalid.

## Worlds

Custom maps are worlds of connected rooms (map format v3). `PhaseWorld` holds the rooms, `PhaseWorldPlay` keeps one live
`PhaseController` for the current room and moves play between rooms in place; both are plain C# compiled into the controller
tests. See [Rooms](Rooms.md) for the rules and the file format.
In PhaseSandbox Play, `Verification/WorldLiveInput.cs` (entry `WorldLiveInput.Run`, script `--timeout_ms 120000`) loads a four-room world, walks
across both seams into the goal, reloads it and jumps up into the room above with the virtual keyboard, saving frames to `TestResults/World/`.

## Scrolling room demo

`Assets/Scenes/Drafts/ScrollTest.unity` is a 640×360 room with a long floor run and three tall steps
built for the removed wall climb; it has no room links and is not in the build. Recreate it with
`unity command eval 'PhaseSandboxBuilder.CreateScrollTest();'`. In Play, call
`Verification/ScrollLiveInput.cs` (entry `ScrollLiveInput.Run`, CLI `--timeout 90`, script
`--timeout_ms 90000`) twice, capturing the Game View after each: run, then reach the wall. Each call drives the keyboard, checks that the view never leaves the
room and moves as expected, waits for the camera to settle and saves the frame to `TestResults/ScrollTest/`.

## Level course

Open `Assets/Scenes/PhaseDemo1.unity`. Rebuild this opening room from
`Assets/Phase/DemoRooms.json` through Pipeline with
`unity command eval 'PhaseSandboxBuilder.CreateDemoRooms();'`.
Each level uses `checkpointOnEntry`, resetting the player color while preserving elapsed time and
jump/death counts.
Built-in levels link in both directions: Demo1, the classmate color room Demo1.1, the laser rooms Demo13 and Demo14, the chaos level Demo15, Umbrella, then the moving-platform room Gear. Backward travel enters a safe standing position near the previous room’s right side. The last level’s right exit freezes play and shows the victory screen; jump restarts level 1 in native Play, and Web also offers replay or level selection. Custom maps have no level links. The original sandbox links retain their momentum/color carry behavior.
See [the level course](Demo.md).
`BuildWeb.py` verifies these tests and builds `PhaseDemo1`, `PhaseDemo1.1`, `PhaseDemo13`, `PhaseDemo14`, the chaos level `PhaseDemo15`, `PhaseUmbrella`, and `PhaseGear`.
In PhaseGear Play, `Verification/GearLiveInput.cs` (entry `GearLiveInput.Run`) plays the intended route, every floor
blue bar, green platform, red bar or platform, with the virtual keyboard; it fails on a death or a ceiling bonk.

## CI

GitHub Actions runs the production controller and its
tests on .NET 10, including the 12 immutable movement reference traces.
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
they are not covered by this hosted controller job. The Web build is made and
published by hand; see [Online play](Web.md#online-play).

`PhaseSandbox.roomSize` sets the room in native pixels: default and minimum 320×180, width
in 8px steps and height 180 or a multiple of 8. The Scene view draws it as a white gizmo from the
Grid origin. Side exits sit at the room's left/right edge, lasers end at the room edges, and
`PhaseController(width:, height:)` takes the same size. An axis larger than the view scrolls:
`PhaseCamera` eases the view origin toward the player each 60 Hz step, rounds it to whole pixels
and clamps it inside the room. `PhasePixels` caches the backdrop and terrain once per phase over
the whole room and copies the visible window each frame; a taller room keeps the skyline at its
bottom with open sky above. Room slides arrive at the opposite edge at the same height,
clamped into a room of different height; vertical exits are not supported. Custom maps and the
in-game editor choose their own room size, 320×176 by default (see below).

On an axis the room does not exceed, the view behaves as below.
The view always contains at least the original 320x180 native-pixel play area.
At wider aspects, height stays180 and width grows; at taller aspects, width
stays320 and height grows; a room shorter than the view sits at the bottom of it with sky above,
and a narrower room is centered. Terrain touching the last
complete 8px row (bottom176) extends below. Gaps and voids remain open, with a
short fade to black between native Y180 and the existing Y194 death boundary, followed by quiet receding quarry terraces. This extension is visual padding. Each built-in demo also has a visible, solid 8px ceiling at Y0; custom maps use their authored terrain, and a custom map's room has an open top.
Both axes fill the viewport without stretching native pixels. The in-game editor alone widens its view further to fit
its tool panel beside or below the 320×176 grid, keeping the screen's aspect.
Terrain ending at the right room edge and room bounds expand above320pixels. Resizing preserves
controller state and rebuilds the pixel surface/cache; resizing during a slide
completes its handoff, including a height-only resize.

## Jump platforms and spike cover

`Platform` (a `Block`) is a 1-cell-thick, odd-length body centred on a pivot cell; the controller appends platforms to
`blocks` after the terrain, so terrain keeps its indices, and their attached `Spike` rows join `spikes`. Every jump
(including wall and crystal jumps) queues one step on each platform; a step slides to the next pivot (the previous one when
`reverse`) in `Platform.SlideTime` (0.3 s), and a jump during a slide waits its turn. The controller moves platforms a pixel
at a time before the player acts: a solid platform carries a rider and pushes the player; a player that cannot be pushed
is not crushed, the platform joins `blocked` and the two pass through until clear. `Reset` returns platforms to their first pivot.
`Block.spiked` makes any block lethal on contact (half a pixel past each face) while it is solid and not in `blocked`;
it still blocks lasers in its own color. `PhaseJumpPlatform` authors a platform (`pivots` are cell offsets from the
first; the transform is the body's top-left at the first pivot; `spikes` picks the sides) and `PhaseTerrain.spiked` covers terrain.
Place the `JumpPlatform` prefab with **PHASE > Add > Jump platform**. Platforms are drawn over the cached terrain each frame.
`Verification/PlatformScene.cs` (entry `PlatformScene.Run`) builds `Assets/Temp/PhaseJumpPlatformProbe.unity`; in Play,
`Verification/PlatformLiveInput.cs` (entry `PlatformLiveInput.Run`) taps jump with a virtual keyboard while Wick stands
on the platform, checks that it steps and carries him, and saves captures to `TestResults/Platform/`.
See [the jump platform spec](JumpPlatformSpec.md).

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

Pass `spikes:`, `lasers:` and `crystals:` by name when constructing `PhaseController`.
Spikes are hazards, never solid blocks. Their lethal strip is the drawn3px root:
Up offsets Y by5; Left offsets X by5; Down/Right use the top/left3px. All four
orientations kill on swept overlap in an active phase, including phase
changes. Inactive spikes keep dark teeth and a first
mixed-color marker. Respawn restores the original player state.
`CheckSpikes(before)` checks spikes alone. Movement hooks call
`CheckHazards(before)` inside each step with a snapshot of the pre-move player
bounds so both spikes and lasers are checked.

For an isolated connected-Editor probe, run `Verification/HazardScene.cs` with
entry `HazardScene.Run`, then enter Play and run `Verification/HazardLiveInput.cs`
with entry `HazardLiveInput.Run`. The first helper creates only
`Assets/Temp/PhaseHazardProbe.unity`; it does not replace a product scene or add
levels to Build Settings. The live script checks component collection, keyboard
jump activation/death and respawn.

## Runtime map bridge

The active `PHASE` object accepts `SelectDemoRoom("1".."7")` and
`LoadCustomMap(json)`. Scene selection uses `PhaseSandbox.Levels` (Demo1, Demo1.1, Demo13–15, Umbrella, Gear), which also defines both horizontal links.
Custom maps are version 2 JSON (`format` `verto-map`, `version`, `generator`, `name`, `size` and `spawn` in cells, `terrain` rows
of `.`, `1`–`7` and spike-covered `A`–`G`, and typed `objects`: spike, laser, crystal, platform; see
[the spec](JumpPlatformSpec.md)). Files without a version are v1 (`blocks`, `spikes`, `lasers`, `crystals`, `spawnX`, `spawnY` in
pixels, explicit `phaseMask`1–7, numeric directions Up0/Down1/Left2/Right3, an older crystal's `kind` must be 0) and migrate on load;
a newer version is refused with a message to update. `PhaseCustomMap.Version` is the latest; add one entry to `Upgrades` per bump.

Custom geometry occupies non-overlapping8px cells within the room, at most one object per cell. A room is
320×176 unless the optional `width` and `height` (native pixels, multiples of 8, 320–1280 and 176–720) say
otherwise; a room that exceeds the view scrolls like any other (`PhaseSandbox.roomSize` is set to the grid, with
the one-screen height 176 played as 180). Spawn X is0 to width−8 and Y0 to height−16 on the same grid, with an8×11 player clear of initially
red-active terrain, spike hitboxes and red lasers. Empty terrain, invalid numbers/masks, non-grid geometry
and unknown fields are rejected. JSON is limited to
2000000characters (the page's import picker accepts files up to 2 MB). Validation completes before replacing the current runtime root;
no saved scene changes. The runtime map has no room-edge links.

Web completion dispatches `window` event `phase-map-result` with
`detail:{success:boolean,message:string}`. Scene success waits for asynchronous
loading; custom-map success waits for the new root's controller and pixel surface.
Invalid input preserves the current map and produces a failure callback without
logging a gameplay error. `PhaseSandbox.LastMapError` exposes the last failure
message for native verification and clears on success. `PhaseCustomMap.ToJson` always writes the latest version.
The runtime uses the installed Newtonsoft.Json parser with a strict schema, rejecting
unknown/duplicate fields and non-numeric coordinates before constructing the room; it
preserves absent object arrays as null. Parsing, migration and controller validation run in the nine `CustomMapTests`;
`MapBridgeTests` adds one Play Mode import/keyboard/respawn/scene-selection case.

For live verification, enter a demo in Play mode and run
`Verification/MapLiveInput.cs`, entry `MapLiveInput.Run`, with CLI timeout90seconds
and script timeout_ms90000. It loads a static map, uses queued keyboard input for
move/jump and spike death, verifies local respawn and then selects demo2.
It requires at least100advancing frames plus controller time, writes captures and
a report to `TestResults/CustomMap`, and restores temporary input/background/timing
settings. Inspect the live Console separately and capture the actual Game View
through Pipeline as in the normal live check.

## Continuous lasers

`PhaseLaser` authors an 8×8 emitter with `LaserDirection` Up/Down/Left/Right
and `phaseMask` 1–7. Its transform uses native X and negative Unity Y, like
terrain and snaps to the same8px Grid. `DemoLaser` stores the same fields in `DemoRooms.json`; rebuilding
the room collects these into the saved scene.

Lasers are continuous, with no timer or pulse. The 4px beam starts at the
emitter cell's mouth and ends at the nearest current solid or the room edge.
White is always lethal; other masks use the same active phases as terrain
and spikes. Inactive beams remain visible at reduced opacity. Out-of-phase terrain a beam crosses is painted again in front of the beam. Ghost and
deferred terrain do not block beams.
The emitter artwork is not an additional solid collider.

`Laser.Beam` supplies the same bounds to rendering and swept hazard checks.
Every player movement hook uses `CheckHazards`, including jumps that change
phase. Death
uses the existing checkpoint reset and preserves the authored emitters.
The renderer adds a dark 1px surround, bright 2px core and static contact cap;
safe beams omit the surround and blend into the backdrop. No flashing or text.

Run `LaserTests` through Pipeline `run_tests --mode editor`, then `PixelTests`.

## Mechanism Prefabs

Paint static terrain with `PHASE/Paint Grid terrain`. Place reusable mechanisms
from `Assets/Phase/Prefabs/` under the scene's `PHASE` Grid, or use `PHASE/Add`:
`Laser`, `Spikes`, `JumpCrystal`.
The menu places new instances at the Scene view centre, kept inside the room bounds, with Undo support.

Keep rotation0/scale1 and use Unity's8px move snapping; native collider shapes snap through the Unity Grid.
Each instance can override its size, direction and phase mask independently.
The crystal Prefab exposes `phaseMask` and uses an8×8 native trigger.
Apply overrides only when the change should affect the shared Prefab.
The demo builders instantiate these assets and preserve the existing room geometry;
hand-authored scenes are never rebuilt. Existing direct children of the origin
`PHASE` root retain their coordinates. The in-game editor places single-cell lasers
and crystals; these Prefabs are the Unity authoring tools.

## Title and level editor

`PhaseTitle` is build scene0 and `PhaseEditor` follows the levels. Recreate both and
their Build Settings entries with `unity command eval 'PhaseSandboxBuilder.CreateScreens();'`.
Each has a `PHASE` root (`PhaseScreen`) answering the same Web bridge messages as
`PhaseSandbox`; any key held when a screen opens is ignored until released.

The title draws the Verto banner and four icons: left/right (arrows or movement keys)
chooses, jump or Enter confirms (or click an icon). Play loads level1, the grid opens
level select, the pencil opens the editor and the gear opens settings.
Escape in a level (not an editor playtest) pauses play like the Web dialog gate, with
resume, level select, settings and back-to-title icons; Escape resumes, and keys
held to close it stay ignored until released. The last level's victory banner has replay-level1
and level-select icons. Menu icons share one 11×11 grid with 2px strokes (`PhaseMenu`).
Menus lay out in layout pixels and draw through `UiZoom`: the largest whole-pixel scale
that keeps the layout within 80% of the native view on both axes, centered. The title
banner, pause row, level select and settings each use it, so they grow with the view
instead of staying at 1×. Navigation reads key presses per frame, so a tap shorter than
a frame still moves the selection, and the click that refocuses the page (which re-arms
the held-key gate) still acts.
Player Settings keep Run In Background on: with it off, the Input System resets the mouse when the
page gains focus and drops the click that focused it. `maps.js` still pauses play while the page is unfocused.
Level select and settings replace the screen behind them. Level select picks 4, 5, 6, 3 or
2 columns, whichever shows the grid largest. Each tile frames a 40×22 thumbnail of the
room (one pixel per 8px cell, terrain and lasers on plain ink, no backdrop) above its 2× number; the selected tile has a blue frame.
Every level is always selectable, it opens at the current level from pause, and Escape
goes back. Thumbnails live in `Assets/Phase/Resources/Levels/<Scene>.png`; rebake them
automatically before every player build (`Phase/Editor/LevelThumbnails.cs`, an
`IPreprocessBuildWithReport`); commit the changed PNGs. To rebake without building, run
`unity command eval 'LevelThumbnails.Run();'`.
A missing thumbnail leaves its frame empty.
Settings show three action icons (left, right, jump) with two key caps each, labelled in a tiny pixel font,
then restore, fullscreen and close controls. Arrows and Enter always navigate. Enter,
jump or a click makes a slot blink and binds the next key; Escape cancels and Backspace
clears. The circular arrow restores the defaults. A rejected change flashes a red border
and keeps the old map; Escape or the close icon closes the screen. The fullscreen row is
screen corners and a switch for Unity's `Screen.fullScreen` (knob right on a lit track
when on); Enter, jump or a click toggles it. It is player state, not saved by the game:
standalone players keep Unity's own setting and Web starts windowed. Web applies the
request within that key press or click. The Editor's Game view ignores `Screen.fullScreen`,
so tests and `KeysLiveInput` swap `PhaseKeys.IsFullScreen`/`SetFullScreen`.
The editor paints the custom-map world (a grid of 40×22-cell screens; terrain, spikes and lasers in four
directions, jump crystals, jump platforms, spike cover, spawns, the goal, erase, the room tool and the move tool; seven masks) and renders it with the game's own pixels in the starting
red phase. The editor widens its own view (same aspect as the screen) so a fixed panel with one 2× icon per object type sits right
of the grid, or below it when that leaves the grid larger (tall screens); the panel never moves and never covers a
cell. Hovering a slot highlights it, a click picks it, and the selected tool and color are framed. Mouse: left paints,
right erases; a stroke paints only if it starts on the grid, so a drag from the panel never paints.
Keyboard: the left/right bindings and ↑/↓ move the cursor, jump paints, C erases, X cycles the
tool type, R/E (or the mouse wheel) turn spikes and lasers clockwise/counter-clockwise, F flips the platform under the cursor, Escape or right-click cancels a platform being placed, 1–7 pick the mask, Enter playtests, Tab shows the world overview, Escape returns to the title.
The canvas is the whole world: the cursor scrolls the 40×22-cell window when it nears an edge, the wheel scrolls rows and
Ctrl+wheel or a sideways wheel scrolls columns (a spike or laser tool keeps the wheel for turning), and a middle-button drag,
or a Ctrl+left drag, pans. Screens without a room are dimmed, faint lines mark the screens, each room has a frame (brighter for
the selected room and the room under the cursor) and a stretch shared by two rooms is dashed. Painting in a screen with no room makes a 1×1 room; erasing a
room's last content removes it. The room tool, clicked within three cells of a seam, merges the rooms on either side (refused with the red
flash if the joined rectangle would cover another room or pass 4×5 screens) or splits a room along the whole line (refused if a platform path
crosses it). The move tool picks a room (Shift adds more), a drag of more than half a screen or Ctrl with an arrow moves the chosen rooms by whole
screens, and a drop onto another room is refused. Spawn and goal are per room: a second click on a spawn takes it back, a world holds one goal, and a room
without a spawn blinks red when a playtest or export is refused. The playtest starts in the room under the middle of the window at its nearest spawn.
Tab shows every room scaled down; a click scrolls the window there. The preview is a `PhaseController` over exactly what the frame shows,
rebuilt when the draft or the scroll changes.
An invalid
map flashes a red border instead of starting; when Wick's start overlaps terrain or a hazard,
Wick's start box and the flag slot also blink red. The playtest runs a `PhaseSandbox`
child named `Playtest`; Escape destroys it and returns to the unchanged draft. A Web
`LoadCustomMap` sent in the editor scene plays that map the same way. The draft
autosaves to PlayerPrefs `phase.editor.v1` as the map file's own JSON, loaded through the same migration without the playability checks. Drafts saved before the
format change (one tool/mask pair per cell, `3,19;…`, dash crystals left empty) still load as one room. Import and export carry the whole world.
The two tray icons after the colors import (arrow out of the tray) and export (arrow into the tray) custom-map JSON.
On Web, export calls `PhaseExportMap` and the page downloads `verto-custom-map.json`;
import calls `PhaseImportMap`, the page opens a file picker and sends the text to
`PHASE.ImportDraft`. In the Unity Editor and other players both use the clipboard.
Export validates first; an invalid map or file flashes the red border and keeps the draft.

With `PhaseTitle` in Play mode, run `Verification/MenuLiveInput.cs`, entry
`MenuLiveInput.Run`. It drives title→editor→paint terrain, a laser and a crystal→mouse panel and grid
checks→clipboard export/import→playtest→back→level1 by keyboard,
restores the developer's draft and clipboard and saves captures to `TestResults/Menu/`.
`Verification/WorldEditorLiveInput.cs`, entry `WorldEditorLiveInput.Run`, starts from `PhaseTitle` in Play and drives the editor with a virtual
keyboard and mouse: paint two screens→merge them with the room tool→split them→place spawns and a goal→playtest across the seam. It restores the
developer's draft and clipboard and saves captures to `TestResults/WorldEditor/`.
`Verification/KeysLiveInput.cs`, entry `KeysLiveInput.Run`, also starts from `PhaseTitle`
in Play: title settings→bind J to jump (a duplicate is rejected)→title level
select→level1 jumps with J→Escape pause freezes play→pause level select→the next level→
restore defaults→fullscreen switch on with Enter, off with jump→back to title. It restores the saved bindings and writes captures to
`TestResults/Keys/`.

## Hand-authored scenes

`Assets/Scenes/PhaseDemo1.1.unity` is the classmate color room, selectable as **2** between `PhaseDemo1` and `PhaseDemo13`. It uses the same
8px Grid and native collider shapes; imported sizes, phase masks and scene links
are retained, with positions snapped by at most half a cell. Edit it with the Grid
painter and normal Inspector tools. It is included in the explicit
Web build and the in-game level select. Use `PhaseSandboxBuilder` only to rebuild
generated demo scenes; preserve hand-authored layouts.

## Colored crystals

`PhaseCrystal` authors an8×8 native pickup trigger on the shared8px Unity Grid,
using native-X/negative-Unity-Y coordinates. Its collider tracks active/collected state.
It is a round jump crystal; `phaseMask` is1–7 using the same seven
colors as terrain and lasers. White works in every phase; other colors can only
be collected in an active phase and appear faint otherwise. Pickups never change
phase. A crystal stores at most one air jump; releasing and pressing jump
spends it and advances phase through the usual jump action. Ground, coyote and
wall jumps take priority. The cloak's small hollow ring shows the stored jump.
Actual landing clears unused air jump and rearms consumed crystals;
death/reset also restores them. There is no respawn timer. Swept movement
collects pickups; lethal hazards take priority.

No built-in level places a crystal; custom maps place them from the in-game editor.
The manually edited `PhaseDemo1.1` is separate from the builders.

`PhaseDemo13` and `PhaseDemo14` are the later classmate laser rooms (`Assets/Phase/Imported/level-a.json` and `level-b.json`). They follow the color room, selectable as **3** and **4**. `PhaseDemo15` (select **5**) is the four-screen chaos level from `Assets/Phase/Imported/chaos.json`, which `python3 Verification/ChaosLevel.py Assets/Phase/Imported/chaos.json` generates ([design](ChaosLevel.md)). The four-screen [Umbrella](Umbrella.md) level `PhaseUmbrella` (`Assets/Phase/Imported/umbrella.json`) follows as **6**, then `PhaseGear` (**7**, `Assets/Phase/Imported/level-gear.json`), a moving-platform room on `PhaseDemo1.1`'s layout; the builder also imports jump platforms. Leaving level 7 to the right wins. Level 3 has a recorded route, `Verification/Routes/PhaseDemo13.json`; `LevelRouteTests` replays it on the saved scene and expects the right exit with no deaths. Re-record it whenever `level-a.json` changes. Recreate the five scenes through Pipeline with `unity command eval 'PhaseSandboxBuilder.CreateImportedLevels();'`; it rewrites `PhaseDemo13` and `PhaseDemo14` with new object IDs, so discard those diffs when only the chaos, umbrella or gear level changed.

## Press images

`python3 Verification/press_art.py` draws all press art as pixel art and enlarges it without
smoothing. The motif is the phase shift: a light letterform trailed by red, blue and green
echoes on the game's night-sky palette; the Verto lettering is read from `PhaseTitle.cs`.
It writes the itch.io cover `Documentation/press/cover.png` (960×480: 2:1 for itch link cards, logo inside the centre 1.25:1 grid crop), the link preview
`social.png` (1280×640), the icon `icon.png` (256×256, three overlapping red, blue and green V's on a dark tile), the default
Player icon `Assets/Phase/Art/AppIcon.png` and the Web favicon.
