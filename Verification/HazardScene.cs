using System;
using System.IO;
using Phase;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class HazardScene
{
    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before creating the probe.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/PhaseSandbox.unity");
        var root=UnityEngine.Object.FindAnyObjectByType<PhaseSandbox>().transform;
        foreach(var item in new[]{(x:48,y:80,mask:3),(x:80,y:56,mask:5),(x:112,y:32,mask:6)})
        {
            var go=new GameObject("Mixed stone");go.transform.SetParent(root);go.transform.position=new Vector3(item.x,-item.y,0);
            var terrain=go.AddComponent<PhaseTerrain>();terrain.size=new Vector2Int(24,8);terrain.phaseMask=item.mask;terrain.CreateBlock();
        }
        foreach(var item in new[]{(x:120,y:96,dir:SpikeDirection.Up,mask:2),(x:160,y:40,dir:SpikeDirection.Down,mask:5),(x:184,y:64,dir:SpikeDirection.Left,mask:3),(x:208,y:64,dir:SpikeDirection.Right,mask:6)})
        {
            var go=new GameObject("Phase spikes");go.transform.SetParent(root);go.transform.position=new Vector3(item.x,-item.y,0);
            var spikes=go.AddComponent<PhaseSpike>();spikes.length=2;spikes.direction=item.dir;spikes.phaseMask=item.mask;
        }
        const string path="Assets/Temp/PhaseHazardProbe.unity";
        Directory.CreateDirectory("Assets/Temp");
        if (!EditorSceneManager.SaveScene(scene,path)) throw new IOException("Could not save the probe scene.");
        return path;
    }
}
