using System;

namespace Phase
{
    public enum CrystalKind { Jump, Dash }

    public sealed class Crystal : Block
    {
        public readonly CrystalKind kind;
        public bool collected;
        public Crystal(double x, double y, CrystalKind kind, int phaseMask = 7)
            : base(x, y, 8, 8, phaseMask: phaseMask)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            if ((int)kind < 0 || (int)kind > 1) throw new ArgumentOutOfRangeException(nameof(kind));
            if (phaseMask == 0) throw new ArgumentOutOfRangeException(nameof(phaseMask));
            this.kind = kind;
        }
    }
}
