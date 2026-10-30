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

    static string watched;
    static System.DateTime written;

    static NoSavePrompt()
    {
        EditorApplication.update += Tick;
        EditorSceneManager.sceneSaved += scene => written = System.IO.File.GetLastWriteTimeUtc(scene.path);
    }

    static void Tick()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (Clear != null && scene.IsValid() && scene.isLoaded && scene.isDirty) Clear.Invoke(null, new object[] { scene });
        }
        if (SceneManager.sceneCount != 1) return;
        var path = SceneManager.GetSceneAt(0).path;
        if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path)) return;
        var now = System.IO.File.GetLastWriteTimeUtc(path);
        if (path != watched) { watched = path; written = now; }
        else if (now != written)
        {
            written = now;
            EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        }
    }
}
