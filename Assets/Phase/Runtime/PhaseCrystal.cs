using UnityEngine;

namespace Phase
{
    public sealed class PhaseCrystal : MonoBehaviour
    {
        [Range(1, 7)] public int phaseMask = 7;
        public Crystal CreateCrystal()
        {
            var box = PhaseGrid.CreateBox(this, transform.position, new Vector2Int(8, 8), mask: phaseMask);
            return new Crystal(box.x, box.y, phaseMask);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask);
            Gizmos.DrawWireSphere(transform.position + new Vector3(4, -4), 4);
        }
    }
}
