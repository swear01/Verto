using UnityEngine;

namespace Phase
{
    public sealed class PhaseClimbGear : MonoBehaviour
    {
        public Vector2Int wallSize = new Vector2Int(8, 56);
        public int wallTop = 80;
        public Vector2Int platformStart = new Vector2Int(88, 80);
        public Vector2Int platformSize = new Vector2Int(48, 8);
        public int platformEndX = 112;
        public ClimbGear CreateGear()
        {
            var wall = PhaseGrid.CreateBox(this, transform.position, wallSize);
            var platform = transform.Find("Platform collider");
            if (platform == null) { platform = new GameObject("Platform collider").transform; platform.SetParent(transform); }
            var surface = PhaseGrid.CreateBox(platform, new Vector3(platformStart.x, -platformStart.y), platformSize);
            var top = PhaseGrid.Snap(this, new Vector3((float)wall.x, -wallTop));
            var end = PhaseGrid.Snap(this, new Vector3(platformEndX, -(float)surface.y));
            return new ClimbGear(wall, -top.y, surface, end.x);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = Color.white;
            Gizmos.DrawWireCube(transform.position + new Vector3(wallSize.x / 2f, -wallSize.y / 2f), new Vector3(wallSize.x, wallSize.y));
            Gizmos.DrawWireCube(new Vector3(platformStart.x + platformSize.x / 2f, -platformStart.y - platformSize.y / 2f), new Vector3(platformSize.x, platformSize.y));
            Gizmos.DrawLine(new Vector3(transform.position.x, -wallTop), transform.position);
        }
    }
}
