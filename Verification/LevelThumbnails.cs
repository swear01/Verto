using System.IO;
using System.Linq;
using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Bakes each level's level-select thumbnail: one pixel per 8px cell of the room rendered by PhasePixels.
// A cell shows its center pixel, or its most saturated one when that is clearly more colorful, so phase blocks,
// lasers and crystals keep their color. Rebake whenever a level scene changes.
public static class LevelThumbnails
{
    const string Folder = "Assets/Phase/Resources/Levels/";
    const int Columns = 40, Rows = 22;
    static int Chroma(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) - Mathf.Min(c.r, Mathf.Min(c.g, c.b));
    public static string Run()
    {
        Directory.CreateDirectory(Folder);
        foreach (var scene in PhaseSandbox.Levels)
        {
            var path = $"Assets/Scenes/{scene}.unity";
            var loaded = SceneManager.GetSceneByPath(path);
            var opened = loaded.isLoaded ? loaded : EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            PhaseController room;
            try { room = opened.GetRootGameObjects().SelectMany(o => o.GetComponentsInChildren<PhaseSandbox>()).Single().CreateController(); }
            finally { if (!loaded.isLoaded) EditorSceneManager.CloseScene(opened, true); }
            var pixels = new PhasePixels();
            var thumbnail = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false);
            try
            {
                pixels.SetView(320, 180, 0); pixels.Draw(room, false);
                var source = pixels.Texture.GetPixels32();
                var cells = new Color32[Columns * Rows];
                for (int cy = 0; cy < Rows; cy++) for (int cx = 0; cx < Columns; cx++)
                {
                    Color32 At(int x, int y) => source[(179 - cy * 8 - y) * 320 + cx * 8 + x];
                    var center = At(4, 4); var vivid = center;
                    for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) if (Chroma(At(x, y)) > Chroma(vivid)) vivid = At(x, y);
                    cells[(Rows - 1 - cy) * Columns + cx] = Chroma(vivid) > Chroma(center) + 40 ? vivid : center;
                }
                thumbnail.SetPixels32(cells); thumbnail.Apply();
                File.WriteAllBytes(Folder + scene + ".png", thumbnail.EncodeToPNG());
            }
            finally { Object.DestroyImmediate(thumbnail); Object.DestroyImmediate(pixels.Texture); }
            AssetDatabase.ImportAsset(Folder + scene + ".png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + scene + ".png");
            importer.textureType = TextureImporterType.Default; importer.isReadable = true; importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point; importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None; importer.alphaIsTransparency = false;
            importer.SaveAndReimport();
        }
        return $"{PhaseSandbox.Levels.Length} thumbnails in {Folder}";
    }
}
