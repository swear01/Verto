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
        foreach (int width in new[] { 83,320,427 }) CheckSlide(width,180);
        CheckSlide(320,693);
    }
    static void CheckSlide(int width, int height)
    {
        var from = new Color32[width * height]; var to = new Color32[from.Length];
        for (int i = 0; i < from.Length; i++) { from[i] = new Color32(10, (byte)(i % width), 0, 255); to[i] = new Color32(20, (byte)(i % width), 0, 255); }
        var pixels = new PhasePixels();
        pixels.SetView(width,height,0);
        try
        {
            foreach (int direction in new[] { -1, 1 })
            {
                pixels.Slide(from, to, 0, direction); CollectionAssert.AreEqual(from, pixels.Texture.GetPixels32());
                pixels.Slide(from, to, 1, direction); CollectionAssert.AreEqual(to, pixels.Texture.GetPixels32());
                pixels.Slide(from, to, .5, direction);
                var frame = pixels.Texture.GetPixels32();
                int shift=(int)Math.Floor(width*.5+.5);
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
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
            pixels.SetView(83,180,95);pixels.Draw(s);var narrow=pixels.Texture.GetPixels32();
            for(int y=0;y<180;y++)for(int x=0;x<83;x++)Assert.That(narrow[y*83+x],Is.EqualTo(original[y*320+x+95]),"camera crop keeps native pixels");
            s.ResizeRoom(427);pixels.SetView(427,180,0);pixels.Draw(s);
            Color32 floor=pixels.Texture.GetPixel(400,19);
            Assert.That((floor.r,floor.g,floor.b),Is.EqualTo(((byte)169,(byte)184,(byte)197)),"connected stone floor reaches the wide edge");
            Assert.That(pixels.Texture.GetPixel(400,140).a,Is.EqualTo(1));
            pixels.SetView(320,180,0);s.ResizeRoom(320);pixels.Draw(s);CollectionAssert.AreEqual(original,pixels.Texture.GetPixels32(),"resizing back restores the original room");
            pixels.SetView(320,693,0);pixels.Draw(s);var portrait=pixels.Texture.GetPixels32();
            for(int y=20;y<180;y++)for(int x=0;x<320;x++)Assert.That(portrait[(y+257)*320+x],Is.EqualTo(original[y*320+x]),"portrait retains playable pixels above the extended floor");
            foreach(var pixel in portrait)Assert.That(pixel.a,Is.EqualTo(255),"extended sky is opaque");
            Assert.That(PhaseSandbox.ViewSize(390,844),Is.EqualTo(new Vector2Int(320,693)));
            Assert.That(PhaseSandbox.ViewSize(2560,1080),Is.EqualTo(new Vector2Int(427,180)));
            foreach(var viewport in new[]{new Vector2Int(320,180),new Vector2Int(800,600),new Vector2Int(390,844),new Vector2Int(320,1200)})
            {
                var size=PhaseSandbox.ViewSize(viewport.x,viewport.y);
                Assert.That(size.x,Is.GreaterThanOrEqualTo(320));Assert.That(size.y,Is.GreaterThanOrEqualTo(180));
            }
            pixels.SetView(320,694,0);pixels.Draw(s);var taller=pixels.Texture.GetPixels32();
            for(int i=0;i<portrait.Length;i++)Assert.That(taller[i],Is.EqualTo(portrait[i]),"height resize keeps sky fixed in world coordinates");
            for(int height=180;height<=1400;height++)Assert.That(PhaseSandbox.ViewSize(320,height),Is.EqualTo(new Vector2Int(320,height)),"native viewport size must not drift on repeat calculation");
            pixels.SetView(320,180,0);pixels.Draw(s);CollectionAssert.AreEqual(original,pixels.Texture.GetPixels32(),"portrait resize restores the original art");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void TallViewCentersPlayAreaAndExtendsOnlyBottomTerrain()
    {
        var pixels=new PhasePixels();
        var blocks=new[]{new Block(0,160,112,24),new Block(208,160,112,24),new Block(128,96,64,8)};
        var room=new PhaseController(blocks,32,149);room.flash=0;
        try
        {
            pixels.SetView(320,693,0);pixels.Draw(room);
            Assert.That(pixels.OriginY,Is.EqualTo(-256));
            Assert.That(Math.Abs(-pixels.OriginY+90-pixels.Height/2.0),Is.LessThanOrEqualTo(.5),"play area is centered");
            var stone=pixels.Texture.GetPixel(32,40);var gap=pixels.Texture.GetPixel(160,40);
            Assert.That(stone,Is.Not.EqualTo(gap),"bottom stone extends while the gap remains void");
            Assert.That(blocks[0].h,Is.EqualTo(24),"visual extension must not enlarge collision terrain");
            var voidRoom=new PhaseController(new[]{new Block(128,96,64,8)},32,64);voidRoom.flash=0;
            pixels.Draw(voidRoom);
            Assert.That(pixels.Texture.GetPixel(32,40),Is.EqualTo(gap),"a bottom void must not acquire a floor");
            Assert.That(pixels.Texture.GetPixel(160,40),Is.EqualTo(gap),"floating terrain must not extend to the bottom");
            pixels.SetView(320,180,0);pixels.Draw(room);Assert.That(pixels.OriginY,Is.Zero);
            pixels.SetView(320,693,0);pixels.Draw(room);Assert.That(pixels.Texture.GetPixel(32,40),Is.EqualTo(stone),"resize rebuilds extended terrain");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void HoodFollowsDashUseFailedRetryAndLandingRefill()
    {
        var s=new PhaseController(new[]{new Block(0,160,320,24)},120,90);s.player.grounded=false;s.flash=0;
        var pixels=new PhasePixels();
        void Check(string color)
        {
            s.flash=0;pixels.Draw(s);int count=0;Color32 expected=PhasePixels.Hex(color);
            foreach(var pixel in pixels.Texture.GetPixels32())if(pixel.Equals(expected))count++;
            Assert.That(count,Is.GreaterThan(24),"charge must occupy the hood, not only the small scarf");
        }
        try
        {
            Check("ffb24d");s.Step(new Controls{dash=true});Assert.That(s.player.dash,Is.Zero);Check("b96532");
            int used=s.dashes;for(int i=0;i<15;i++)s.Step(new Controls());
            s.Step(new Controls{dash=true});Assert.That(s.dashes,Is.EqualTo(used));Check("b96532");
            for(int i=0;i<120&&!s.player.grounded;i++)s.Step(new Controls());
            Assert.That(s.player.grounded,Is.True);Assert.That(s.player.dash,Is.EqualTo(1));Check("ffb24d");
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
            int worst=0,bad=0,hoodPixels=0,scarfPixels=0;string first=null;
            for(int f=0;f<reference.frames.Length;f++)
            {
                var frame=reference.frames[f];controller.player=frame.player;controller.time=frame.time;controller.phase=frame.phase;controller.flash=0;
                controller.blocked.Clear();foreach(int index in frame.blocked)controller.blocked.Add(index);
                pixels.Draw(controller);var actual=pixels.Texture.GetPixels32();
                var p=frame.player;int feet=(int)Math.Floor(p.y+p.h+.5)+(p.climbing&&p.stamina<=20&&(int)Math.Floor(frame.time*8)%2!=0?1:0);
                int headTop=feet-(p.ducking?8:13),collar=p.ducking?5:frame.name.StartsWith("run1 ")||frame.name.StartsWith("run3 ")?6:7;
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)
                {
                    int index=(golden.height-1-(f/reference.columns*180+y))*golden.width+(f%reference.columns*320+x);
                    var a=actual[(179-y)*320+x];var e=expected[index];atlas[index]=a;
                    if(x>=p.x-1&&x<p.x+9&&y>=headTop&&y<headTop+collar)
                    {
                        if(e.r==234&&e.g==223&&e.b==197){e=(Color32)PhasePixels.Hex(p.dash>0?"ffb24d":"b96532");hoodPixels++;}
                        else if(e.r==179&&e.g==163&&e.b==134){e=(Color32)PhasePixels.Hex(p.dash>0?"d5732b":"763d24");hoodPixels++;}
                    }
                    if(p.dash==0&&x>=p.x-8&&x<p.x+17&&y>=headTop&&y<=feet)
                    {
                        if(e.r==90&&e.g==101&&e.b==120){e=(Color32)PhasePixels.Hex("b96532");scarfPixels++;}
                        else if(e.r==65&&e.g==74&&e.b==90){e=(Color32)PhasePixels.Hex("763d24");scarfPixels++;}
                    }
                    int delta=Math.Max(Math.Abs(a.r-e.r),Math.Max(Math.Abs(a.g-e.g),Math.Abs(a.b-e.b)));
                    worst=Math.Max(worst,delta);
                    if(delta>2){bad++;if(first==null)first=$"{frame.name} ({x},{y}) expected {e} actual {a}";}
                }
            }
            Directory.CreateDirectory("TestResults/Web");
            var rendered=new Texture2D(golden.width,golden.height,TextureFormat.RGBA32,false);
            try { rendered.SetPixels32(atlas);rendered.Apply();File.WriteAllBytes("TestResults/Web/art-unity.png",rendered.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(rendered); }
            File.WriteAllText("TestResults/Web/art-parity.json",JsonConvert.SerializeObject(new { frames=reference.frames.Length,hoodPixelsRemapped=hoodPixels,scarfPixelsRemapped=scarfPixels,badPixels=bad,maxChannelDelta=worst,firstMismatch=first },Formatting.Indented));
            Assert.That(hoodPixels,Is.GreaterThan(1000),"all poses retain a large dash-charge hood");
            Assert.That(scarfPixels,Is.GreaterThan(100),"spent scarf uses the new dark orange palette");
            Assert.That(bad,Is.Zero,first);
            controller.blocks.Add(new Block(0,0,8,8));pixels.Draw(controller);
            Color32 added=pixels.Texture.GetPixel(1,179);
            Assert.That((added.r,added.g,added.b),Is.EqualTo(((byte)169,(byte)184,(byte)197)),"adding terrain invalidates the cached room");
        }
        finally { UnityEngine.Object.DestroyImmediate(golden); UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }
}
