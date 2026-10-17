using UnityEngine;

namespace Phase
{
    public sealed class PhaseLaser : MonoBehaviour
    {
        public LaserDirection direction;
        [Range(1, 7)] public int phaseMask = 7;
        public Laser CreateLaser() => new Laser(Mathf.Round(transform.position.x), -Mathf.Round(transform.position.y), direction, phaseMask);
        void OnDrawGizmos()
        {
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask);
            Gizmos.DrawWireCube(transform.position + new Vector3(4, -4), new Vector3(8, 8));
            var offset = direction == LaserDirection.Up ? Vector3.up : direction == LaserDirection.Down ? Vector3.down :
                direction == LaserDirection.Left ? Vector3.left : Vector3.right;
            Gizmos.DrawRay(transform.position + new Vector3(4, -4), offset * 16);
        }
    }
}
