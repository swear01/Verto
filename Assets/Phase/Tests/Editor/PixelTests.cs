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
    public void GearRacksTranslateRigidlyAndStayEngagedAcrossFullTravel()
    {
        var s = new PhaseController(Array.Empty<Block>(), 0, 149, gears: new[] {
            new ClimbGear(new Block(72,104,8,56),64,new Block(104,104,32,8),144)
        });
        var gear = s.gears[0]; s.flash = 0;
        var pixels = new PhasePixels();
        try
        {
            for (int lift = 0; lift <= 40; lift++)
            {
                gear.wall.y = 104 - lift; gear.platform.x = 104 + lift;
                pixels.Draw(s); var frame = pixels.Texture.GetPixels32();
                int bottom = Math.Min(160, 192 - lift);
                for (int y = (int)gear.wall.y; y < bottom; y++)
                    Assert.That(frame[(179-y)*320+81].r, Is.EqualTo(160), $"rigid rack at lift {lift}, y {y}");
                Assert.That(frame[(179-137)*320+81].r, Is.EqualTo(160), "rack remains beside the fixed input pinion");
                if (bottom < 160)
                    for (int x = 81; x <= 84; x++)
                        Assert.That(frame[(179-bottom)*320+x].r, Is.LessThan(40), $"rack end translates with wall at lift {lift}");
                int left = Math.Max(90, 58 + lift);
                for (int x = left; x < 136 + lift; x++)
                    Assert.That(frame[(179-113)*320+x].r, Is.EqualTo(160), $"rigid horizontal rack at lift {lift}, x {x}");
                Assert.That(frame[(179-113)*320+114].r, Is.EqualTo(160), "rack remains above the fixed output pinion");
                if (left > 90)
                    Assert.That(frame[(179-113)*320+left-1].r, Is.LessThan(40), "horizontal rack end translates beyond the fixed sleeve");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [TestCase(0)] [TestCase(19)] [TestCase(40)]
    public void GearRackStaysAttachedToThePlatformAcrossTravel(int lift)
    {
        var s = new PhaseController(Array.Empty<Block>(), 0, 149, gears: new[] {
            new ClimbGear(new Block(72,104,8,56),64,new Block(104,104,32,8),144)
        });
        var gear = s.gears[0]; gear.wall.y -= lift; gear.platform.x += lift; s.flash = 0;
        var pixels = new PhasePixels();
        try
        {
            pixels.Draw(s);
            for (int x = (int)gear.platform.x + 1; x < gear.platform.x + gear.platform.w - 1; x++)
                for (int y = (int)(gear.platform.y + gear.platform.h) - 1; y <= gear.platform.y + gear.platform.h + 1; y++)
                {
                    Color32 pixel = pixels.Texture.GetPixel(x, 179 - y);
                    Assert.That(pixel.r, Is.GreaterThan(40), $"platform and rack have no background gap at ({x},{y})");
                }
            var first = pixels.Texture.GetPixels32(); s.time += 1; pixels.Draw(s);
            var second = pixels.Texture.GetPixels32();
            for (int y = 60; y < 166; y++) for (int x = 70; x < 180; x++)
                Assert.That(second[(179-y)*320+x], Is.EqualTo(first[(179-y)*320+x]), "stationary gears do not animate on their own");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void MixedInlaysAndDirectionalSpikesMatchTheirMasks()
    {
        var pixels=new PhasePixels();
        Color32 At(int x,int y)=>pixels.Texture.GetPixel(x,179-y);
        try
        {
            foreach(int mask in new[]{3,5,6})
            {
                var b=new Block(80,80,16,8,phaseMask:mask);var room=new PhaseController(new[]{b},32,40);room.flash=0;
                for(int phase=0;phase<3;phase++)
                {
                    room.phase=phase;pixels.Draw(room,false);int dot=0;
                    for(int p=0;p<3;p++)if(b.ActiveInPhase(p))
                    {
                        var actual=At(82+dot++*3,84);
                        if(b.ActiveInPhase(phase))Assert.That(actual,Is.EqualTo((Color32)PhasePixels.Hex(PhasePixels.Highlights[p])),"inlays show both solid phases");
                        else Assert.That(actual,Is.Not.EqualTo((Color32)PhasePixels.Hex(PhasePixels.Highlights[p])),"inactive inlays are dim");
                    }
                }
            }
            foreach(SpikeDirection direction in Enum.GetValues(typeof(SpikeDirection)))
            {
                var spike=new Spike(80,80,16,direction,3);var room=new PhaseController(Array.Empty<Block>(),32,40,spikes:new[]{spike});room.flash=0;
                var empty=new PhaseController(Array.Empty<Block>(),32,40);empty.flash=0;pixels.Draw(empty,false);var background=pixels.Texture.GetPixels32();
                room.phase=0;pixels.Draw(room,false);var active=pixels.Texture.GetPixels32();var hit=spike.Hitbox;
                for(int y=(int)hit.y;y<hit.y+hit.h;y++)for(int x=(int)hit.x;x<hit.x+hit.w;x++)Assert.That(At(x,y),Is.Not.EqualTo(background[(179-y)*320+x]),"every lethal root pixel is drawn");
                room.phase=2;pixels.Draw(room,false);var inactive=pixels.Texture.GetPixels32();Assert.That(inactive,Is.Not.EqualTo(active),"inactive spikes retain dark teeth");
                room.phase=0;pixels.Draw(room,false);CollectionAssert.AreEqual(active,pixels.Texture.GetPixels32(),"phase changes recover the same cached spike art");
            }
            Assert.That(PhasePixels.SurfaceColor(7),Is.EqualTo(PhasePixels.Hex("e8e8e8")),"permanent white remains neutral");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

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
            Assert.That((floor.r,floor.g,floor.b),Is.EqualTo(((byte)232,(byte)232,(byte)232)),"connected stone floor reaches the wide edge");
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
            s.mover=new MovingPlatform(new[]{new[]{160.0,144},new[]{192.0,144}},32,8,true);
            foreach(int phase in new[]{0,1,2})foreach(double x in new[]{160.0,176.0,192.0})
            {
                s.phase=phase;s.mover.x=x;pixels.Draw(s);
                Assert.That((Color32)pixels.Texture.GetPixel(24,19),Is.EqualTo((Color32)PhasePixels.Hex("e8e8e8")),"permanent floor stays white in every phase");
                Assert.That((Color32)pixels.Texture.GetPixel((int)x+4,35),Is.EqualTo((Color32)PhasePixels.Hex("f0f0f0")),"moving platform stays white along its path in every phase");
                Assert.That((Color32)pixels.Texture.GetPixel((int)x+2,33),Is.EqualTo((Color32)PhasePixels.Hex("787878")),"platform retains gray texture");
            }
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
    public void FatigueUsesCharacterColorAndShakeWithoutGauge()
    {
        var room=new PhaseController(new[]{new Block(0,160,320,24)},120,100);room.flash=0;
        var p=room.player;p.climbing=true;p.grounded=false;p.facing=1;
        var pixels=new PhasePixels();
        Color32 At(int x,int y) => pixels.Texture.GetPixel(x-pixels.OriginX,pixels.Height-1-y+pixels.OriginY);
        void Draw(double stamina,double time=0) { p.stamina=stamina;room.time=time;pixels.Draw(room); }
        try
        {
            Draw(110);var original=pixels.Texture.GetPixels32();
            Draw(55);CollectionAssert.AreEqual(original,pixels.Texture.GetPixels32(),"healthy grip has no gauge or tint");
            Draw(37.5);Assert.That(At(121,107),Is.EqualTo(new Color32(242,174,158,255)),"body gradually reddens");
            Draw(20);Assert.That(At(121,107),Is.EqualTo(new Color32(252,108,107,255)),"critical body is red");
            Draw(20,.25);Assert.That(At(121,107),Is.EqualTo((Color32)PhasePixels.Hex("ff5b5e")),"red intensity pulses gently");
            Draw(20,.14);Assert.That(At(121,108).g,Is.LessThan(150),"critical sprite shakes by one pixel without moving the collider");
            Assert.That(p.y,Is.EqualTo(100));
            foreach(int phase in new[]{0,1,2})foreach(int dash in new[]{0,1})
            {
                room.phase=phase;p.dash=dash;Draw(110);var full=pixels.Texture.GetPixels32();Draw(20);
                for(int y=98;y<105;y++)for(int x=118;x<132;x++)Assert.That(At(x,y),Is.EqualTo(full[(179-y)*320+x]),"eyes and hood keep their meanings");
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)if(x<118||x>132||y<97||y>113)
                    Assert.That(At(x,y),Is.EqualTo(full[(179-y)*320+x]),"no detached gauge or terrain recoloring");
            }
            room.phase=0;p.dash=1;p.climbing=false;Draw(0);Assert.That(At(121,107).g,Is.LessThan(150),"exhausted falling body remains red");
            p.grounded=true;Draw(0);Assert.That(At(121,107).g,Is.GreaterThan(150),"grounding clears the warning");
            p.grounded=false;p.climbing=true;p.wallBoost=.2;Draw(15);Assert.That(At(121,107).g,Is.GreaterThan(150),"pending wallboost refund avoids a false critical warning");p.wallBoost=0;
            Draw(110);CollectionAssert.AreEqual(original,pixels.Texture.GetPixels32(),"refill restores cached original art");
            pixels.SetView(320,693,0);Draw(20);Assert.That(At(121,107).g,Is.LessThan(150),"portrait retains the character warning");
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
            var neutralColors=new[]{"a9b8c5","e8e8e8","697d8e","a0a0a0","3c4a57","646464","506170","7f7f7f","34404b","565656","242c34","3c3c3c","191f25","2a2a2a","f2dc8a","f0f0f0","e7ce76","e8e8e8","6f603c","787878","8f7c47","a0a0a0","4b4129","484848"};
            var tracks=new[]{"484a3f","424b57","454537","3f464f","444334","3e444c","786e46","6c7076","7b734d","6f757d"};
            int worst=0,bad=0,hoodPixels=0,scarfPixels=0,fatiguePixels=0,neutralPixels=0;string first=null;
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
                    bool stone=y>=160||(x>=224&&x<240&&y>=80&&y<160);
                    bool mover=x>=160&&x<192&&y>=144&&y<152;
                    if(stone||mover)
                    {
                        for(int c=0;c<neutralColors.Length;c+=2)if(e.Equals((Color32)PhasePixels.Hex(neutralColors[c])))
                        { e=PhasePixels.Hex(neutralColors[c+1]);neutralPixels++;break; }
                    }
                    if(x>=192&&x<210&&y>=146&&y<149)
                    {
                        for(int c=0;c<tracks.Length;c+=2)if(e.Equals((Color32)PhasePixels.Hex(tracks[c])))
                        { e=PhasePixels.Hex(tracks[c+1]);neutralPixels++;break; }
                    }
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
                    if(p.stamina<55&&!p.grounded&&x>=p.x-2&&x<p.x+10&&y>=headTop+collar&&y<=feet)
                    {
                        if(e.Equals((Color32)PhasePixels.Hex("eadfc5"))||e.Equals((Color32)PhasePixels.Hex("b3a386"))||e.Equals((Color32)PhasePixels.Hex("7d6e5c")))
                        {
                            e=new Color32((byte)Math.Floor(e.r+(255-e.r)*7/8.0+.5),(byte)Math.Floor(e.g+(91-e.g)*7/8.0+.5),(byte)Math.Floor(e.b+(94-e.b)*7/8.0+.5),255);fatiguePixels++;
                        }
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
            File.WriteAllText("TestResults/Web/art-parity.json",JsonConvert.SerializeObject(new { frames=reference.frames.Length,hoodPixelsRemapped=hoodPixels,scarfPixelsRemapped=scarfPixels,fatiguePixelsRemapped=fatiguePixels,neutralPixelsRemapped=neutralPixels,badPixels=bad,maxChannelDelta=worst,firstMismatch=first },Formatting.Indented));
            Assert.That(hoodPixels,Is.GreaterThan(1000),"all poses retain a large dash-charge hood");
            Assert.That(scarfPixels,Is.GreaterThan(100),"spent scarf uses the new dark orange palette");
            Assert.That(fatiguePixels,Is.GreaterThan(80),"only low-stamina body pixels use the warning palette");
            Assert.That(neutralPixels,Is.GreaterThan(360000),"permanent stone, mover and track colors are remapped without changing their texture");
            Assert.That(bad,Is.Zero,first);
            controller.blocks.Add(new Block(0,0,8,8));pixels.Draw(controller);
            Color32 added=pixels.Texture.GetPixel(1,179);
            Assert.That((added.r,added.g,added.b),Is.EqualTo(((byte)232,(byte)232,(byte)232)),"adding terrain invalidates the cached room");
        }
        finally { UnityEngine.Object.DestroyImmediate(golden); UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }
}
