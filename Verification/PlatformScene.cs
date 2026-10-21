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
        go.GetComponent<PhaseJumpPlatform>().pivots = new[] { Vector2Int.zero, new Vector2Int(0, -2) };
        const string path = "Assets/Temp/PhaseJumpPlatformProbe.unity";
        Directory.CreateDirectory("Assets/Temp");
        if (!EditorSceneManager.SaveScene(scene, path)) throw new IOException("Could not save the probe scene.");
        return path;
    }
}
