using System;
using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using Phase;
using UnityEngine;

public class PixelTests
{
    public class Reference { public int columns; public Block[] blocks; public Frame[] frames; }
    public class Frame { public string name; public double time; public int phase; public int[] blocked; public Player player; }

    [Test]
    public void RoomSlideKeepsPixelsAndJoinsBothDirections()
    {
        foreach (int width in new[] { 83,320,427 }) CheckSlide(width);
    }
    static void CheckSlide(int width)
    {
        var from = new Color32[width * 180]; var to = new Color32[from.Length];
        for (int i = 0; i < from.Length; i++) { from[i] = new Color32(10, (byte)(i % width), 0, 255); to[i] = new Color32(20, (byte)(i % width), 0, 255); }
        var pixels = new PhasePixels();
        pixels.SetView(width,0);
        try
        {
            foreach (int direction in new[] { -1, 1 })
            {
                pixels.Slide(from, to, 0, direction); CollectionAssert.AreEqual(from, pixels.Texture.GetPixels32());
                pixels.Slide(from, to, 1, direction); CollectionAssert.AreEqual(to, pixels.Texture.GetPixels32());
                pixels.Slide(from, to, .5, direction);
                var frame = pixels.Texture.GetPixels32();
                int shift=(int)Math.Floor(width*.5+.5);
                for (int y = 0; y < 180; y++) for (int x = 0; x < width; x++)
                {
                    int source = x + shift * direction;
                    var expected = source >= 0 && source < width ? from[y * width + source] : to[y * width + source - width * direction];
                    Assert.That(frame[y * width + x], Is.EqualTo(expected));
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void AdaptiveViewPreservesArtAndFillsExtendedTerrain()
    {
        var s=new PhaseController(new[] {new Block(0,160,320,24),new Block(240,80,8,80)},120,149);s.flash=0;
        var pixels=new PhasePixels();
        try
        {
            pixels.Draw(s); var original=pixels.Texture.GetPixels32();
            pixels.SetView(83,95);pixels.Draw(s);var narrow=pixels.Texture.GetPixels32();
            for(int y=0;y<180;y++)for(int x=0;x<83;x++)Assert.That(narrow[y*83+x],Is.EqualTo(original[y*320+x+95]),"camera crop keeps native pixels");
            s.ResizeRoom(427);pixels.SetView(427,0);pixels.Draw(s);
            Color32 floor=pixels.Texture.GetPixel(400,19);
            Assert.That((floor.r,floor.g,floor.b),Is.EqualTo(((byte)169,(byte)184,(byte)197)),"connected stone floor reaches the wide edge");
            Assert.That(pixels.Texture.GetPixel(400,140).a,Is.EqualTo(1));
            pixels.SetView(320,0);s.ResizeRoom(320);pixels.Draw(s);CollectionAssert.AreEqual(original,pixels.Texture.GetPixels32(),"resizing back restores the original room");
            Assert.That(PhaseSandbox.ViewWidth(390,844),Is.EqualTo(83)); Assert.That(PhaseSandbox.ViewWidth(2560,1080),Is.EqualTo(427));
            Assert.That(PhaseSandbox.ViewOrigin(83,320,160),Is.EqualTo(122)); Assert.That(PhaseSandbox.ViewOrigin(83,320,315),Is.EqualTo(237));
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void PublishedDemoArtMatchesAllPosesFacingsDashStatesAndPhases()
    {
        var reference=JsonConvert.DeserializeObject<Reference>(File.ReadAllText("Verification/ArtReference.json"));
        var golden=new Texture2D(2,2);
        var pixels=new PhasePixels();
        try
        {
            Assert.That(golden.LoadImage(File.ReadAllBytes("Verification/ArtReference.png")),Is.True);
            var expected=golden.GetPixels32();var atlas=new Color32[expected.Length];
            var controller=new PhaseController(reference.blocks,120,149,new MovingPlatform(new[] {new[] {160.0,144},new[] {192.0,144}},32,8,true));
            int worst=0,bad=0;string first=null;
            for(int f=0;f<reference.frames.Length;f++)
            {
                var frame=reference.frames[f];controller.player=frame.player;controller.time=frame.time;controller.phase=frame.phase;controller.flash=0;
                controller.blocked.Clear();foreach(int index in frame.blocked)controller.blocked.Add(index);
                pixels.Draw(controller);var actual=pixels.Texture.GetPixels32();
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)
                {
                    int index=(golden.height-1-(f/reference.columns*180+y))*golden.width+(f%reference.columns*320+x);
                    var a=actual[(179-y)*320+x];var e=expected[index];atlas[index]=a;
                    int delta=Math.Max(Math.Abs(a.r-e.r),Math.Max(Math.Abs(a.g-e.g),Math.Abs(a.b-e.b)));
                    worst=Math.Max(worst,delta);
                    if(delta>2){bad++;if(first==null)first=$"{frame.name} ({x},{y}) expected {e} actual {a}";}
                }
            }
            Directory.CreateDirectory("TestResults/Web");
            var rendered=new Texture2D(golden.width,golden.height,TextureFormat.RGBA32,false);
            try { rendered.SetPixels32(atlas);rendered.Apply();File.WriteAllBytes("TestResults/Web/art-unity.png",rendered.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(rendered); }
            File.WriteAllText("TestResults/Web/art-parity.json",JsonConvert.SerializeObject(new { frames=reference.frames.Length,badPixels=bad,maxChannelDelta=worst,firstMismatch=first },Formatting.Indented));
            Assert.That(bad,Is.Zero,first);
            controller.blocks.Add(new Block(0,0,8,8));pixels.Draw(controller);
            Color32 added=pixels.Texture.GetPixel(1,179);
            Assert.That((added.r,added.g,added.b),Is.EqualTo(((byte)169,(byte)184,(byte)197)),"adding terrain invalidates the cached room");
        }
        finally { UnityEngine.Object.DestroyImmediate(golden); UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }
}
