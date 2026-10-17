using UnityEngine;

namespace Phase
{
    [RequireComponent(typeof(Grid))]
    public sealed class PhaseGrid : MonoBehaviour
    {
        public const int CellSize = 8;
        PhaseSandbox sandbox;
        PhaseTerrain[] terrain;
        PhaseMovingPlatform mover;
        PhaseClimbGear[] gears;
        public static Vector3 Snap(Component owner, Vector3 position)
        {
            var grid = owner.GetComponentInParent<Grid>();
            if (grid == null) throw new System.InvalidOperationException("PHASE geometry requires an 8px Unity Grid.");
            if (grid.cellSize != new Vector3(CellSize, CellSize, 1) || grid.cellGap != Vector3.zero || grid.cellLayout != GridLayout.CellLayout.Rectangle)
                throw new System.InvalidOperationException("PHASE requires rectangular 8px cells without gaps.");
            if (grid.transform.rotation != Quaternion.identity || grid.transform.lossyScale != Vector3.one)
                throw new System.InvalidOperationException("PHASE Grid requires unscaled world axes.");
            return grid.CellToWorld(grid.WorldToCell(position + new Vector3(4, 4, 0)));
        }
        public static Vector3 CellTopLeft(Grid grid, Vector3 point)
            => grid.CellToWorld(grid.WorldToCell(point + new Vector3(0, -.001f, 0)) + Vector3Int.up);
        public static Block CreateBox(Component owner, Vector3 position, Vector2Int size, int phase = -1, int mask = 0)
        {
            if (owner.transform.rotation != Quaternion.identity || owner.transform.lossyScale != Vector3.one)
                throw new System.InvalidOperationException("PHASE collider requires unscaled world axes.");
            owner.transform.position = Snap(owner, position);
            var shape = owner.GetComponent<BoxCollider2D>();
            if (shape == null) shape = owner.gameObject.AddComponent<BoxCollider2D>();
            if (size.x < 8 || size.y < 8 || size.x % 8 != 0 || size.y % 8 != 0)
                throw new System.InvalidOperationException("Collision sizes must cover whole 8px Grid cells.");
            shape.enabled = true; shape.size = size; shape.offset = new Vector2(size.x / 2f, -size.y / 2f); shape.isTrigger = true;
            Physics2D.SyncTransforms();
            var bounds = shape.bounds;
            return new Block(bounds.min.x, -bounds.max.y, bounds.size.x, bounds.size.y, phase, mask);
        }
        void Start()
        {
            sandbox = GetComponent<PhaseSandbox>();
            terrain = GetComponentsInChildren<PhaseTerrain>();
            mover = GetComponentInChildren<PhaseMovingPlatform>();
            gears = GetComponentsInChildren<PhaseClimbGear>();
        }
        void LateUpdate()
        {
            var controller = sandbox.Controller;
            for (int i = 0; i < terrain.Length; i++)
                Sync(terrain[i].GetComponent<BoxCollider2D>(), controller.blocks[i], controller.blocks[i].ActiveInPhase(controller.phase) && !controller.blocked.Contains(i));
            if (mover != null) Sync(mover.GetComponent<BoxCollider2D>(), controller.mover);
            for (int i = 0; i < gears.Length; i++)
            {
                Sync(gears[i].GetComponent<BoxCollider2D>(), controller.gears[i].wall);
                Sync(gears[i].transform.Find("Platform collider").GetComponent<BoxCollider2D>(), controller.gears[i].platform);
            }
            Physics2D.SyncTransforms();
        }
        static void Sync(BoxCollider2D shape, Block block, bool active = true)
        {
            shape.enabled = active;
            shape.transform.position = new Vector3((float)block.x, -(float)block.y);
            shape.size = new Vector2((float)block.w, (float)block.h);
            shape.offset = new Vector2((float)block.w / 2, -(float)block.h / 2);
        }
    }
}
