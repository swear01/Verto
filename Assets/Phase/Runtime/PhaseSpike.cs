using UnityEngine;

namespace Phase
{
    public sealed class PhaseSpike : MonoBehaviour
    {
        [Min(1)] public int length = 1;
        public SpikeDirection direction;
        [Range(1,7)] public int phaseMask = 7;
        public Spike CreateSpike() => new Spike(Mathf.Round(transform.position.x),-Mathf.Round(transform.position.y),length*8,direction,phaseMask);
        void OnValidate() { length = Mathf.Max(1,length); }
        void OnDrawGizmos()
        {
            var s = CreateSpike(); var b = s.Hitbox;
            Gizmos.color = PhasePixels.SurfaceColor(s.Mask);
            Gizmos.DrawWireCube(new Vector3((float)(b.x+b.w/2),-(float)(b.y+b.h/2)),new Vector3((float)b.w,(float)b.h));
        }
    }
}
