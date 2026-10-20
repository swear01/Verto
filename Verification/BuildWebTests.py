import json
import subprocess
import unittest
from unittest.mock import patch
import BuildWeb


class PipelineFailures(unittest.TestCase):
    def test_recompile_accepts_current_scripts_but_rejects_compiler_errors(self):
        for status in ("completed", "up_to_date"):
            with patch.object(BuildWeb, "command"), patch.object(BuildWeb, "wait", return_value={"status": status, "failed": False}):
                self.assertEqual(BuildWeb.recompile()["status"], status)
        for failure in ({"failed": True}, {"compilationFailed": True}, {"errors": ["CS1002"]}):
            with patch.object(BuildWeb, "command"), patch.object(BuildWeb, "wait", return_value={"status": "completed", **failure}):
                with self.assertRaisesRegex(RuntimeError, "Script compilation failed"):
                    BuildWeb.recompile()

    def test_release_contains_demo_and_crystal_scenes_in_order(self):
        self.assertEqual(BuildWeb.SCENES, ["Assets/Scenes/PhaseTitle.unity", "Assets/Scenes/PhaseDemo1.unity", "Assets/Scenes/PhaseDemo1.1.unity"] + [f"Assets/Scenes/PhaseDemo{i}.unity" for i in range(2, 9)] + [f"Assets/Scenes/PhaseCrystal{i}.unity" for i in range(1, 4)] + ["Assets/Scenes/PhaseDemo12.unity", "Assets/Scenes/PhaseDemo13.unity", "Assets/Scenes/PhaseDemo14.unity", "Assets/Scenes/PhaseEditor.unity"])
        for scene in BuildWeb.SCENES:
            self.assertTrue((BuildWeb.PROJECT / scene).is_file(), scene)

    def test_text_command_result_is_valid(self):
        response = {"success": True, "data": {"result": "Auto-tick enabled"}}
        with patch.object(BuildWeb.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(response))):
            self.assertEqual(BuildWeb.command("set_autotick"), "Auto-tick enabled")

    def test_outer_success_does_not_hide_command_failure(self):
        response = {"success": True, "data": {"result": {"success": False, "error": "compile failed"}}}
        with patch.object(BuildWeb.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(response))):
            with self.assertRaisesRegex(RuntimeError, "compile failed"):
                BuildWeb.command("eval")

    def test_transport_failure_is_not_success(self):
        response = {"success": False, "errors": [{"message": "disconnected"}]}
        with patch.object(BuildWeb.subprocess, "run", return_value=subprocess.CompletedProcess([], 0, json.dumps(response))):
            with self.assertRaisesRegex(RuntimeError, "disconnected"):
                BuildWeb.command("build")

    def test_previous_completed_build_is_not_current_build(self):
        stale = {"status": "completed", "buildId": "old"}
        current = {"status": "completed", "buildId": "new"}
        with patch.object(BuildWeb, "command", side_effect=[stale, current]), patch.object(BuildWeb.time, "sleep"):
            result = BuildWeb.wait("build_status", lambda r: r.get("buildId") == "new", 10)
        self.assertEqual(result, current)

    def test_cancelled_run_fails_immediately(self):
        with patch.object(BuildWeb, "command", return_value={"status": "cancelled"}):
            with self.assertRaisesRegex(RuntimeError, "cancelled"):
                BuildWeb.wait("test_status", lambda r: False, 10)


if __name__ == "__main__":
    unittest.main()
