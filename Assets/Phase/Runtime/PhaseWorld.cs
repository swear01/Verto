using System;
using System.Linq;

namespace Phase
{
    // Connected rooms on a grid of screens. A room's content is room-local, like a single custom map.
    public sealed class PhaseWorld
    {
        public const int ScreenWidth = 320, ScreenHeight = 176, MaxScreensWide = 4, MaxScreensTall = 5, MaxRooms = 256;
        public string name;
        public PhaseDemoRoom[] rooms;

        // The first room by position: left to right, then top to bottom.
        public int Start => Enumerable.Range(0, rooms.Length).OrderBy(i => rooms[i].atX).ThenBy(i => rooms[i].atY).First();

        public static bool Overlaps(PhaseDemoRoom a, PhaseDemoRoom b) =>
            a.atX < b.atX + b.GridWidth / ScreenWidth && a.atX + a.GridWidth / ScreenWidth > b.atX &&
            a.atY < b.atY + b.GridHeight / ScreenHeight && a.atY + a.GridHeight / ScreenHeight > b.atY;

        // A room with its neighbors: the rooms that share an edge with it, as rectangles in its pixels.
        public PhaseController Create(int index)
        {
            var room = rooms[index]; var controller = room.CreateController();
            double w = room.GridWidth, h = room.GridHeight;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (i == index) continue;
                var n = new Neighbor { room = i, x = (rooms[i].atX - room.atX) * (double)ScreenWidth, y = (rooms[i].atY - room.atY) * (double)ScreenHeight,
                    w = rooms[i].GridWidth, h = rooms[i].GridHeight };
                bool beside = (n.x == w || n.x + n.w == 0) && n.y < h && n.y + n.h > 0, above = (n.y == h || n.y + n.h == 0) && n.x < w && n.x + n.w > 0;
                if (beside || above) controller.neighbors.Add(n);
            }
            return controller;
        }
    }

    // Plays a world: one live controller for the current room. Crossing into a neighbor carries the player over
    // and pauses play for a moment while the view slides.
    public sealed class PhaseWorldPlay
    {
        public const double Pause = .18;
        public readonly PhaseWorld world;
        readonly PhaseController[] controllers;
        public int Room { get; private set; }
        public PhaseController Controller => controllers[Room];
        public double pause;
        // The way the last transition moved on screen: -1/1 horizontally, -1 (up) or 1 (down) vertically.
        public int slideX, slideY;
        public bool Won => Controller.won;

        public PhaseWorldPlay(PhaseWorld world, int? start = null)
        {
            this.world = world; controllers = new PhaseController[world.rooms.Length];
            Room = start ?? world.Start; controllers[Room] = world.Create(Room);
        }

        // Advances one step; false while paused. A pending jump stays with the caller until a step runs.
        public bool Step(Controls input, double dt = PhaseController.Dt)
        {
            if (pause > 0) { pause = Math.Max(0, pause - dt); return false; }
            var from = Controller;
            from.Step(input, dt);
            int next = from.ExitRoom;
            if (next >= 0 && !from.won) Enter(next);
            return true;
        }

        void Enter(int next)
        {
            var from = Controller; var n = from.neighbors.First(m => m.room == next);
            var to = controllers[next] ?? (controllers[next] = world.Create(next));
            var p = from.player; double cx = p.x + p.w / 2, cy = p.y + p.h / 2;
            slideX = cx < 0 ? -1 : cx >= from.Width ? 1 : 0;
            slideY = slideX != 0 ? 0 : cy < 0 ? -1 : 1;
            to.EnterRoom(from, -n.x, -n.y);
            // The respawn is the spawn nearest the entry point that is safe in the entry phase; with none, the nearest one in phase 0.
            var spawns = world.rooms[next].Spawns.OrderBy(s => Math.Pow(s.x - to.player.x, 2) + Math.Pow(s.y - to.player.y, 2)).ToList();
            var safe = spawns.Cast<(double x, double y)?>().FirstOrDefault(s => to.SafeSpawn(s.Value.x, s.Value.y, to.phase));
            var spawn = safe ?? spawns[0];
            to.spawnX = spawn.x; to.spawnY = spawn.y; to.respawnPhase = safe != null ? to.phase : 0;
            Room = next; pause = Pause;
        }
    }
}
