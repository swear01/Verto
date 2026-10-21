using System;
using System.Collections.Generic;
using System.Linq;

namespace Phase
{
    [Serializable]
    public class Block
    {
        public double x, y, w, h;
        public int phase = -1, phaseMask;
        // A block fully covered in spikes: active, it kills on any contact. It still blocks like terrain.
        [NonSerialized] public bool spiked;
        public int Mask => phaseMask == 0 ? (phase < 0 ? 7 : 1 << phase) : phaseMask;
        public bool ActiveInPhase(int state) => (Mask & (1 << state)) != 0;
        public Block(double x, double y, double w, double h, int phase = -1, int phaseMask = 0)
        {
            if (phaseMask == 0 && (phase < -1 || phase > 2)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (phaseMask < 0 || phaseMask > 7) throw new ArgumentOutOfRangeException(nameof(phaseMask));
            this.x = x; this.y = y; this.w = w; this.h = h; this.phase = phase; this.phaseMask = phaseMask;
        }
    }

    public enum SpikeDirection { Up, Down, Left, Right }

    [Serializable]
    public sealed class Spike : Block
    {
        public SpikeDirection direction;
        // The platform an attached row belongs to; it stops hurting while that platform passes through the player.
        [NonSerialized] public Platform owner;
        public Spike(double x, double y, double length, SpikeDirection direction, int phaseMask = 7)
            : base(x,y,direction == SpikeDirection.Up || direction == SpikeDirection.Down ? length : 8,
                direction == SpikeDirection.Up || direction == SpikeDirection.Down ? 8 : length,phaseMask:phaseMask)
        {
            if (length < 8 || length % 8 != 0) throw new ArgumentOutOfRangeException(nameof(length));
            if ((int)direction < 0 || (int)direction > 3) throw new ArgumentOutOfRangeException(nameof(direction));
            if (phaseMask == 0) throw new ArgumentOutOfRangeException(nameof(phaseMask));
            this.direction = direction;
        }
        public Block Hitbox => new Block(x+(direction == SpikeDirection.Left ? 5 : 0),y+(direction == SpikeDirection.Up ? 5 : 0),
            direction == SpikeDirection.Left || direction == SpikeDirection.Right ? 3 : w,
            direction == SpikeDirection.Up || direction == SpikeDirection.Down ? 3 : h);
    }

    public struct Controls
    {
        public int x;
        public bool jump, jumpHeld;
    }

    public sealed class Player : Block
    {
        public double vx, vy, coyote = .1, buffer, remX, remY;
        public double wallLock, slide, retention, retained;
        public double varTime, varSpeed = -105;
        public int facing = 1, airJump, wall, forceX;
        public bool grounded = true;
        public Player(double x, double y) : base(x, y, 8, 11) { }
    }

    // View origin in room pixels. An axis larger than the view eases toward the player and stays inside the room;
    // an axis the view covers keeps the fixed layout: X at 0, Y centred with stone above and void below.
    public sealed class PhaseCamera
    {
        public static readonly double Ease = 1 - Math.Pow(.01, PhaseController.Dt);
        public double x, y;
        public int X => (int)Math.Floor(x + .5);
        public int Y => (int)Math.Floor(y + .5);
        public static double Clamp(double origin, int room, int view, bool vertical) =>
            room <= view ? (vertical ? (room - view) / 2 : 0) : Math.Max(0, Math.Min(room - view, origin));
        public void Follow(PhaseController c, int viewWidth, int viewHeight, bool snap = false)
        {
            var p = c.player;
            double tx = Clamp(p.x + p.w / 2 - viewWidth / 2.0, c.Width, viewWidth, false);
            double ty = Clamp(p.y + p.h / 2 - viewHeight / 2.0, c.Height, viewHeight, true);
            x = Clamp(snap ? tx : x + (tx - x) * Ease, c.Width, viewWidth, false);
            y = Clamp(snap ? ty : y + (ty - y) * Ease, c.Height, viewHeight, true);
        }
    }

    public struct Particle { public double x, y, vx, vy, life; public int phase; }

    // Native pixels, Y down. The player runs and jumps; every jump, including wall and crystal jumps, advances the phase.
    public sealed class PhaseController
    {
        public const double Dt = 1.0 / 60;
        public readonly List<Block> blocks;
        public readonly List<Spike> spikes;
        public readonly List<Laser> lasers;
        public readonly List<Crystal> crystals;
        public readonly List<Platform> platforms;
        readonly int[] spikedBlocks;
        public readonly HashSet<int> blocked = new HashSet<int>();
        public readonly List<Particle> particles = new List<Particle>();
        public readonly double spawnX, spawnY;
        readonly Random random = new Random(1);
        bool airJumpCollected, airJumpRequested;
        public Player player;
        public int phase, jumps, deaths;
        public double time, flash, respawn;
        public bool exitLeft, exitRight;
        // Authored room size in native pixels; Width also grows with ResizeRoom for views wider than the room.
        public readonly int RoomWidth = 320, Height = 180;
        public int Width { get; private set; } = 320;
        public int ExitDirection => respawn > 0 ? 0 : exitLeft && player.x < 0 ? -1 : exitRight && player.x + player.w > Width ? 1 : 0;
        bool WithinSides(double x, double w) => (exitLeft || x >= 0) && (exitRight || x + w <= Width);

        public void ResizeRoom(int width)
        {
            width = Math.Max(RoomWidth, width);
            if (width == Width) return;
            foreach (var block in blocks) if (!(block is Platform) && block.x + block.w == Width) block.w += width - Width;
            Width = width;
            player.x = Math.Min(player.x, Width - player.w);
        }

        public void EnterFrom(PhaseController previous, int direction)
        {
            player = previous.player; phase = previous.phase; time = previous.time;
            airJumpRequested = previous.airJumpRequested;
            jumps = previous.jumps; deaths = previous.deaths; flash = previous.flash;
            player.x = direction > 0 ? 0 : Width - player.w;
            if (previous.Height != Height) player.y = Math.Max(0, Math.Min(player.y, Height - player.h));
            player.wall = player.forceX = 0;
            player.wallLock = player.retention = player.slide = 0;
            UpdateGrounded(Solid());
        }

        public PhaseController(IEnumerable<Block> blocks, double spawnX, double spawnY, IEnumerable<Spike> spikes = null, IEnumerable<Laser> lasers = null, IEnumerable<Crystal> crystals = null, int width = 320, int height = 180, IEnumerable<Platform> platforms = null)
        {
            if (width < 320 || width % 8 != 0 || height < 180 || (height % 8 != 0 && height != 180))
                throw new ArgumentOutOfRangeException(nameof(width), "Room size must be at least 320x180 in 8px steps.");
            RoomWidth = Width = width; Height = height;
            this.platforms = platforms?.ToList() ?? new List<Platform>();
            // Platforms follow the terrain so terrain keeps its indices.
            this.blocks = blocks.Concat(this.platforms).ToList(); this.spawnX = spawnX; this.spawnY = spawnY;
            spikedBlocks = Enumerable.Range(0, this.blocks.Count).Where(i => this.blocks[i].spiked).ToArray();
            this.spikes = (spikes ?? Array.Empty<Spike>()).Concat(this.platforms.SelectMany(p => p.attached)).ToList();
            this.lasers = lasers?.ToList() ?? new List<Laser>();
            this.crystals = crystals?.ToList() ?? new List<Crystal>();
            Reset();
        }
        public void Reset()
        {
            phase = 0; respawn = 0; blocked.Clear();
            player = new Player(Math.Min(spawnX, Width - 8), spawnY);
            airJumpCollected = airJumpRequested = false;
            foreach (var crystal in crystals) crystal.collected = false;
            foreach (var platform in platforms) platform.Restart();
            flash = .08;
        }
        public static bool Overlap(Block a, Block b, double x = 0, double y = 0) =>
            a.x + x < b.x + b.w && a.x + a.w + x > b.x && a.y + y < b.y + b.h && a.y + a.h + y > b.y;
        public List<Block> Solid() => blocks.Where((b, i) => b.ActiveInPhase(phase) && !blocked.Contains(i)).ToList();
        static bool Collides(Player p, List<Block> solids, double x = 0, double y = 0) => solids.Any(b => Overlap(p, b, x, y));
        static int WallAt(Player p, List<Block> solids, int preferred, double reach = 3)
        {
            foreach (int dir in new[] { preferred, -preferred }) foreach (var b in solids)
            {
                if (p.y + p.h <= b.y || p.y >= b.y + b.h) continue;
                double distance = dir > 0 ? b.x - (p.x + p.w) : p.x - (b.x + b.w);
                if (distance >= -.01 && distance < reach) return dir;
            }
            return 0;
        }
        static bool OnGround(Player p, List<Block> solids) => solids.Any(b => Overlap(p, b, 0, 1) && p.y + p.h <= b.y);
        static double Approach(double value, double target, double amount) => value < target ? Math.Min(value + amount, target) : Math.Max(value - amount, target);
        public static int PixelRound(double value) => (int)Math.Round(value, MidpointRounding.ToEven);
        void Burst(double x, double y, int color, int count = 8)
        {
            for (int i = 0; i < count; i++)
            {
                double angle = random.NextDouble() * Math.PI * 2, speed = 8 + random.NextDouble() * 33;
                particles.Add(new Particle { x = x, y = y, vx = Math.Cos(angle) * speed, vy = Math.Sin(angle) * speed, life = .3 + random.NextDouble() * .2, phase = color });
            }
        }
        void Kill()
        {
            if (respawn > 0) return;
            deaths++; respawn = .28; Burst(player.x + 4, player.y + 5, -1, 18);
        }
        public bool CheckSpikes(Block from = null)
        {
            if (respawn > 0) return true;
            var p = player; from = from ?? p;
            foreach (var spike in spikes)
            {
                if (!spike.ActiveInPhase(phase) || Shielded(spike)) continue;
                if (HitsSweep(spike.Hitbox, from)) { Kill(); return true; }
            }
            foreach (int i in spikedBlocks)
                if (Lethal(i) && HitsSweep(Reach(blocks[i]), from)) { Kill(); return true; }
            return false;
        }
        // A spike-covered block hurts on contact, so its reach is one half pixel past each face.
        public static Block Reach(Block b) => new Block(b.x - .5, b.y - .5, b.w + 1, b.h + 1);
        bool Lethal(int i) => blocks[i].ActiveInPhase(phase) && !blocked.Contains(i);
        // The block index of the n-th platform; platforms follow the terrain.
        public int PlatformIndex(int n) => blocks.Count - platforms.Count + n;
        bool Shielded(Spike spike) => spike.owner != null && blocked.Contains(PlatformIndex(platforms.IndexOf(spike.owner)));
        bool HitsSweep(Block b, Block from)
        {
            if (b.w <= 0 || b.h <= 0) return false;
            var p = player; double enter = 0, leave = 1;
            foreach (var axis in new[] { (at:p.x,start:from.x,lo:b.x-p.w,hi:b.x+b.w), (at:p.y,start:from.y,lo:b.y-p.h,hi:b.y+b.h) })
            {
                double delta = axis.at-axis.start;
                if (delta == 0) { if (axis.at <= axis.lo || axis.at >= axis.hi) return false; }
                else
                {
                    double a = (axis.lo-axis.start)/delta, z = (axis.hi-axis.start)/delta;
                    enter = Math.Max(enter,Math.Min(a,z)); leave = Math.Min(leave,Math.Max(a,z));
                }
            }
            return enter < leave;
        }
        public Block LaserBeam(Laser laser) => laser.Beam(Solid(), Width, Height);
        public bool CheckHazards(Block from = null)
        {
            if (CheckSpikes(from)) return true;
            if (lasers.Count == 0) return false;
            var solids = Solid();
            foreach (var laser in lasers)
                if (laser.ActiveInPhase(phase) && HitsSweep(laser.Beam(solids, Width, Height), from ?? player))
                { Kill(); return true; }
            return false;
        }
        void CollectCrystals(Block from = null)
        {
            foreach (var crystal in crystals)
            {
                if (crystal.collected || !crystal.ActiveInPhase(phase) || !HitsSweep(crystal, from ?? player)) continue;
                crystal.collected = true; player.airJump = 1; airJumpCollected = true; airJumpRequested = false;
                Burst(crystal.x + 4, crystal.y + 4, crystal.Mask == 7 ? -1 : phase);
            }
        }
        // A spot overlapping an active spike strip or laser beam, without killing.
        bool Hazardous(Block at) => spikes.Any(s => s.ActiveInPhase(phase) && !Shielded(s) && Overlap(at, s.Hitbox)) ||
            spikedBlocks.Any(i => Lethal(i) && Overlap(at, Reach(blocks[i]))) ||
            lasers.Any(l => l.ActiveInPhase(phase) && Overlap(at, l.Beam(Solid(), Width, Height)));
        bool CheckInteractions(Block from = null)
        {
            if (CheckHazards(from)) return true;
            CollectCrystals(from);
            return false;
        }
        void UpdateGrounded(List<Block> solids)
        {
            bool grounded = player.vy >= 0 && OnGround(player, solids);
            if (grounded && !player.grounded)
            {
                player.airJump = 0;
                foreach (var crystal in crystals) crystal.collected = false;
            }
            player.grounded = grounded;
        }
        void Shift()
        {
            int previous = phase; phase = (phase + 1) % 3; flash = .08;
            for (int i = 0; i < blocks.Count; i++) if (!blocks[i].ActiveInPhase(previous) && blocks[i].ActiveInPhase(phase) && Overlap(player, blocks[i])) blocked.Add(i);
            Burst(player.x + 4, player.y + 5, phase);
            CheckInteractions();
        }
        // A wall jump pushes away from the wall and briefly locks steering toward it.
        void Jump(int wall, Controls input, bool airborne = false)
        {
            var p = player;
            airJumpRequested = false;
            if (airborne) p.airJump = 0;
            if (wall != 0)
            {
                p.vx = -wall * 130; p.facing = -wall;
                if (input.x != 0) { p.forceX = -wall; p.wallLock = .16; }
            }
            else p.vx += input.x * 40;
            p.vy = p.varSpeed = -105; p.varTime = .2;
            p.grounded = false; p.coyote = p.buffer = 0; p.slide = 0;
            jumps++; foreach (var platform in platforms) platform.queued++;
            Shift(); Burst(p.x + 4, p.y + 11, -1, 4);
        }
        // Platforms slide one pixel at a time. A solid one carries a rider and pushes the player; a player it cannot push
        // (against a wall) is not crushed: they pass through each other until the player is clear.
        bool MovePlatforms(double dt)
        {
            for (int n = 0; n < platforms.Count; n++)
            {
                var (dx, dy) = platforms[n].Advance(dt);
                for (int step = 0; step < Math.Abs(dx) + Math.Abs(dy); step++)
                    if (Nudge(platforms[n], PlatformIndex(n), step < Math.Abs(dx) ? Math.Sign(dx) : 0, step < Math.Abs(dx) ? 0 : Math.Sign(dy))) return true;
            }
            return false;
        }
        bool Nudge(Platform platform, int index, int sx, int sy)
        {
            var p = player;
            bool solid = platform.ActiveInPhase(phase) && !blocked.Contains(index);
            bool rides = solid && p.vy >= 0 && Overlap(p, platform, 0, 1) && p.y + p.h <= platform.y;
            platform.Move(sx, sy);
            if (solid && (rides || Overlap(p, platform)))
            {
                var others = Solid().Where(b => b != platform).ToList();
                if (WithinSides(p.x + sx, p.w) && p.y + sy >= 0 && p.y + p.h + sy <= Height && !Collides(p, others, sx, sy)) { p.x += sx; p.y += sy; }
                else if (Overlap(p, platform)) blocked.Add(index);
            }
            return CheckInteractions();
        }
        public void Step(Controls input, double dt = Dt)
        {
            time += dt; flash = Math.Max(0, flash - dt);
            for (int i = particles.Count - 1; i >= 0; i--)
            { var q = particles[i]; q.x += q.vx * dt; q.y += q.vy * dt; q.vy += 67 * dt; q.life -= dt; if (q.life <= 0) particles.RemoveAt(i); else particles[i] = q; }
            if (respawn > 0) { respawn = Math.Max(0, respawn - dt); if (respawn == 0) Reset(); return; }
            var p = player; if (CheckHazards()) return;
            airJumpCollected = false;
            p.buffer = input.jump ? .08 : input.jumpHeld ? Math.Max(0, p.buffer - dt) : 0;
            if (p.buffer <= 0) airJumpRequested = false;
            if (input.jump && p.airJump > 0) airJumpRequested = true;
            blocked.RemoveWhere(i => !Overlap(player, blocks[i]));
            if (MovePlatforms(dt)) return;
            var solids = Solid(); int contact = WallAt(p, solids, input.x != 0 ? input.x : p.facing);
            p.wall = contact; UpdateGrounded(solids); CollectCrystals();
            int move = p.wallLock > 0 ? p.forceX : input.x;
            p.coyote = p.grounded ? .1 : Math.Max(0, (double)((float)p.coyote - (float)dt)); if (p.coyote < 1e-8) p.coyote = 0;
            p.wallLock = Math.Max(0, p.wallLock - dt);
            p.varTime = Math.Max(0, p.varTime - dt);
            if (p.grounded) p.slide = 0;
            if (p.retention > 0)
            {
                if (Math.Sign(p.vx) == -Math.Sign(p.retained)) p.retention = 0;
                else if (WallAt(p, solids, Math.Sign(p.retained), 1) == 0) { p.vx = p.retained; p.retention = 0; }
                else p.retention = Math.Max(0, p.retention - dt);
            }
            double mult = p.grounded ? 1 : .65, acceleration = Math.Abs(p.vx) > 90 && Math.Sign(p.vx) == move ? 400 : 1000;
            p.vx = Approach(p.vx, move * 90, acceleration * mult * dt);
            if (move != 0) p.facing = move;
            double maxFall = 160;
            // Pressing into a wall while falling slides down it, slowly at first.
            int slide = WallAt(p, solids, p.facing, 1);
            if (slide == p.facing && move == p.facing && p.vy >= 0 && p.slide < 1.2)
            { maxFall = 20 + (160 - 20) * p.slide / 1.2; p.slide += dt; }
            if (!p.grounded) { double gravity = Math.Abs(p.vy) < 40 && input.jumpHeld ? 450 : 900; p.vy = Approach(p.vy, maxFall, gravity * dt); }
            if (p.varTime > 1e-9) { if (input.jumpHeld) p.vy = Math.Min(p.vy, p.varSpeed); else p.varTime = 0; }
            if (p.buffer > 0 && (p.coyote > 0 || contact != 0)) { var jumpInput = input; jumpInput.x = move; Jump(p.coyote > 0 ? 0 : contact, jumpInput); }
            else if (airJumpRequested && !airJumpCollected && p.airJump > 0 && !p.grounded) Jump(0, input, true);
            if (respawn > 0 || CheckHazards()) return;
            p.remX += p.vx * dt; int moveX = PixelRound(p.remX); p.remX -= moveX;
            bool hitX = false;
            for (int n = 0; n < Math.Abs(moveX) && !hitX; n++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                solids = Solid(); int vx = Math.Sign(moveX); p.x += vx;
                // Pressing into a ledge whose top is at most 4px above the feet steps up onto it when that spot is clear.
                if (move == vx && Collides(p, solids))
                    for (int lift = 1; lift <= 4; lift++)
                        if (p.y - lift >= 0 && !Collides(p, solids, 0, -lift) && !Hazardous(new Block(p.x, p.y - lift, p.w, p.h))) { p.y -= lift; break; }
                foreach (var b in solids) if (Overlap(p, b))
                {
                    p.remX = 0; p.x = vx > 0 ? b.x - p.w : b.x + b.w;
                    if (p.retention <= 0) { p.retained = p.vx; p.retention = .06; }
                    p.vx = 0; hitX = true;
                }
                if (!exitLeft) p.x = Math.Max(0, p.x);
                if (!exitRight) p.x = Math.Min(Width - p.w, p.x);
                if (CheckInteractions(before)) return;
            }
            p.remY += p.vy * dt; int moveY = PixelRound(p.remY); p.remY -= moveY;
            bool hitY = false;
            for (int n = 0; n < Math.Abs(moveY) && !hitY; n++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                solids = Solid(); int vy = Math.Sign(moveY); p.y += vy;
                foreach (var b in solids) if (Overlap(p, b))
                {
                    p.remY = 0; bool corrected = false;
                    // Rising into a corner nudges the player up to 4px sideways.
                    if (vy < 0)
                        for (int offset = 1; offset <= 4 && !corrected; offset++) foreach (int sign in new[] { -1, 1 })
                        {
                            if (p.vx * sign < 0) continue;
                            double x = p.x + offset * sign;
                            if (WithinSides(x, p.w) && !Collides(p, solids, offset * sign)) { p.x = x; corrected = true; break; }
                        }
                    if (!corrected)
                    {
                        p.y = vy > 0 ? b.y - p.h : b.y + b.h;
                        if (vy > 0 && p.vy >= 120)
                            for (int dust = 0; dust < 6; dust++)
                                particles.Add(new Particle { x = p.x + p.w / 2, y = p.y + p.h - 1,
                                    vx = (dust % 2 == 0 ? -1 : 1) * (12 + random.NextDouble() * 24),
                                    vy = -10 - random.NextDouble() * 15, life = .25 + random.NextDouble() * .15, phase = -1 });
                        p.vy = 0; hitY = true; if (vy > 0 || p.varTime < .2 - .05) p.varTime = 0;
                    }
                }
                if (CheckInteractions(before)) return;
            }
            if (p.y < 0) { p.y = 0; p.vy = Math.Max(0, p.vy); p.varTime = p.remY = 0; }
            solids = Solid(); UpdateGrounded(solids); p.wall = WallAt(p, solids, input.x != 0 ? input.x : p.facing);
            if (p.y > Height + 14) Kill();
            if (respawn <= 0) CollectCrystals();
        }
    }
}
