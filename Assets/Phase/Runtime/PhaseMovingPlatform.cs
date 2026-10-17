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
        public MovingPlatform CreatePlatform()
        {
            if (size.x <= 0 || size.y <= 0 || nodes == null || nodes.Length < 2 || start < 0 || start >= nodes.Length)
                throw new InvalidOperationException("Platform requires positive size, two nodes and a valid start.");
            var shape = PhaseGrid.CreateBox(this, new Vector3(nodes[start].x, -nodes[start].y), size);
            return new MovingPlatform(nodes.Select(n => { var p = PhaseGrid.Snap(this, new Vector3(n.x, -n.y)); return new[] { (double)p.x, -p.y }; }).ToArray(), shape.w, shape.h, returns, start);
        }
        void OnDrawGizmos()
        {
            if (nodes == null) return;
            Gizmos.color = Color.white;
            foreach (var n in nodes) Gizmos.DrawWireCube(new Vector3(n.x + size.x / 2f, -n.y - size.y / 2f), new Vector3(size.x, size.y, 0));
            for (int i = 1; i < nodes.Length; i++) Gizmos.DrawLine(new Vector3(nodes[i - 1].x, -nodes[i - 1].y), new Vector3(nodes[i].x, -nodes[i].y));
        }
    }
}
