# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.4.7f1** GUI Editor. Scene construction, tests, input checks,
captures and Console inspection all run through the live Pipeline connection.
Do not use direct Editor `-batchmode` / `-executeMethod` invocations or cold
`unity run` / `unity test` jobs for this workflow. Fix a failed Pipeline connection
before continuing. In agent terminals, `--wait` keeps the Editor
attached to the terminal session; run subsequent commands in another terminal.
Keep Unity's completed URP material-upgrade record in version control; resetting
it can reopen a startup confirmation dialog and block Pipeline commands.

```sh
unity open . --editor-version 6000.4.7f1 --wait
```

Run from the project directory, or pass `--project-path` to target it explicitly.
Discover the connected package's command parameters with `unity command --query`.

```sh
unity status --until-ready --json
unity command set_autotick
unity command open_scene -- --path Assets/Scenes/PhaseSandbox.unity
unity command run_tests -- --mode editor --filter ControllerTests --async_tests true
unity command test_status
unity command run_tests -- --mode playmode --filter SandboxTests --async_tests true
unity command test_status
```

To recreate the sandbox, call its existing builder through the connected Editor:
`unity command eval 'PhaseSandboxBuilder.Create();'`. Save any intentional scene
edits first; the builder preserves Unity's unsaved-scene confirmation.

Wait for each `test_status` result to be `completed` and inspect its summary before
starting the next test run. There are 17 Editor cases and one Play Mode case.
The three immutable reference files cover 40 movement/action/climb traces.
Production steps at 1/60 s; reference replays use the recorded 0.0166667 s.

For a live input check, enter Play mode and run the verification script. It queues
keyboard events on the player loop and temporarily permits unfocused input, then
restores the prior settings and removes its test keyboard. It requires no game
code changes or domain reload.

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
The saved sandbox and test fixtures contain no exits, progression or player text.
Movement, dash aiming and climb directions accept arrow keys or WASD.

## CI

GitHub Actions runs the production `PhaseController.cs` and the same 17
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
