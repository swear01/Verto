# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.4.7f1** GUI Editor. Scene construction, tests, input checks,
captures and Console inspection all run through the live Pipeline connection.
Do not use direct Editor `-batchmode` / `-executeMethod` invocations or cold
`unity run` / `unity test` jobs for this workflow. Fix a failed Pipeline connection
before continuing. Reuse the connected Editor instead of reopening it.
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
starting the next test run. There are 32 controller cases and 13 demo-route/authoring cases, one 48-frame art
comparison (permanent-solid colors remapped outside the deliberately revised character), one bidirectional pixel-slide
test, one adaptive-view test, one centered-view stone/void extension test, one
dash-charge hood lifecycle test, one helmet-fatigue/recovery test, one checkpoint-respawn cue test, one mixed-inlay/spike rendering test, three
gear-rack attachment positions, one full-travel rigid-rack/engagement test, and three
Play Mode cases, including custom bindings, pause/release gating and room persistence.
The three immutable reference files preserve 40 movement/action/climb traces.
The exhaustion case uses explicit revised Y/speed expectations for its final17
frames under the new45px/s held-grab cap; all earlier frames and other traces
retain their original expectations. None of the immutable reference files are edited.
Production steps at 1/60 s; reference replays use the recorded 0.0166667 s.

Before tests or live Play checks, `BackgroundEditor.Run` sets every existing
Game/Simulator view to Play Unfocused. This is an Editor layout preference, not
a game setting; Unity’s public `SetPlayModeFocused(false)` maximizes the view
instead. The script fails if the installed Editor API changes or no view exists.
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
pixel coordinates with Y negated; platform nodes and spawn use screen coordinates.
`PhaseSandbox.rightScene` links to `PhaseRoom2`; its `leftScene` returns to the
sandbox. Crossing an assigned horizontal edge loads that scene asynchronously,
places the player at the opposite edge at the same height, and carries color,
velocity, jump/dash state and counters. Empty links keep the side closed. Both
scenes must be in Build Settings and in the Web build's explicit scene list.
Deaths respawn in the current room. Top/bottom behavior and immutable reference
fixtures stay unchanged; there is no player text.
Default movement, dash aiming and climb bindings accept arrow keys or WASD.
Web settings expose two independent slots per action; changing one preserves the
other. Logical `Shift` activates both physical Shift keys. The Web bridge sends
14 strings to `ConfigureControls`, in left/right/up/down/jump/dash/grab order,
with two adjacent slots per action; empty strings disable a slot. The native
validator rejects duplicate physical keys, reserved keys and actions with no key,
then applies the entire map atomically. Browser storage/migration is described
in [Web preview](Web.md).

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
The horizontal rack fills the lower three pixels inside the 56-pixel platform,
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
The original sandbox links retain their momentum/color carry behavior.
See [room designs and live input verification](Demo.md).
`BuildWeb.py` verifies these tests and builds only `PhaseDemo1` through
`PhaseDemo8`, in that order. `DemoWebSmoke.cjs` replays keyboard routes through
the eight Web scenes and checks settings, sizing, fullscreen and loading failure.
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

CI uploads TRX test results on pull requests and pushes to `main`.
Unity scene, keyboard and rendering checks remain live CLI/Pipeline checks;
they are not covered by this hosted controller job. The separate
[Web build and preview](Web.md) workflow runs them through a licensed GUI runner
and deploys only after browser verification. It requires owner setup before use.

The view always contains at least the original 320x180 native-pixel play area.
At wider aspects, height stays180 and width grows; at taller aspects, width
stays320 and height grows around the vertically centered180-pixel play area.
Stone fills the area above the native top boundary; terrain touching the last
complete 8px row (bottom176) extends below. Gaps and voids remain open, with a
short fade to black between native Y180 and the existing Y194 death boundary. This is visual padding; collision stays unchanged.
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
`CheckSpikes(before)` is the hook for other mechanisms that displace the player;
call it inside each movement step with a snapshot of the pre-move player bounds.
The separate gear/room worktree owns integration of that hook in its gear carry.

For an isolated connected-Editor probe, run `Verification/HazardScene.cs` with
entry `HazardScene.Run`, then enter Play and run `Verification/HazardLiveInput.cs`
with entry `HazardLiveInput.Run`. The first helper creates only
`Assets/Temp/PhaseHazardProbe.unity`; it does not replace a product scene or add
levels to Build Settings. The live script checks component collection, keyboard
jump activation/death, respawn and phase-preserving dash.

## Runtime map bridge

The active `PHASE` object accepts `SelectDemoRoom("1".."8")` and
`LoadCustomMap(json)`. Scene selection uses only the eight built demo scene names.
Custom maps use the `PhaseDemoRoom` fields `name`, `blocks`, `spikes`, `spawnX`,
`spawnY`; blocks require explicit `phaseMask`1–7 and the Web editor sends
`phase:-1`. Spike direction is Up0/Down1/Left2/Right3; length is native pixels.

Custom geometry occupies non-overlapping8px cells within320×176, at most880cells.
Spawn X is0–312 and Y0–160 on the same grid, with an8×11 player clear of initially
red-active terrain and spike hitboxes. Empty terrain, invalid numbers/masks,
non-grid geometry, moving nodes and gear data are rejected. JSON is limited to
200000characters. Validation completes before replacing the current runtime root;
no saved scene changes. The runtime map has no room-edge links.

Web completion dispatches `window` event `phase-map-result` with
`detail:{success:boolean,message:string}`. Scene success waits for asynchronous
loading; custom-map success waits for the new root's controller and pixel surface.
Invalid input preserves the current map and produces a failure callback without
logging a gameplay error. Controller validation runs in the three `CustomMapTests`;
`MapBridgeTests` adds one Play Mode import/keyboard/respawn/scene-selection case.

For live verification, enter a demo in Play mode and run
`Verification/MapLiveInput.cs`, entry `MapLiveInput.Run`, with CLI timeout90seconds
and script timeout_ms90000. It loads a static map, uses queued keyboard input for
move/jump/dash and spike death, verifies local respawn and then selects demo2.
It requires at least100advancing frames plus controller time, writes captures and
a report to `TestResults/CustomMap`, and restores temporary input/background/timing
settings. Inspect the live Console separately and capture the actual Game View
through Pipeline as in the normal live check.
