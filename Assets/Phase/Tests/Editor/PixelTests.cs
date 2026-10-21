using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using Phase;
using UnityEngine;

public class PixelTests
{
    public class Reference { public int columns; public Block[] blocks; public Frame[] frames; }
    public class Frame { public string name; public double time; public int phase; public int[] blocked; public Player player; }


    [Test]
    public void PlatformsAreDrawnWhereTheyAreAndSpikeCoverChangesTheBlock()
    {
        var pixels = new PhasePixels();
        try
        {
            var platform = new Platform(false, 3, new[] { (10, 5), (20, 5) }, false, 7);
            var room = new PhaseController(Array.Empty<Block>(), 32, 149, platforms: new[] { platform }); room.flash = 0;
            pixels.Draw(room, false); var home = pixels.Texture.GetPixels32();
            platform.queued = 1; for (int i = 0; i < 20; i++) room.Step(default);
            pixels.Draw(room, false); var moved = pixels.Texture.GetPixels32();
            Color32 At(Color32[] frame, int x, int y) => frame[(179 - y) * 320 + x];
            Assert.That(At(home, 80, 41), Is.Not.EqualTo(At(moved, 80, 41)), "the first stop is empty after the platform left");
            Assert.That(At(moved, 160, 41), Is.Not.EqualTo(At(home, 160, 41)), "the second stop shows the platform");
            var plain = new PhaseController(new[] { new Block(80, 80, 8, 8, phaseMask: 7) }, 32, 40); plain.flash = 0;
            var covered = new PhaseController(new[] { new Block(80, 80, 8, 8, phaseMask: 7) { spiked = true } }, 32, 40); covered.flash = 0;
            pixels.Draw(plain, false); var bricks = pixels.Texture.GetPixels32();
            pixels.Draw(covered, false); var teeth = pixels.Texture.GetPixels32();
            Assert.That(Enumerable.Range(80, 8).SelectMany(x => Enumerable.Range(80, 8).Select(y => At(bricks, x, y).Equals(At(teeth, x, y)))).Count(same => !same), Is.GreaterThan(8), "a spike-covered cell looks different");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }
    [Test]
    public void CrystalShapesColorsAndGhostsKeepTheirPickupMeaning()
    {
        var pixels = new PhasePixels();
        Color32 At(int x,int y) => pixels.Texture.GetPixel(x,179-y);
        int Distance(Color32 a,Color32 b) => Math.Abs(a.r-b.r)+Math.Abs(a.g-b.g)+Math.Abs(a.b-b.b);
        try
        {
            var empty = new PhaseController(Array.Empty<Block>(),32,149); empty.flash=0;
            pixels.Draw(empty,false); var background=pixels.Texture.GetPixels32();
            for (int mask=1;mask<=7;mask++)
            {
                var crystal=new Crystal(120,80,mask);
                var room=new PhaseController(Array.Empty<Block>(),32,149,crystals:new[]{crystal});room.flash=0;
                for(int phase=0;phase<3;phase++)if(crystal.ActiveInPhase(phase))
                {
                    room.phase=phase;pixels.Draw(room,false);
                    Assert.That(At(123,80),Is.EqualTo((Color32)PhasePixels.SurfaceColor(mask)),"active rim uses the shared phase palette");
                    Assert.That(At(120,80),Is.EqualTo(background[(179-80)*320+120]),"round corners are cut away");
                }
                room.time=2;pixels.Draw(room,false);var active=pixels.Texture.GetPixels32();
                room.time=5;pixels.Draw(room,false);CollectionAssert.AreEqual(active,pixels.Texture.GetPixels32(),"crystals do not blink or add competing motion");
                for(int phase=0;phase<3;phase++)if(!crystal.ActiveInPhase(phase))
                {
                    room.phase=phase;pixels.Draw(room,false);
                    int index=(179-80)*320+123;
                    Assert.That(Distance(At(123,80),background[index]),Is.GreaterThan(0),"unavailable crystals remain discoverable");
                    Assert.That(Distance(At(123,80),background[index]),Is.LessThan(Distance(active[index],background[index])),"wrong-phase crystals are visibly translucent");
                }
                crystal.collected=true;pixels.Draw(room,false);CollectionAssert.AreEqual(background,pixels.Texture.GetPixels32(),"consumed crystals cannot look available");
            }
            var covered=new PhaseController(Array.Empty<Block>(),32,149,lasers:new[]{new Laser(8,80,LaserDirection.Right)},crystals:new[]{new Crystal(120,80)});covered.flash=0;
            pixels.Draw(covered,false);
            Assert.That(At(123,83),Is.EqualTo((Color32)PhasePixels.Hex("f6f6f6")),"a pickup never hides a lethal beam crossing it");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void ExtraJumpRingChangesOnlyTheCloakAndClearsWhenSpent()
    {
        var pixels=new PhasePixels();
        var room=new PhaseController(Array.Empty<Block>(),120,100);room.flash=0;
        try
        {
            foreach(int facing in new[]{-1,1})foreach(int phase in new[]{0,1,2})
            {
                room.player.facing=facing;room.phase=phase;
                room.player.airJump=0;pixels.Draw(room);var plain=pixels.Texture.GetPixels32();
                room.player.airJump=1;pixels.Draw(room);var bonus=pixels.Texture.GetPixels32();
                int changed=0,feet=(int)Math.Floor(room.player.y+room.player.h+.5);
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)
                {
                    int index=(179-y)*320+x;
                    if(bonus[index].Equals(plain[index]))continue;
                    changed++;
                    Assert.That(x,Is.InRange(122,124));Assert.That(y,Is.InRange(feet-6,feet-4),"hood, eyes and scarf keep their existing meanings");
                }
                Assert.That(changed,Is.GreaterThan(4));
                Assert.That((Color32)pixels.Texture.GetPixel(123,179-(feet-6)),Is.EqualTo((Color32)PhasePixels.Hex("e8e8e8")),"bonus is a neutral ring independent of current phase and dash");
                room.player.airJump=0;pixels.Draw(room);CollectionAssert.AreEqual(plain,pixels.Texture.GetPixels32(),"spending the jump restores original art exactly");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void LasersStaySteadyAndSafeColorsAreTransparent()
    {
        var pixels = new PhasePixels();
        try
        {
            foreach (LaserDirection direction in Enum.GetValues(typeof(LaserDirection))) for (int mask = 1; mask <= 7; mask++)
            {
                var laser = new Laser(120,80,direction,mask);
                var s = new PhaseController(Array.Empty<Block>(),32,149,lasers:new[]{laser}); s.flash=0;
                var empty = new PhaseController(Array.Empty<Block>(),32,149); empty.flash=0;
                pixels.Draw(empty,false); var background=pixels.Texture.GetPixels32();
                s.phase=mask==2||mask==6?1:mask==4?2:0;
                pixels.Draw(s,false); var active=pixels.Texture.GetPixels32();
                var b=s.LaserBeam(laser);
                for(int y=(int)b.y;y<b.y+b.h;y++)for(int x=(int)b.x;x<b.x+b.w;x++)
                    Assert.That(active[(179-y)*320+x],Is.Not.EqualTo(background[(179-y)*320+x]),"every lethal pixel is visible");
                s.time+=3; pixels.Draw(s,false); CollectionAssert.AreEqual(active,pixels.Texture.GetPixels32(),"steady light has no time cycle");
                for(int phase=0;phase<3;phase++)if(!laser.ActiveInPhase(phase))
                {
                    s.phase=phase; pixels.Draw(s,false);var safe=pixels.Texture.GetPixels32();
                    int at=(179-(int)(b.y+b.h/2))*320+(int)(b.x+b.w/2);
                    int Distance(Color32 a,Color32 z)=>Math.Abs(a.r-z.r)+Math.Abs(a.g-z.g)+Math.Abs(a.b-z.b);
                    Assert.That(Distance(safe[at],background[at]),Is.GreaterThan(0),"safe beams remain visible");
                    Assert.That(Distance(safe[at],background[at]),Is.LessThan(Distance(active[at],background[at])),"safe beams reveal the backdrop");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void LaserArtStopsAtCurrentCoverAndIgnoresGhostCover()
    {
        var pixels=new PhasePixels();
        try
        {
            var laser=new Laser(8,80,LaserDirection.Right);
            var s=new PhaseController(new[]{new Block(80,80,8,8,0)},32,149,lasers:new[]{laser});s.flash=0;
            pixels.Draw(s,false);var solid=pixels.Texture.GetPixels32();
            s.phase=1;pixels.Draw(s,false);var ghost=pixels.Texture.GetPixels32();
            int before=(179-83)*320+60,after=(179-83)*320+100;
            Assert.That(solid[before],Is.EqualTo(ghost[before]),"beam stays bright before the blocker");
            Assert.That(solid[after],Is.Not.EqualTo(ghost[after]),"ghost terrain no longer truncates the drawn beam");
            s.phase=0;s.time=5;pixels.Draw(s,false);CollectionAssert.AreEqual(solid,pixels.Texture.GetPixels32());
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
            foreach(int phase in new[]{0,1,2})
            {
                s.phase=phase;pixels.Draw(s);
                Assert.That((Color32)pixels.Texture.GetPixel(24,19),Is.EqualTo((Color32)PhasePixels.Hex("e8e8e8")),"permanent floor stays white in every phase");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void TallViewCentersPlayAreaAndMarksBothBoundaries()
    {
        var pixels=new PhasePixels();
        var blocks=new[]{new Block(0,160,112,24),new Block(208,160,112,24),new Block(128,96,64,8)};
        var room=new PhaseController(blocks,32,149);room.flash=0;
        try
        {
            pixels.SetView(320,693,0);pixels.Draw(room);
            Assert.That(pixels.OriginY,Is.EqualTo(-256));
            Color32 top = pixels.Texture.GetPixel(160, pixels.Height - 1);
            Assert.That(top.r, Is.EqualTo(top.g), "portrait padding above the play area is neutral stone");
            Assert.That(top.r, Is.GreaterThan(50));
            Assert.That((Color32)pixels.Texture.GetPixel(160, pixels.Height - 1 - (194 - pixels.OriginY)), Is.EqualTo((Color32)PhasePixels.Hex("0b0f16")), "void becomes fully dark at the existing death boundary");
            Assert.That(Math.Abs(-pixels.OriginY+90-pixels.Height/2.0),Is.LessThanOrEqualTo(.5),"play area is centered");
            var stone=pixels.Texture.GetPixel(32,40);var gap=pixels.Texture.GetPixel(160,40);
            Assert.That(stone,Is.Not.EqualTo(gap),"bottom stone extends while the gap remains void");
            Assert.That(blocks[0].h,Is.EqualTo(24),"visual extension must not enlarge collision terrain");
            var voidRoom=new PhaseController(new[]{new Block(128,96,64,8)},32,64);voidRoom.flash=0;
            pixels.Draw(voidRoom);
            int terraces=0;
            for(int y=0;y<pixels.Height-1-(194-pixels.OriginY);y++)foreach(int x in new[]{32,160})
            {
                Color32 depth=pixels.Texture.GetPixel(x,y);
                Assert.That(Math.Max(depth.r,Math.Max(depth.g,depth.b)),Is.LessThan(48),"bottom void and floating terrain stay quiet dark depths");
                if(!depth.Equals((Color32)PhasePixels.Hex("0b0f16")))terraces++;
            }
            Assert.That(terraces,Is.GreaterThan(0),"void depths show receding quarry terraces");
            var gridFloor = new PhaseController(new[] { new Block(0,168,320,8) },32,157); gridFloor.flash=0;
            pixels.Draw(gridFloor);
            Assert.That(pixels.Texture.GetPixel(32,40), Is.Not.EqualTo(gap), "the last complete 8px grid row extends as stone");
            pixels.SetView(320,180,0);pixels.Draw(room);Assert.That(pixels.OriginY,Is.Zero);
            pixels.SetView(320,693,0);pixels.Draw(room);Assert.That(pixels.Texture.GetPixel(32,40),Is.EqualTo(stone),"resize rebuilds extended terrain");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void BigRoomViewIsAWindowOntoTheWholeRoom()
    {
        var s=new PhaseController(new[]{new Block(0,352,640,8),new Block(600,40,24,8,1),new Block(300,200,8,80)},316,180,
            lasers:new[]{new Laser(8,300,LaserDirection.Right)},width:640,height:360);s.flash=0;
        var pixels=new PhasePixels();
        try
        {
            pixels.SetView(640,360,0);pixels.Draw(s);var whole=pixels.Texture.GetPixels32();
            Assert.That(pixels.OriginY,Is.Zero,"a view that covers the room has no padding");
            Color32 sky=whole[359*640+5];Assert.That(sky.b,Is.GreaterThan(sky.r),"open sky above the skyline, not stone");
            pixels.SetView(320,180,0);
            foreach(var (x,y) in new[]{(0,0),(160,96),(320,180),(1000,-50)})
            {
                pixels.Scroll(x,y);pixels.Draw(s);var frame=pixels.Texture.GetPixels32();
                int ox=Math.Max(0,Math.Min(320,x)),oy=Math.Max(0,Math.Min(180,y));
                Assert.That((pixels.OriginX,pixels.OriginY),Is.EqualTo((ox,oy)),"origin stays inside the room");
                for(int row=0;row<180;row++)for(int col=0;col<320;col++)
                    if(!frame[row*320+col].Equals(whole[(row+180-oy)*640+col+ox])){Assert.Fail($"{x},{y}: pixel {col},{row} differs from the whole room");}
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }



    [Test]
    public void RespawnConvergesAtCheckpointWithoutFlashingTheWorld()
    {
        var room=new PhaseController(new[]{new Block(0,160,320,24)},120,100);room.flash=0;
        var pixels=new PhasePixels();
        try
        {
            pixels.Draw(room,false);var background=pixels.Texture.GetPixels32();
            room.respawn=.11;pixels.Draw(room);var early=pixels.Texture.GetPixels32();
            room.respawn=.03;pixels.Draw(room);var late=pixels.Texture.GetPixels32();
            int outerEarly=0,outerLate=0,changed=0;
            for(int y=0;y<180;y++)for(int x=0;x<320;x++)
            {
                int index=(179-y)*320+x;
                if(!early[index].Equals(background[index]))
                {
                    changed++;Assert.That(Math.Abs(x-124),Is.LessThanOrEqualTo(11));Assert.That(Math.Abs(y-105),Is.LessThanOrEqualTo(11));
                    if(Math.Abs(x-124)>5||Math.Abs(y-105)>5)outerEarly++;
                }
                if(!late[index].Equals(background[index])&&(Math.Abs(x-124)>5||Math.Abs(y-105)>5))outerLate++;
            }
            Assert.That(changed,Is.EqualTo(8));Assert.That(outerEarly,Is.GreaterThan(0));Assert.That(outerLate,Is.Zero,"the eight sparks contract onto the checkpoint");
            Assert.That(room.player.x,Is.EqualTo(120));Assert.That(room.player.y,Is.EqualTo(100),"animation does not move collision");
            room.respawn=.001;room.Step(new Controls{x=1,jump=true});Assert.That(room.respawn,Is.Zero);Assert.That(room.jumps,Is.Zero,"respawn timing and input freeze are unchanged");
            room.flash=0;pixels.Draw(room);Assert.That(pixels.Texture.GetPixels32(),Is.Not.EqualTo(background),"the player returns after the cue");
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void PublishedTerrainArtRemainsUnchangedAcrossAllCharacterPoses()
    {
        var reference=JsonConvert.DeserializeObject<Reference>(File.ReadAllText("Verification/ArtReference.json"));
        var golden=new Texture2D(2,2);
        var pixels=new PhasePixels();
        try
        {
            Assert.That(golden.LoadImage(File.ReadAllBytes("Verification/ArtReference.png")),Is.True);
            var expected=golden.GetPixels32();var atlas=new Color32[expected.Length];
            var controller=new PhaseController(reference.blocks,120,149);
            var neutralColors=new[]{"a9b8c5","e8e8e8","697d8e","a0a0a0","3c4a57","646464","506170","7f7f7f","34404b","565656","242c34","3c3c3c","191f25","2a2a2a","f2dc8a","f0f0f0","e7ce76","e8e8e8","6f603c","787878","8f7c47","a0a0a0","4b4129","484848"};
            int worst=0,bad=0,neutralPixels=0;string first=null;
            for(int f=0;f<reference.frames.Length;f++)
            {
                var frame=reference.frames[f];controller.player=frame.player;controller.time=frame.time;controller.phase=frame.phase;controller.flash=0;
                controller.blocked.Clear();foreach(int index in frame.blocked)controller.blocked.Add(index);
                pixels.Draw(controller);var actual=pixels.Texture.GetPixels32();
                var p=frame.player;int feet=(int)Math.Floor(p.y+p.h+.5);
                int headTop=feet-13,collar=frame.name.StartsWith("run1 ")||frame.name.StartsWith("run3 ")?6:7;
                for(int y=0;y<180;y++)for(int x=0;x<320;x++)
                {
                    int index=(golden.height-1-(f/reference.columns*180+y))*golden.width+(f%reference.columns*320+x);
                    var a=actual[(179-y)*320+x];var e=expected[index];atlas[index]=a;
                    bool stone=y>=160||(x>=224&&x<240&&y>=80&&y<160);
                    // The reference still shows the removed dash platform and its track; those pixels are skipped.
                    if((x>=159&&x<211&&y>=143&&y<153)) continue;
                    if(stone)
                    {
                        for(int c=0;c<neutralColors.Length;c+=2)if(e.Equals((Color32)PhasePixels.Hex(neutralColors[c])))
                        { e=PhasePixels.Hex(neutralColors[c+1]);neutralPixels++;break; }
                    }
                    if(x>=p.x-12&&x<p.x+19&&y>=headTop-3&&y<=feet+2) continue;
                    int delta=Math.Max(Math.Abs(a.r-e.r),Math.Max(Math.Abs(a.g-e.g),Math.Abs(a.b-e.b)));
                    worst=Math.Max(worst,delta);
                    if(delta>2){bad++;if(first==null)first=$"{frame.name} ({x},{y}) expected {e} actual {a}";}
                }
            }
            Directory.CreateDirectory("TestResults/Web");
            var rendered=new Texture2D(golden.width,golden.height,TextureFormat.RGBA32,false);
            try { rendered.SetPixels32(atlas);rendered.Apply();File.WriteAllBytes("TestResults/Web/art-unity.png",rendered.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(rendered); }
            File.WriteAllText("TestResults/Web/art-parity.json",JsonConvert.SerializeObject(new { frames=reference.frames.Length,neutralPixelsRemapped=neutralPixels,badPixels=bad,maxChannelDelta=worst,firstMismatch=first },Formatting.Indented));
            Assert.That(neutralPixels,Is.GreaterThan(300000),"permanent stone colors are remapped without changing their texture");
            Assert.That(bad,Is.Zero,first);
            controller.blocks.Add(new Block(0,0,8,8));pixels.Draw(controller);
            Color32 added=pixels.Texture.GetPixel(1,179);
            Assert.That((added.r,added.g,added.b),Is.EqualTo(((byte)232,(byte)232,(byte)232)),"adding terrain invalidates the cached room");
        }
        finally { UnityEngine.Object.DestroyImmediate(golden); UnityEngine.Object.DestroyImmediate(pixels.Texture); }
    }

    [Test]
    public void MenusFitTheReferenceViews()
    {
        foreach (var icon in new[] { PhaseMenu.PlayIcon, PhaseMenu.LevelsIcon, PhaseMenu.EditIcon, PhaseMenu.SettingsIcon, PhaseMenu.HomeIcon, PhaseMenu.RetryIcon, PhaseMenu.CloseIcon })
        {
            Assert.That(icon.Length, Is.EqualTo(PhaseMenu.Size));
            foreach (var row in icon) Assert.That(row.Length, Is.EqualTo(PhaseMenu.Size));
        }
        foreach (var size in new[] { new Vector2Int(320, 180), new Vector2Int(427, 180), new Vector2Int(320, 693) })
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = 0, maxY = 0;
            for (int i = 0; i < PhaseSandbox.Levels.Length; i++)
            {
                var tile = PhaseLevels.Tile(size, i);
                Assert.That(tile.xMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(tile.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(tile.xMax, Is.LessThanOrEqualTo(size.x));
                Assert.That(tile.yMax, Is.LessThanOrEqualTo(size.y));
                minX = Math.Min(minX, tile.xMin); minY = Math.Min(minY, tile.yMin);
                maxX = Math.Max(maxX, tile.xMax); maxY = Math.Max(maxY, tile.yMax);
            }
            float fill = Math.Max((maxX - minX) / (float)size.x, (maxY - minY) / (float)size.y);
            Assert.That(fill, Is.InRange(0.6f, 0.8f), $"level select fill at {size}");
            int scale = UiZoom.Fit(size, 80, 44).Scale;
            Assert.That((3 * PhaseMenu.Pitch + PhaseMenu.Size) * scale, Is.LessThanOrEqualTo(size.x));
            Assert.That(44 * scale, Is.LessThanOrEqualTo(size.y));
            var page = UiZoom.Fit(size, 67, 128);
            Assert.That(page.Origin.x, Is.GreaterThanOrEqualTo(0));
            Assert.That(page.Origin.y, Is.GreaterThanOrEqualTo(0));
            Assert.That(page.Origin.x + 67 * page.Scale, Is.LessThanOrEqualTo(size.x));
            Assert.That(page.Origin.y + 128 * page.Scale, Is.LessThanOrEqualTo(size.y));
        }
        var standard = new Vector2Int(320, 180);
        int columns = PhaseLevels.Columns(standard);
        Assert.That(PhaseLevels.Tile(standard, 1).yMin, Is.EqualTo(PhaseLevels.Tile(standard, 0).yMin));
        Assert.That(PhaseLevels.Tile(standard, columns).xMin, Is.EqualTo(PhaseLevels.Tile(standard, 0).xMin));
        Assert.That(PhaseLevels.Tile(standard, columns).yMin, Is.GreaterThan(PhaseLevels.Tile(standard, 0).yMin));
    }
}
