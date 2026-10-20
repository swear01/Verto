using System;
using System.Collections.Generic;

namespace Phase
{
    public enum LaserDirection { Up, Down, Left, Right }

    public sealed class Laser : Block
    {
        public readonly LaserDirection direction;
        public Laser(double x, double y, LaserDirection direction, int phaseMask = 7)
            : base(x, y, 8, 8, phaseMask: phaseMask)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            if ((int)direction < 0 || (int)direction > 3) throw new ArgumentOutOfRangeException(nameof(direction));
            if (phaseMask == 0) throw new ArgumentOutOfRangeException(nameof(phaseMask));
            this.direction = direction;
        }

        public Block Beam(IEnumerable<Block> solids, int width, int height = 180)
        {
            bool horizontal = direction == LaserDirection.Left || direction == LaserDirection.Right;
            bool positive = direction == LaserDirection.Right || direction == LaserDirection.Down;
            double origin = horizontal ? (positive ? x + 8 : x) : (positive ? y + 8 : y);
            double across = horizontal ? y + 2 : x + 2;
            double limit = horizontal ? width : height, crossLimit = horizontal ? height : width;
            if (origin < 0 || origin > limit || across < 0 || across + 4 > crossLimit)
                return new Block(x, y, 0, 0);
            double end = positive ? limit : 0;
            foreach (var solid in solids)
            {
                double lo = horizontal ? solid.x : solid.y, hi = lo + (horizontal ? solid.w : solid.h);
                double cross = horizontal ? solid.y : solid.x, size = horizontal ? solid.h : solid.w;
                if (across + 4 <= cross || across >= cross + size) continue;
                if (positive && hi > origin) end = Math.Min(end, Math.Max(origin, lo));
                if (!positive && lo < origin) end = Math.Max(end, Math.Min(origin, hi));
            }
            double start = Math.Min(origin, end), length = Math.Abs(end - origin);
            return horizontal ? new Block(start, across, length, 4) : new Block(across, start, 4, length);
        }
    }
}
