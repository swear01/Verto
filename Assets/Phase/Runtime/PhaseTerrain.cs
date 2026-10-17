using UnityEngine;

namespace Phase
{
    public sealed class PhaseTerrain : MonoBehaviour
    {
        public Vector2Int size = new Vector2Int(8, 8);
        [Range(-1, 2)] public int phase = -1;
        [Range(0, 7)] public int phaseMask;
        public Block CreateBlock() => PhaseGrid.CreateBox(this, transform.position, size, phase, phaseMask);
        void OnValidate() { size.x = Mathf.Max(8, Mathf.RoundToInt(size.x / 8f) * 8); size.y = Mathf.Max(8, Mathf.RoundToInt(size.y / 8f) * 8); }
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask == 0 ? (phase < 0 ? 7 : 1 << phase) : phaseMask);
            Gizmos.DrawWireCube(transform.position + new Vector3(size.x / 2f, -size.y / 2f), new Vector3(size.x, size.y, 0));
        }
    }
}
