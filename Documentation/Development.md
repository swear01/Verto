# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.4.7f1** GUI Editor. Scene construction, tests, input checks,
captures and Console inspection all run through the live Pipeline connection.
Do not use direct Editor `-batchmode` / `-executeMethod` invocations or cold
`unity run` / `unity test` jobs for this workflow. Fix a failed Pipeline connection
before continuing. Reuse the connected Editor instead of reopening it.
Keep Unity's completed URP material-upgrade record in version control; resetting
it can reopen a startup confirmation dialog and block Pipeline commands.
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
starting the next test run. There are 22 controller cases, one 48-frame art
comparison (only hood and spent-scarf colors intentionally remapped), one bidirectional pixel-slide
test, one adaptive-view test, one centered-view stone/void extension test, one
dash-charge hood lifecycle test, and three
Play Mode cases, including custom bindings, pause/release gating and room persistence.
The three immutable reference files cover 40 movement/action/climb traces.
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
Web players can replace these bindings through the settings button.

## CI

GitHub Actions runs the production `PhaseController.cs` and the same 22
`ControllerTests` on .NET 10, including the 40 immutable reference traces.
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
Sky extends above; terrain touching the lower room edge extends below. Gaps
and voids remain open. This is visual padding; collision stays unchanged.
Both axes fill the viewport without stretching native pixels.
Full-width terrain and room edges still expand above320pixels. Resizing preserves
controller state and rebuilds the pixel surface/cache; resizing during a slide
completes its handoff, including a height-only resize.
