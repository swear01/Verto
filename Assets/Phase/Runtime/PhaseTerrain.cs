using UnityEngine;

namespace Phase
{
    public sealed class PhaseTerrain : MonoBehaviour
    {
        public Vector2Int size = new Vector2Int(8, 8);
        [Range(-1, 2)] public int phase = -1;
        [Range(0, 7)] public int phaseMask;
        public Block CreateBlock() => new Block(Mathf.Round(transform.position.x), -Mathf.Round(transform.position.y), size.x, size.y, phase, phaseMask);
        void OnValidate() { size.x = Mathf.Max(8, size.x / 8 * 8); size.y = Mathf.Max(8, size.y / 8 * 8); }
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(CreateBlock().Mask);
            Gizmos.DrawWireCube(transform.position + new Vector3(size.x / 2f, -size.y / 2f), new Vector3(size.x, size.y, 0));
        }
    }
}
