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

    [Serializable]
    public class MovingPlatform : Block
    {
        public double[][] nodes;
        public bool returns, moving;
        public int start, index, pending;
        public double elapsed, fromX, fromY, duration, hold;
        public MovingPlatform(double[][] nodes, double w, double h, bool returns = false, int start = 0)
            : base(nodes[start][0], nodes[start][1], w, h)
        { this.nodes = nodes; this.returns = returns; this.start = start; index = start; }
        public MovingPlatform Fresh() => new MovingPlatform(nodes, w, h, returns, start);
    }

    public struct Controls
    {
        public int x, y;
        public bool jump, dash, jumpHeld, dashHeld, grab;
    }

    public sealed class ClimbGear
    {
        public readonly Block wall, platform;
        public readonly double top, bottom, startX, endX;
        public double Lift => bottom - wall.y;
        public ClimbGear(Block wall, double top, Block platform, double endX)
        {
            if (top < 0 || top >= wall.y || wall.w <= 0 || wall.h <= 0 || platform.w <= 0 || platform.h <= 0 || endX == platform.x)
                throw new ArgumentException("Gear requires a vertical wall range and a horizontal platform range.");
            double pinionX = wall.x + wall.w + 39;
            if (endX - platform.x != wall.y - top || pinionX < endX + 6 || pinionX > platform.x + platform.w - 6)
                throw new ArgumentException("Equal gears require matching travel and a platform rack that stays over the output pinion.");
            this.wall = wall; this.platform = platform; this.top = top;
            bottom = wall.y; startX = platform.x; this.endX = endX;
        }
        public ClimbGear Fresh() => new ClimbGear(new Block(wall.x, bottom, wall.w, wall.h), top,
            new Block(startX, platform.y, platform.w, platform.h), endX);
    }

    public sealed class Player : Block
    {
        public double vx, vy, coyote = .1, buffer, dashBuffer, dashAttack, remX, remY;
        public double dashTime, dashCooldown, dx, dy, wallLock, slide, maxFall = 160;
        public double climbWait, wallBoost, retention, retained, dashRefill, beforeDash;
        public double varTime, varSpeed = -105, stamina = 110;
        public int facing = 1, dash = 1, wall, forceX, climbMove, hopX, boostDir;
        public bool grounded = true, hopMover, dashPending, dashRide, dashStartedOnGround;
        public bool ducking, autoJump, climbing;
        public Player(double x, double y) : base(x, y, 8, 11) { }
    }

    public struct Trail { public double x, y, life; public int phase; }
    public struct Particle { public double x, y, vx, vy, life; public int phase; }

    // Native pixels, Y down. Timing and operation order follow PHASE's script#engine.
    public sealed class PhaseController
    {
        public const double Dt = 1.0 / 60;
        public readonly List<Block> blocks;
        public readonly List<Spike> spikes;
        public readonly List<Laser> lasers;
        public readonly HashSet<int> blocked = new HashSet<int>();
        public readonly List<Trail> trails = new List<Trail>();
        public readonly List<Particle> particles = new List<Particle>();
        readonly double spawnX, spawnY;
        readonly MovingPlatform platformTemplate;
        readonly ClimbGear[] gearTemplates;
        readonly Random random = new Random(1);
        public Player player;
        public MovingPlatform mover;
        public ClimbGear[] gears;
        public int phase, jumps, dashes, deaths;
        public double time, flash, freeze, respawn;
        public bool exitLeft, exitRight;
        public int Width { get; private set; } = 320;
        public int ExitDirection => respawn > 0 ? 0 : exitLeft && player.x < 0 ? -1 : exitRight && player.x + player.w > Width ? 1 : 0;
        bool WithinSides(double x, double w) => (exitLeft || x >= 0) && (exitRight || x + w <= Width);

        public void ResizeRoom(int width)
        {
            width = Math.Max(320, width);
            if (width == Width) return;
            foreach (var block in blocks) if (block.x + block.w == Width) block.w += width - Width;
            Width = width;
            player.x = Math.Min(player.x, Width - player.w);
        }

        public void EnterFrom(PhaseController previous, int direction)
        {
            player = previous.player; phase = previous.phase; time = previous.time;
            jumps = previous.jumps; dashes = previous.dashes; deaths = previous.deaths;
            freeze = previous.freeze; flash = previous.flash;
            player.x = direction > 0 ? 0 : Width - player.w;
            player.wall = player.hopX = player.forceX = 0;
            player.wallLock = player.retention = player.slide = 0;
            player.climbing = player.hopMover = player.dashRide = false;
            player.grounded = player.vy >= 0 && OnGround(player, Solid());
        }

        public PhaseController(IEnumerable<Block> blocks, double spawnX, double spawnY, MovingPlatform platform = null, IEnumerable<ClimbGear> gears = null, IEnumerable<Spike> spikes = null, IEnumerable<Laser> lasers = null)
        {
            this.blocks = blocks.ToList(); this.spawnX = spawnX; this.spawnY = spawnY;
            this.spikes = spikes?.ToList() ?? new List<Spike>();
            this.lasers = lasers?.ToList() ?? new List<Laser>();
            platformTemplate = platform; gearTemplates = gears?.Select(g => g.Fresh()).ToArray() ?? Array.Empty<ClimbGear>(); Reset();
        }
        public void Reset()
        {
            phase = 0; respawn = freeze = 0; blocked.Clear();
            player = new Player(spawnX, spawnY); mover = platformTemplate?.Fresh();
            gears = gearTemplates.Select(g => g.Fresh()).ToArray();
            trails.Clear(); flash = .08;
        }
        public static bool Overlap(Block a, Block b, double x = 0, double y = 0) =>
            a.x + x < b.x + b.w && a.x + a.w + x > b.x && a.y + y < b.y + b.h && a.y + a.h + y > b.y;
        public List<Block> Solid()
        {
            var result = Walls(); if (mover != null) result.Add(mover);
            foreach (var gear in gears) { result.Add(gear.wall); result.Add(gear.platform); }
            return result;
        }
        List<Block> Walls() => blocks.Where((b, i) => b.ActiveInPhase(phase) && !blocked.Contains(i)).ToList();
        bool Occupied(Block b) => Overlap(player,b) || (mover != null && Overlap(mover,b)) || gears.Any(g => Overlap(g.wall,b) || Overlap(g.platform,b));
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
                if (!spike.ActiveInPhase(phase)) continue;
                if (HitsSweep(spike.Hitbox, from)) { Kill(); return true; }
            }
            return false;
        }
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
        public Block LaserBeam(Laser laser) => laser.Beam(Solid(), Width);
        public bool CheckHazards(Block from = null)
        {
            if (CheckSpikes(from)) return true;
            if (lasers.Count == 0) return false;
            var solids = Solid();
            foreach (var laser in lasers)
                if (laser.ActiveInPhase(phase) && HitsSweep(laser.Beam(solids, Width), from ?? player))
                { Kill(); return true; }
            return false;
        }
        void Shift()
        {
            int previous = phase; phase = (phase + 1) % 3; flash = .08;
            for (int i = 0; i < blocks.Count; i++) if (!blocks[i].ActiveInPhase(previous) && blocks[i].ActiveInPhase(phase) && Occupied(blocks[i])) blocked.Add(i);
            Burst(player.x + 4, player.y + 5, phase);
            CheckHazards();
        }
        static bool Duck(Player p, List<Block> solids, bool value)
        {
            if (p.ducking == value) return true;
            double h = value ? 6 : 11, y = p.y + p.h - h;
            if (!value && solids.Any(b => Overlap(new Block(p.x, y, p.w, h), b))) return false;
            p.y = y; p.h = h; p.ducking = value; return true;
        }
        void Jump(int wall, Controls input)
        {
            var p = player;
            bool upDash = p.dashAttack > 0 && p.dx == 0 && p.dy == -1;
            bool climbJump = wall != 0 && p.dashTime <= 0 && p.stamina > 0 && input.x != -wall && (p.climbing || (input.grab && p.facing == wall));
            double speed = -105, variable = .2;
            bool crouched = p.ducking;
            if ((wall != 0 || (p.dashTime > 0 && p.dy == 0)) && !Duck(p, Solid(), false)) return;
            if (p.climbing) p.retention = 0;
            if (p.climbing || p.dashTime > 0) p.maxFall = 160;
            p.wallBoost = 0; p.autoJump = false;
            if (wall != 0 && !climbJump)
            {
                p.vx = -wall * (upDash ? 170 : 130); p.facing = -wall;
                if (upDash) { speed = -160; variable = .25; }
                else if (input.x != 0) { p.forceX = -wall; p.wallLock = .16; }
            }
            else
            {
                if (wall == 0 && p.dashTime > 0 && p.dy == 0)
                { p.vx = p.facing * 260; if (crouched) { p.vx *= 1.25; speed *= .5; } }
                else p.vx += input.x * 40;
                if (climbJump)
                { if (!p.grounded) p.stamina -= 27.5; if (input.x == 0) { p.wallBoost = .2; p.boostDir = -wall; } }
            }
            p.dashTime = p.dashAttack = 0; p.dashPending = false; p.vy = p.varSpeed = speed; p.varTime = variable;
            p.grounded = false; p.coyote = p.buffer = 0; p.climbing = false; p.slide = p.hopX = 0;
            p.hopMover = p.dashRide = false; jumps++; Shift(); Burst(p.x + 4, p.y + 11, -1, 4);
        }
        static bool SlipCheck(Player p, List<Block> solids, double addY = 0)
        {
            double x = p.facing > 0 ? p.x + p.w : p.x - 1, y = p.y + 4 + addY;
            return !solids.Any(b => new[] { y, y - 4 + addY }.Any(at => x >= b.x && x < b.x + b.w && at >= b.y && at < b.y + b.h));
        }
        void ClimbHop(List<Block> solids)
        {
            var p = player; bool touching = Collides(p, solids, p.facing);
            p.hopX = touching ? p.facing : 0; p.hopMover = mover != null && Collides(p, new List<Block> { mover }, p.facing);
            p.vx = touching ? 0 : p.facing * 100; p.vy = Math.Min(p.vy, -120); p.forceX = 0; p.wallLock = .2;
            p.climbing = false; p.retention = 0; p.maxFall = 160;
        }
        void ActivatePlatform()
        {
            var m = mover; if (m == null) return;
            if (!m.returns) { m.pending++; return; }
            m.fromX = m.x; m.fromY = m.y; m.index = 1; m.elapsed = 0; m.hold = .2;
            m.duration = .2 * Math.Abs(m.nodes[1][0] - m.x) / Math.Abs(m.nodes[1][0] - m.nodes[0][0]); m.moving = m.duration > 1e-9;
        }
        double MovePlatform(Controls input, double dt)
        {
            var m = mover; if (m == null) return 0;
            if (m.returns)
            {
                if (!m.moving && m.index == 1)
                {
                    m.hold = Math.Max(0, m.hold - dt); if (m.hold > 1e-9) return 0;
                    m.fromX = m.x; m.fromY = m.y; m.index = 0; m.elapsed = 0;
                    m.duration = 1.2 * Math.Abs(m.x - m.nodes[0][0]) / Math.Abs(m.nodes[1][0] - m.nodes[0][0]); m.moving = m.duration > 1e-9;
                }
            }
            else if (!m.moving && m.pending > 0)
            { m.pending--; m.fromX = m.x; m.fromY = m.y; m.index = (m.index + 1) % m.nodes.Length; m.elapsed = 0; m.duration = .3; m.moving = true; }
            if (!m.moving) return 0;
            m.elapsed = Math.Min(m.duration, m.elapsed + dt); double t = m.elapsed / m.duration;
            double dx = PixelRound(m.fromX + (m.nodes[m.index][0] - m.fromX) * t) - m.x;
            double dy = PixelRound(m.fromY + (m.nodes[m.index][1] - m.fromY) * t) - m.y;
            var p = player;
            bool rider = p.vy >= 0 && Math.Abs(p.y + p.h - m.y) < .6 && p.x + p.w > m.x && p.x < m.x + m.w;
            bool clinger = (p.climbing && input.grab && p.stamina > 0 && p.dashTime <= 0 && Collides(p, new List<Block> { m }, p.facing)) || (p.hopMover && p.wallLock > 0);
            int steps = Math.Max(1, (int)Math.Ceiling(Math.Max(Math.Abs(dx), Math.Abs(dy)))); var walls = Walls();
            foreach (var gear in gears) { walls.Add(gear.wall); walls.Add(gear.platform); }
            for (int i = 0; i < steps; i++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                m.x += dx / steps; m.y += dy / steps;
                if (rider || clinger) { p.x += dx / steps; p.y += dy / steps; }
                else if (Overlap(p, m))
                { if (dx > 0) p.x = m.x + m.w; else if (dx < 0) p.x = m.x - p.w; else if (dy > 0) p.y = m.y + m.h; else if (dy < 0) p.y = m.y - p.h; }
                if (CheckHazards(before) || walls.Any(b => Overlap(p, b)) || !WithinSides(p.x, p.w)) { Kill(); break; }
            }
            if (t >= 1) { m.moving = false; if (m.returns && m.index == 1) m.hold = .2; }
            return dx;
        }
        bool MoveGearBlock(Block block, int dx, int dy, Controls input)
        {
            var p = player;
            bool rider = p.vy >= 0 && OnGround(p, new List<Block> { block });
            bool clinger = p.climbing && input.grab && Collides(p, new List<Block> { block }, p.facing);
            var obstacles = Solid().Where(b => b != block).ToList();
            int steps = Math.Max(Math.Abs(dx), Math.Abs(dy));
            for (int i = 0; i < steps; i++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                block.x += (double)dx / steps; block.y += (double)dy / steps;
                if (obstacles.Any(b => Overlap(block, b)) || !WithinSides(block.x, block.w)) return false;
                if (rider || clinger) { p.x += (double)dx / steps; p.y += (double)dy / steps; }
                else if (Overlap(p, block))
                {
                    if (dx > 0) p.x = block.x + block.w; else if (dx < 0) p.x = block.x - p.w;
                    else if (dy > 0) p.y = block.y + block.h; else p.y = block.y - p.h;
                }
                if (CheckHazards(before) || obstacles.Any(b => Overlap(p, b)) || !WithinSides(p.x, p.w) || p.y < 0) return false;
            }
            return true;
        }
        void DriveGear(ClimbGear gear, int climb, Controls input)
        {
            for (int i = 0; i < Math.Abs(climb); i++)
            {
                int dy = Math.Sign(climb);
                double y = gear.wall.y + dy;
                if (y < gear.top || y > gear.bottom) break;
                double wallY = gear.wall.y, platformX = gear.platform.x, px = player.x, py = player.y;
                int dx = PixelRound(gear.startX + (gear.endX - gear.startX) * (gear.bottom - y) / (gear.bottom - gear.top)) - (int)gear.platform.x;
                if (MoveGearBlock(gear.wall, 0, dy, input) && MoveGearBlock(gear.platform, dx, 0, input)) continue;
                gear.wall.y = wallY; gear.platform.x = platformX; player.x = px; player.y = py;
                break;
            }
        }
        public void Step(Controls input, double dt = Dt)
        {
            time += dt; flash = Math.Max(0, flash - dt);
            for (int i = particles.Count - 1; i >= 0; i--)
            { var q = particles[i]; q.x += q.vx * dt; q.y += q.vy * dt; q.vy += 67 * dt; q.life -= dt; if (q.life <= 0) particles.RemoveAt(i); else particles[i] = q; }
            for (int i = trails.Count - 1; i >= 0; i--)
            { var q = trails[i]; q.life -= dt; if (q.life <= 0) trails.RemoveAt(i); else trails[i] = q; }
            if (respawn > 0) { respawn = Math.Max(0, respawn - dt); if (respawn == 0) Reset(); return; }
            var p = player; if (CheckHazards()) return;
            p.buffer = input.jump ? .08 : input.jumpHeld ? Math.Max(0, p.buffer - dt) : 0;
            p.dashBuffer = input.dash ? .08 : input.dashHeld ? Math.Max(0, p.dashBuffer - dt) : 0;
            if (freeze > 1e-9) { freeze = Math.Max(0, freeze - dt); return; }
            blocked.RemoveWhere(i => !Occupied(blocks[i]));
            double platformDX = MovePlatform(input, dt); if (respawn > 0) return;
            p.dashAttack = Math.Max(0, p.dashAttack - dt);
            var solids = Solid(); int contact = WallAt(p, solids, input.x != 0 ? input.x : p.facing, p.dashAttack > 0 && p.dx == 0 && p.dy == -1 ? 5 : 3);
            p.wall = contact; p.grounded = p.vy >= 0 && OnGround(p, solids);
            int move = p.wallLock > 0 ? p.forceX : input.x;
            p.coyote = p.grounded ? .1 : Math.Max(0, (double)((float)p.coyote - (float)dt)); if (p.coyote < 1e-8) p.coyote = 0;
            p.wallLock = Math.Max(0, p.wallLock - dt); if (p.wallLock == 0) p.hopMover = false;
            p.dashCooldown = Math.Max(0, p.dashCooldown - dt); p.varTime = Math.Max(0, p.varTime - dt); p.wallBoost = Math.Max(0, p.wallBoost - dt);
            bool refillReady = p.dashRefill <= 0; p.dashRefill = Math.Max(0, p.dashRefill - dt);
            if (p.grounded) { if (!p.climbing) p.autoJump = false; p.stamina = 110; p.slide = 0; if (refillReady && !p.dashPending) p.dash = 1; }
            if (p.wallBoost > 0 && input.x == p.boostDir) { p.vx = 130 * input.x; p.stamina = Math.Min(110, p.stamina + 27.5); p.wallBoost = 0; }
            if (p.retention > 0)
            {
                if (Math.Sign(p.vx) == -Math.Sign(p.retained)) p.retention = 0;
                else if (WallAt(p, solids, Math.Sign(p.retained), 1) == 0) { p.vx = p.retained; p.retention = 0; }
                else p.retention = Math.Max(0, p.retention - dt);
            }
            if (p.hopX != 0)
            { if (Math.Sign(p.vx) == -p.hopX || p.vy > 0) p.hopX = 0; else if (WallAt(p, solids, p.hopX, 1) == 0) { p.vx = p.hopX * 100; p.hopX = 0; } }
            if (move != 0 && !p.climbing && p.dashTime <= 0) p.facing = move;
            bool enteredClimb = false;
            if (!p.climbing && p.dashTime <= 0 && input.grab && p.stamina + (p.wallBoost > 0 ? 27.5 : 0) >= 20 && !p.ducking && p.vy >= 0 && Math.Sign(p.vx) != -p.facing)
            {
                int correction = -1;
                for (int y = 0; y <= 2; y++)
                { if (y != 0 && input.y == 1) break; if (!Collides(p, solids, 0, -y) && Collides(p, solids, p.facing * 2, -y)) { correction = y; break; } }
                if (correction >= 0)
                {
                    enteredClimb = true; p.y -= correction; p.climbing = true; p.autoJump = false; p.vx = 0; p.vy *= .2; p.climbWait = .1;
                    p.climbMove = 0; p.wallBoost = p.slide = p.retention = p.hopX = 0; p.hopMover = false;
                    for (int i = 0; i < 2 && !Collides(p, solids, p.facing); i++) p.x += p.facing;
                }
            }
            if (!enteredClimb && p.dashBuffer > 0 && !(p.climbing && p.buffer > 0) && p.dash != 0 && p.dashTime <= 0 && p.dashCooldown <= 0)
            {
                if (p.climbing) p.retention = 0;
                p.dashStartedOnGround = p.grounded; p.beforeDash = p.vx; p.vx = p.vy = p.dx = p.dy = 0;
                p.dashTime = .15 + 2 * dt; p.dashCooldown = .2; p.dashRefill = .1; p.dashAttack = .3;
                p.dashBuffer = p.dash = 0; p.varTime = 0; p.climbing = p.hopMover = false; p.wallLock = p.wallBoost = 0;
                p.dashPending = true; p.dashRide = false; p.slide = 0; dashes++; ActivatePlatform(); freeze = .05; return;
            }
            if (p.dashTime > 0)
            {
                if (p.buffer > 0 && (contact != 0 || (p.coyote > 0 && p.dy == 0))) Jump(p.coyote > 0 && p.dy == 0 ? 0 : contact, input);
                else if ((int)Math.Floor(time / Dt) % 3 == 0) trails.Add(new Trail { x = p.x, y = p.y, life = .16, phase = phase });
            }
            else if (p.climbing)
            {
                if (!enteredClimb)
                {
                    p.climbWait -= dt;
                    if (p.buffer > 0) Jump(p.facing, input);
                    else if (!input.grab) { p.climbing = false; p.retention = 0; p.maxFall = 160; }
                    else if (!Collides(p, solids, p.facing)) { if (p.vy < 0) ClimbHop(solids); else { p.climbing = false; p.retention = 0; p.maxFall = 160; } }
                    else
                    {
                        double target = 0; bool trySlip = p.climbWait > 0;
                        if (p.climbWait <= 0)
                        {
                            if (input.y < 0)
                            {
                                target = -45;
                                if (Collides(p, solids, 0, -1) || (Collides(p, solids, 0, -6) && SlipCheck(p, solids, -1)))
                                { if (p.vy < 0) p.vy = 0; target = 0; trySlip = true; }
                                else if (SlipCheck(p, solids)) ClimbHop(solids);
                            }
                            else if (input.y > 0) { target = 80; if (p.grounded) { if (p.vy > 0) p.vy = 0; target = 0; } }
                            else trySlip = true;
                        }
                        if (p.climbing)
                        {
                            p.climbMove = Math.Sign(target); if (trySlip && SlipCheck(p, solids)) target = 30;
                            p.vx = 0; p.vy = Approach(p.vy, target, 900 * dt);
                            if (input.y != 1 && p.vy > 0 && !Collides(p, solids, p.facing, 1)) p.vy = 0;
                            if (p.climbWait <= 0) p.stamina -= (p.climbMove < 0 ? 100 / 2.2 : p.climbMove == 0 ? 10 : 0) * dt;
                            if (p.stamina <= 0) { p.climbing = false; p.retention = 0; p.maxFall = 160; }
                        }
                    }
                }
            }
            else
            {
                if (p.grounded) Duck(p, solids, input.y == 1);
                double mult = p.grounded ? 1 : .65, acceleration = Math.Abs(p.vx) > 90 && Math.Sign(p.vx) == move ? 400 : 1000;
                p.vx = p.ducking && p.grounded ? Approach(p.vx, 0, 500 * dt) : Approach(p.vx, move * 90, acceleration * mult * dt);
                if (move != 0) p.facing = move;
                p.maxFall = Approach(p.maxFall, input.y > 0 && p.vy >= 160 ? 240 : 160, 300 * dt); double maxFall = p.maxFall;
                int slide = WallAt(p, solids, p.facing, 1);
                if (slide == p.facing && (move == p.facing || (move == 0 && input.grab)) && p.vy >= 0 && input.y != 1 && p.slide < 1.2)
                { maxFall = 20 + (160 - 20) * p.slide / 1.2; p.slide += dt; }
                if (slide == p.facing && input.grab && p.stamina <= 0 && (move == 0 || move == p.facing) && p.vy >= 0 && input.y != 1)
                    maxFall = Math.Min(maxFall, 30);
                if (!p.grounded) { double gravity = Math.Abs(p.vy) < 40 && (input.jumpHeld || p.autoJump) ? 450 : 900; p.vy = Approach(p.vy, maxFall, gravity * dt); }
                if (p.varTime > 1e-9) { if (input.jumpHeld) p.vy = Math.Min(p.vy, p.varSpeed); else p.varTime = 0; }
                if (p.buffer > 0 && (p.coyote > 0 || contact != 0)) { var jumpInput = input; jumpInput.x = move; Jump(p.coyote > 0 ? 0 : contact, jumpInput); }
            }
            if (respawn > 0 || CheckHazards()) return;
            if (p.dashPending)
            {
                int dx = input.x, dy = input.y; if (dx == 0 && dy == 0) dx = p.facing;
                double length = Math.Sqrt(dx * dx + dy * dy); p.dx = dx / length; p.dy = dy / length; p.vx = p.dx * 240; p.vy = p.dy * 240;
                if (Math.Sign(p.beforeDash) == Math.Sign(p.vx) && Math.Abs(p.beforeDash) > Math.Abs(p.vx)) p.vx = p.beforeDash;
                if (dx != 0) p.facing = Math.Sign(dx);
                if (p.grounded && p.dx != 0 && p.dy > 0) { p.dx = Math.Sign(p.dx); p.dy = p.vy = 0; p.vx *= 1.2; Duck(p, solids, true); }
                p.dashPending = false;
            }
            bool onMover = mover != null && p.grounded && p.y + p.h == mover.y && p.x + p.w > mover.x && p.x < mover.x + mover.w;
            p.dashRide = p.dashTime > 0 && p.dy == 0 && p.dx != 0 && onMover && (Math.Sign(platformDX) == Math.Sign(p.dx) || (p.dashRide && platformDX == 0 && !mover.moving));
            if (p.dashTime > 0)
            {
                p.dashTime = Math.Max(0, p.dashTime - dt);
                if (p.dashTime < 1e-9) { p.dashTime = 0; p.maxFall = 160; p.autoJump = true; if (p.dy <= 0) { p.vx = p.dx * 160; p.vy = p.dy * 160 * (p.dy < 0 ? .75 : 1); } }
            }
            if (p.vy > 0 && !p.grounded && p.ducking) Duck(p, Solid(), false);
            p.remX += (p.dashRide ? 0 : p.vx) * dt; int moveX = PixelRound(p.remX); p.remX -= moveX;
            bool hitX = false;
            for (int n = 0; n < Math.Abs(moveX) && !hitX; n++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                solids = Solid(); int vx = Math.Sign(moveX); p.x += vx;
                foreach (var b in solids) if (Overlap(p, b))
                {
                    p.remX = 0; bool corrected = false;
                    if (p.dashTime > 0 && p.dy == 0)
                        for (int offset = 1; offset <= 4 && !corrected; offset++) foreach (int sign in new[] { 1, -1 })
                            if (!Collides(p, solids, 0, offset * sign)) { p.y += offset * sign; corrected = true; break; }
                    if (!corrected) { p.x = vx > 0 ? b.x - p.w : b.x + b.w; if (p.retention <= 0) { p.retained = p.vx; p.retention = .06; } p.vx = 0; p.dashAttack = 0; hitX = true; }
                }
                if (!exitLeft) p.x = Math.Max(0, p.x);
                if (!exitRight) p.x = Math.Min(Width - p.w, p.x);
                if (CheckHazards(before)) return;
            }
            var drivenGear = p.climbing && input.grab && input.y != 0 && p.climbWait <= 0 && p.climbMove == input.y
                ? gears.FirstOrDefault(g => Collides(p, new List<Block> { g.wall }, p.facing)) : null;
            double climbY = p.y;
            p.remY += p.vy * dt; int moveY = PixelRound(p.remY); p.remY -= moveY;
            bool hitY = false;
            for (int n = 0; n < Math.Abs(moveY) && !hitY; n++)
            {
                var before = new Block(p.x,p.y,p.w,p.h);
                solids = Solid(); int vy = Math.Sign(moveY); p.y += vy;
                foreach (var b in solids) if (Overlap(p, b))
                {
                    p.remY = 0; bool corrected = false;
                    if ((vy > 0 && p.dashTime > 0 && !p.dashStartedOnGround) || vy < 0)
                        for (int offset = 1; offset <= 4 && !corrected; offset++) foreach (int sign in new[] { -1, 1 })
                        {
                            if (p.vx * sign < 0) continue;
                            double x = p.x + offset * sign;
                            if (WithinSides(x, p.w) && !Collides(p, solids, offset * sign)) { p.x = x; corrected = true; break; }
                        }
                    if (!corrected)
                    {
                        p.y = vy > 0 ? b.y - p.h : b.y + b.h;
                        if (vy > 0 && p.dashTime > 0 && p.dx != 0 && p.dy > 0) { p.dx = Math.Sign(p.dx); p.dy = 0; p.vx *= 1.2; Duck(p, solids, true); }
                        p.vy = p.dashAttack = 0; hitY = true; if (vy > 0 || p.varTime < .2 - .05) p.varTime = 0;
                    }
                }
                if (CheckHazards(before)) return;
            }
            if (drivenGear != null && Math.Sign(p.y - climbY) == input.y) DriveGear(drivenGear, PixelRound(p.y - climbY), input);
            if (p.y < 0) { p.y = 0; p.vy = Math.Max(0, p.vy); p.varTime = p.remY = p.dashAttack = 0; }
            solids = Solid(); p.grounded = p.vy >= 0 && OnGround(p, solids); p.wall = WallAt(p, solids, input.x != 0 ? input.x : p.facing);
            if (p.grounded && p.dashRefill <= 0 && p.dashTime <= 0) p.dash = 1;
            if (p.y > 180 + 14) Kill();
        }
    }
}
