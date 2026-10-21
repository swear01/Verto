using System;

namespace Phase
{
    // A jump crystal: touching it in an active phase stores one air jump until the player lands.
    public sealed class Crystal : Block
    {
        public bool collected;
        public Crystal(double x, double y, int phaseMask = 7)
            : base(x, y, 8, 8, phaseMask: phaseMask)
        {
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(y) || double.IsInfinity(y))
                throw new ArgumentOutOfRangeException(nameof(x));
            if (phaseMask == 0) throw new ArgumentOutOfRangeException(nameof(phaseMask));
        }
    }
}
