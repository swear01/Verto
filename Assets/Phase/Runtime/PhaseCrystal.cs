using UnityEngine;

namespace Phase
{
    public sealed class PhaseCrystal : MonoBehaviour
    {
        public CrystalKind kind;
        [Range(1, 7)] public int phaseMask = 7;
        public Crystal CreateCrystal()
        {
            var box = PhaseGrid.CreateBox(this, transform.position, new Vector2Int(8, 8), mask: phaseMask);
            return new Crystal(box.x, box.y, kind, phaseMask);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask);
            var center = transform.position + new Vector3(4, -4);
            if (kind == CrystalKind.Jump) Gizmos.DrawWireSphere(center, 4);
            else Gizmos.DrawWireCube(center, new Vector3(8, 8));
        }
    }
}
