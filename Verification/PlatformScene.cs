using System;
using System.IO;
using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the probe for PlatformLiveInput: the sandbox plus one jump platform that rises two cells and returns. Only Assets/Temp is written.
public static class PlatformScene
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before creating the probe.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/PhaseSandbox.unity");
        var root = UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>().transform;
        var go = PhaseMechanismPrefabs.Instantiate("JumpPlatform", root, new Vector3(112, -152));
        var platform = go.GetComponent<PhaseJumpPlatform>();
        platform.pivots = new[] { Vector2Int.zero, new Vector2Int(0, -2) };
        PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
        const string path = "Assets/Temp/PhaseJumpPlatformProbe.unity";
        Directory.CreateDirectory("Assets/Temp");
        if (!EditorSceneManager.SaveScene(scene, path, true)) throw new IOException("Could not save the probe scene.");
        // The shared sandbox scene stays untouched: drop the edited copy from the Editor.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        return path;
    }
}
