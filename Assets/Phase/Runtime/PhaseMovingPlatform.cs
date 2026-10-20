using System;
using System.Linq;
using UnityEngine;

namespace Phase
{
    public sealed class PhaseMovingPlatform : MonoBehaviour
    {
        public Vector2Int size = new Vector2Int(40, 8);
        public Vector2Int[] nodes = { new Vector2Int(80, 120), new Vector2Int(160, 120) };
        public bool returns;
        public int start;
        [Range(0, 7)] public int phaseMask;
        public MovingPlatform CreatePlatform()
        {
            if (size.x <= 0 || size.y <= 0 || nodes == null || nodes.Length < 2 || start < 0 || start >= nodes.Length)
                throw new InvalidOperationException("Platform requires positive size, two nodes and a valid start.");
            var shape = PhaseGrid.CreateBox(this, NodePosition(nodes[start]), size);
            return new MovingPlatform(nodes.Select(n => { var p = PhaseGrid.Snap(this, NodePosition(n)); return new[] { (double)p.x, -p.y }; }).ToArray(), shape.w, shape.h, returns, start, phaseMask);
        }
        Vector3 NodePosition(Vector2Int node)
        {
            var point = new Vector3(node.x, -node.y);
            return transform.parent == null ? point : transform.parent.TransformPoint(point);
        }
        void OnDrawGizmos()
        {
            if (nodes == null) return;
            Gizmos.color = PhasePixels.SurfaceColor(phaseMask == 0 ? 7 : phaseMask);
            foreach (var n in nodes) Gizmos.DrawWireCube(NodePosition(n) + new Vector3(size.x / 2f, -size.y / 2f), new Vector3(size.x, size.y, 0));
            for (int i = 1; i < nodes.Length; i++) Gizmos.DrawLine(NodePosition(nodes[i - 1]), NodePosition(nodes[i]));
        }
    }
}
