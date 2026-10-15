using UnityEngine;

namespace Phase
{
    public sealed class PhaseTerrain : MonoBehaviour
    {
        public Vector2Int size = new Vector2Int(8, 8);
        [Range(-1, 2)] public int phase = -1;
        public Block CreateBlock() => new Block(Mathf.Round(transform.position.x), -Mathf.Round(transform.position.y), size.x, size.y, phase);
        void OnValidate() { size.x = Mathf.Max(8, size.x / 8 * 8); size.y = Mathf.Max(8, size.y / 8 * 8); }
        void OnDrawGizmos()
        {
            Gizmos.color = phase < 0 ? Color.gray : PhasePixels.Hex(PhasePixels.Highlights[phase]);
            Gizmos.DrawWireCube(transform.position + new Vector3(size.x / 2f, -size.y / 2f), new Vector3(size.x, size.y, 0));
        }
    }
}
