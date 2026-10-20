"""Test and build through Unity CLI/Pipeline in an already-open GUI Editor."""
import argparse
import json
from pathlib import Path
import subprocess
import time

PROJECT = Path(__file__).resolve().parent.parent
REPORTS = PROJECT / "TestResults/Web"
SCENES = ["Assets/Scenes/PhaseTitle.unity"] + [f"Assets/Scenes/PhaseDemo{i}.unity" for i in range(1, 9)] + [f"Assets/Scenes/PhaseCrystal{i}.unity" for i in range(1, 4)] + ["Assets/Scenes/PhaseDemo12.unity", "Assets/Scenes/PhaseEditor.unity"]


def command(name, *args):
    completed = subprocess.run(
        ["unity", "command", name, "--project-path", str(PROJECT), "--json", "--timeout", "15", *args],
        capture_output=True, text=True, timeout=25,
    )
    data = json.loads(completed.stdout)
    if completed.returncode or not data.get("success"):
        raise RuntimeError(f"{name}: {data.get('errors')}")
    result = data["data"]["result"]
    if isinstance(result, dict) and result.get("success") is False:
        raise RuntimeError(f"{name}: {result}")
    return result


def wait(name, ready, seconds):
    deadline = time.monotonic() + seconds
    last = None
    while time.monotonic() < deadline:
        try:
            last = command(name)
        except (RuntimeError, json.JSONDecodeError, subprocess.TimeoutExpired) as error:
            last = str(error)
        if isinstance(last, dict):
            if last.get("status") in ("error", "cancelled"):
                raise RuntimeError(f"{name}: {last}")
            if ready(last):
                return last
        time.sleep(3)
    raise TimeoutError(f"{name} did not finish: {last}")


def save(name, result):
    (REPORTS / f"{name}.json").write_text(json.dumps(result, indent=2))


def recompile():
    command("recompile", "--", "--focus", "false")
    result = wait("recompile_status", lambda r: r.get("status") in ("completed", "up_to_date"), 180)
    if result.get("failed") or result.get("compilationFailed") or result.get("errors"):
        raise RuntimeError(f"Script compilation failed: {result}")
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, default=PROJECT / "Builds/Web")
    args = parser.parse_args()
    REPORTS.mkdir(parents=True, exist_ok=True)
    status = wait("editor_status", lambda r: r.get("status") == "ready" and r.get("playMode") == "stopped", 600)
    if status.get("unityVersion") != "6000.4.7f1":
        raise RuntimeError(f"Expected Unity 6000.4.7f1, got {status.get('unityVersion')}")
    splash = command("eval", "return new { show = UnityEditor.PlayerSettings.SplashScreen.show, logo = UnityEditor.PlayerSettings.SplashScreen.showUnityLogo };")["result"]
    save("splash", splash)
    if splash["show"] or splash["logo"]:
        raise RuntimeError("Unity splash screen and logo must be disabled")
    command("set_autotick")
    save("recompile", recompile())
    command("run_script", "--", "--file", "Verification/BackgroundEditor.cs", "--entry", "BackgroundEditor.Run")
    for mode, test_filter, expected in [
        ("editor", "ControllerTests", 48), ("editor", "LaserTests", 9), ("editor", "DemoRoomTests", 17),
        ("editor", "CrystalTests", 15), ("editor", "CrystalRoomTests", 26),
        ("editor", "PixelTests", 18), ("editor", "GridGeometryTests", 6),
        ("editor", "CustomMapTests", 5), ("playmode", "SandboxTests", 11),
        ("playmode", "MapBridgeTests", 1), ("playmode", "MenuTests", 2),
    ]:
        wait("editor_status", lambda r: r.get("status") == "ready" and r.get("playMode") == "stopped", 120)
        command("run_tests", "--", "--mode", mode, "--filter", test_filter, "--async_tests")
        result = wait("test_status", lambda r: r.get("status") == "completed", 300)
        save({"ControllerTests": "editor", "DemoRoomTests": "demo", "PixelTests": "pixels", "SandboxTests": "playmode"}.get(test_filter, test_filter), result)
        summary = result["summary"]
        if summary.get("total") != expected or summary.get("passed") != expected:
            raise RuntimeError(f"{mode}: expected {expected} passing tests, got {summary}")
        print(f"{test_filter}: {expected} passed", flush=True)
    wait("editor_status", lambda r: r.get("status") == "ready" and r.get("playMode") == "stopped", 120)
    for scene in SCENES:
        if not (PROJECT / scene).is_file():
            raise RuntimeError(f"Missing demo scene: {scene}")
    save("scenes", SCENES)
    command("open_scene", "--", "--path", SCENES[0])
    queued = command("build", "--", "--target", "WebGL", "--outputPath", str(args.output.resolve()),
                     "--scenes", json.dumps(SCENES), "--confirm")
    if queued.get("status") != "queued":
        raise RuntimeError(f"Build was not queued: {queued}")
    result = wait("build_status", lambda r: r.get("status") == "completed" and r.get("buildId") == queued["buildId"], 1200)
    save("build", result)
    if result.get("result") != "Succeeded" or result.get("totalErrors") != 0:
        raise RuntimeError(f"Web build failed; see {REPORTS / 'build.json'}")
    if not (args.output / "index.html").is_file():
        raise RuntimeError("Web build produced no index.html")
    print(f"Web: Succeeded, {result['totalSizeBytes']} bytes, {result['totalWarnings']} warnings", flush=True)


if __name__ == "__main__":
    main()
