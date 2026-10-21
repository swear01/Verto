using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Phase
{
    [Flags] public enum PlatformSides { Up = 1, Down = 2, Left = 4, Right = 8 }

    // A jump platform. The transform marks the body's top-left cell at the first pivot; `pivots` are the centre cells it visits,
    // as offsets in cells (right, down) from the first, which stays at zero. The path closes back to the first pivot.
    public sealed class PhaseJumpPlatform : MonoBehaviour
    {
        public bool vertical;
        [Min(1)] public int length = 3;
        public Vector2Int[] pivots = { Vector2Int.zero, new Vector2Int(8, 0) };
        public bool reverse, spiked;
        [Range(1, 7)] public int phaseMask = 7;
        public PlatformSides spikes;
        Vector2Int Size => vertical ? new Vector2Int(8, length * 8) : new Vector2Int(length * 8, 8);
        public Platform CreatePlatform()
        {
            Sanitize();
            var box = PhaseGrid.CreateBox(this, transform.position, Size, mask: phaseMask);
            int x = (int)Math.Round(box.x) / 8 + (vertical ? 0 : length / 2), y = (int)Math.Round(box.y) / 8 + (vertical ? length / 2 : 0);
            var sides = new List<SpikeDirection>();
            if ((spikes & PlatformSides.Up) != 0) sides.Add(SpikeDirection.Up);
            if ((spikes & PlatformSides.Down) != 0) sides.Add(SpikeDirection.Down);
            if ((spikes & PlatformSides.Left) != 0) sides.Add(SpikeDirection.Left);
            if ((spikes & PlatformSides.Right) != 0) sides.Add(SpikeDirection.Right);
            return new Platform(vertical, length, pivots.Select(p => (x + p.x, y + p.y)), reverse, phaseMask, spiked, sides);
        }
        void OnValidate() => Sanitize();
        // An odd length, a color, at least two stops and the first at zero; the path itself is free.
        void Sanitize()
        {
            length = Mathf.Max(1, length | 1);
            phaseMask = phaseMask < 1 ? 7 : Mathf.Min(phaseMask, 7);
            if (pivots == null || pivots.Length < 2) pivots = new[] { Vector2Int.zero, vertical ? new Vector2Int(0, -1) : new Vector2Int(1, 0) };
            pivots[0] = Vector2Int.zero;
        }
        void OnDrawGizmos()
        {
            if (pivots == null || pivots.Length == 0) return;
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask == 0 ? 7 : phaseMask);
            for (int i = 0; i < pivots.Length; i++)
            {
                var next = pivots[(i + 1) % pivots.Length];
                Gizmos.DrawWireCube(transform.position + new Vector3(pivots[i].x * 8 + Size.x / 2f, -pivots[i].y * 8 - Size.y / 2f), new Vector3(Size.x, Size.y));
                Gizmos.DrawLine(transform.position + new Vector3(Size.x / 2f + pivots[i].x * 8, -Size.y / 2f - pivots[i].y * 8),
                    transform.position + new Vector3(Size.x / 2f + next.x * 8, -Size.y / 2f - next.y * 8));
            }
        }
    }
}
