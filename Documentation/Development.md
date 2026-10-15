# Development

Use the official Unity CLI and the project's `com.unity.pipeline` package to drive
a resident Unity **6000.4.7f1** Editor. In agent terminals, `--wait` keeps the Editor
attached to the terminal session; run subsequent commands in another terminal.

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

Wait for each `test_status` result to be `completed` and inspect its summary before
starting the next test run. There are 17 Editor cases and one Play Mode case.
The three immutable reference files cover 40 movement/action/climb traces.
Production steps at 1/60 s; reference replays use the recorded 0.0166667 s.

For a live input check, enter Play mode and run the verification script. It queues
keyboard events on the player loop and temporarily permits unfocused input, then
restores the prior settings and removes its test keyboard. It requires no game
code changes or domain reload.

```sh
unity command eval 'UnityEngine.Application.runInBackground = true;'
unity command editor_play
unity status --until-ready --json
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
