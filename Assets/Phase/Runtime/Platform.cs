using System;
using System.Collections.Generic;
using System.Linq;

namespace Phase
{
    // A jump platform: one cell thick, odd length, centred on its pivot cell. Every jump queues one step to the next pivot.
    public sealed class Platform : Block
    {
        public const double SlideTime = .3;
        public readonly bool vertical, reverse;
        public readonly int length;
        // Centre cells, in the order the platform visits them; the path closes back to the first.
        public readonly (int x, int y)[] pivots;
        // Spike rows beside the body, sharing its color and moving with it.
        public readonly Spike[] attached;
        readonly (double dx, double dy)[] offsets;
        public int pivot, queued;
        int target = -1;
        double elapsed;

        public Platform(bool vertical, int length, IEnumerable<(int x, int y)> pivots, bool reverse = false, int phaseMask = 7, bool spiked = false, IEnumerable<SpikeDirection> sides = null)
            : base(0, 0, vertical ? 8 : length * 8, vertical ? length * 8 : 8, phaseMask: phaseMask)
        {
            if (length < 1 || length % 2 == 0) throw new ArgumentOutOfRangeException(nameof(length), "Platform length must be odd.");
            if (phaseMask < 1) throw new ArgumentOutOfRangeException(nameof(phaseMask));
            this.pivots = pivots?.ToArray();
            if (this.pivots == null || this.pivots.Length < 2) throw new ArgumentException("A platform needs at least two pivots.", nameof(pivots));
            this.vertical = vertical; this.length = length; this.reverse = reverse; this.spiked = spiked;
            attached = (sides ?? Array.Empty<SpikeDirection>()).Distinct().Select(Row).ToArray();
            var home = OriginAt(0);
            offsets = attached.Select(s => (s.x - home.x, s.y - home.y)).ToArray();
            Restart();
        }
        Spike Row(SpikeDirection side)
        {
            var at = OriginAt(0); bool across = side == SpikeDirection.Up || side == SpikeDirection.Down;
            return new Spike(side == SpikeDirection.Left ? at.x - 8 : side == SpikeDirection.Right ? at.x + w : at.x,
                side == SpikeDirection.Up ? at.y - 8 : side == SpikeDirection.Down ? at.y + h : at.y, across ? w : h, side, Mask);
        }
        public (double x, double y) OriginAt(int i)
        {
            int half = length / 2;
            return ((pivots[i].x - (vertical ? 0 : half)) * 8, (pivots[i].y - (vertical ? half : 0)) * 8);
        }
        public int Next => ((pivot + (reverse ? -1 : 1)) % pivots.Length + pivots.Length) % pivots.Length;
        public void Restart()
        {
            pivot = queued = 0; target = -1; elapsed = 0;
            var at = OriginAt(0); x = at.x; y = at.y;
            for (int i = 0; i < attached.Length; i++) { attached[i].x = x + offsets[i].dx; attached[i].y = y + offsets[i].dy; }
        }
        public void Move(int dx, int dy)
        {
            x += dx; y += dy;
            foreach (var spike in attached) { spike.x += dx; spike.y += dy; }
        }
        // Slides toward the next queued pivot; returns the whole pixels still to travel this step.
        public (int dx, int dy) Advance(double dt)
        {
            if (target < 0)
            {
                if (queued == 0) return (0, 0);
                queued--; target = Next; elapsed = 0;
            }
            elapsed += dt;
            double t = Math.Min(1, elapsed / SlideTime), eased = t * t * (3 - 2 * t);
            var from = OriginAt(pivot); var to = OriginAt(target);
            double nx = t >= 1 ? to.x : Math.Round(from.x + (to.x - from.x) * eased, MidpointRounding.ToEven);
            double ny = t >= 1 ? to.y : Math.Round(from.y + (to.y - from.y) * eased, MidpointRounding.ToEven);
            if (t >= 1) { pivot = target; target = -1; }
            return ((int)(nx - x), (int)(ny - y));
        }
    }
}
