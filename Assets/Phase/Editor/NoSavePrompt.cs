using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

// The Editor is only watched, never clicked: it must not raise "Scene(s) Have Been Modified" or
// "modified externally" dialogs. Scenes that must persist are saved explicitly with SaveScene.
[InitializeOnLoad]
static class NoSavePrompt
{
    // ponytail: internal Unity API, no public way to clear a scene's dirty flag; the hook is skipped if it is renamed
    static readonly System.Reflection.MethodInfo Clear = typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness",
        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);

    static NoSavePrompt()
    {
        EditorApplication.update += ClearDirty;
    }

    static void ClearDirty()
    {
        if (Clear == null || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.isDirty) Clear.Invoke(null, new object[] { scene });
        }
    }

    class Reloader : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.sceneCount != 1) return;
            var scene = SceneManager.GetSceneAt(0);
            if (string.IsNullOrEmpty(scene.path) || System.Array.IndexOf(imported, scene.path) < 0) return;
            var path = scene.path;
            EditorApplication.delayCall += () => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
