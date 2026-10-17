using UnityEngine;

namespace Phase
{
    public sealed class PhaseClimbGear : MonoBehaviour
    {
        public Vector2Int wallSize = new Vector2Int(8, 48);
        public int wallTop = 56;
        public Vector2Int platformStart = new Vector2Int(104, 104);
        public Vector2Int platformSize = new Vector2Int(32, 8);
        public int platformEndX = 160;
        public ClimbGear CreateGear() => new ClimbGear(
            new Block(Mathf.Round(transform.position.x), -Mathf.Round(transform.position.y), wallSize.x, wallSize.y), wallTop,
            new Block(platformStart.x, platformStart.y, platformSize.x, platformSize.y), platformEndX);
        void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + new Vector3(wallSize.x / 2f, -wallSize.y / 2f), new Vector3(wallSize.x, wallSize.y));
            Gizmos.DrawWireCube(new Vector3(platformStart.x + platformSize.x / 2f, -platformStart.y - platformSize.y / 2f), new Vector3(platformSize.x, platformSize.y));
            Gizmos.DrawLine(new Vector3(transform.position.x, -wallTop), transform.position);
        }
    }
}
