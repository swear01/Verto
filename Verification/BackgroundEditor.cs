using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class BackgroundEditor
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before background verification setup.");
        // Unity skips creating this index in Play, then EnumerateAll reads an empty list.
        var search = Type.GetType("UnityEditor.Search.SearchDatabase,UnityEditor.QuickSearchModule", true);
        if (search.GetMethod("GetDefaultSearchDatabase", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.Invoke(null, null) == null)
            throw new InvalidOperationException("Could not initialize Unity's default Search index.");
        // Unity's public SetPlayModeFocused(false) maximizes; select PlayUnfocused explicitly.
        var type = typeof(EditorWindow).Assembly.GetType("UnityEditor.PlayModeView", true);
        var behavior = type.GetProperty("enterPlayModeBehavior");
        var unfocused = Enum.Parse(behavior.PropertyType, "PlayUnfocused");
        var views = Resources.FindObjectsOfTypeAll(type);
        if (views.Length == 0) throw new InvalidOperationException("Open a Game View before background verification.");
        foreach (var view in views)
        {
            behavior.SetValue(view, unfocused);
            if (!unfocused.Equals(behavior.GetValue(view))) throw new InvalidOperationException("Play Unfocused was not applied.");
        }
        return $"Play Unfocused: {views.Length} view(s)";
    }
}
