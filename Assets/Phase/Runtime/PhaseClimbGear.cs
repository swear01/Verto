using UnityEngine;

namespace Phase
{
    public sealed class PhaseClimbGear : MonoBehaviour
    {
        public Vector2Int wallSize = new Vector2Int(8, 56);
        public int wallTop = 80;
        public Vector2Int platformStart = new Vector2Int(88, 80);
        public Vector2Int platformSize = new Vector2Int(56, 8);
        public int platformEndX = 112;
        [Range(0, 7)] public int phaseMask;
        public ClimbGear CreateGear()
        {
            var wall = PhaseGrid.CreateBox(this, transform.position, wallSize, mask: phaseMask);
            var platform = transform.Find("Platform collider");
            if (platform == null) { platform = new GameObject("Platform collider").transform; platform.SetParent(transform); }
            var surface = PhaseGrid.CreateBox(platform, ParentPosition(platformStart.x, platformStart.y), platformSize, mask: phaseMask);
            var top = PhaseGrid.Snap(this, new Vector3((float)wall.x, ParentPosition(0, wallTop).y));
            var end = PhaseGrid.Snap(this, new Vector3(ParentPosition(platformEndX, 0).x, -(float)surface.y));
            return new ClimbGear(wall, -top.y, surface, end.x);
        }
        Vector3 ParentPosition(float x, float y)
        {
            var point = new Vector3(x, -y);
            return transform.parent == null ? point : transform.parent.TransformPoint(point);
        }
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask == 0 ? 7 : phaseMask);
            Gizmos.DrawWireCube(transform.position + new Vector3(wallSize.x / 2f, -wallSize.y / 2f), new Vector3(wallSize.x, wallSize.y));
            Gizmos.DrawWireCube(ParentPosition(platformStart.x, platformStart.y) + new Vector3(platformSize.x / 2f, -platformSize.y / 2f), new Vector3(platformSize.x, platformSize.y));
            Gizmos.DrawLine(new Vector3(transform.position.x, ParentPosition(0, wallTop).y), transform.position);
        }
    }
}
