using System;
using UnityEditor;
using UnityEngine;

public static class BackgroundEditor
{
    public static string Run()
    {
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
