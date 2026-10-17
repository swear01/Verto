using UnityEngine;

namespace Phase
{
    public sealed class PhaseCrystal : MonoBehaviour
    {
        public CrystalKind kind;
        [Range(1, 7)] public int phaseMask = 7;
        public Crystal CreateCrystal() => new Crystal(Mathf.Round(transform.position.x), -Mathf.Round(transform.position.y), kind, phaseMask);
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask);
            var center = transform.position + new Vector3(4, -4);
            if (kind == CrystalKind.Jump) Gizmos.DrawWireSphere(center, 4);
            else Gizmos.DrawWireCube(center, new Vector3(8, 8));
        }
    }
}
