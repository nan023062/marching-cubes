using System.Collections.Generic;
using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形核心逻辑（纯 C#）。
    /// 持有地形数据（高度 + 地形类型）和碰撞 Mesh 顶点数据。
    /// 不持有任何 GameObject / Tile，显示逻辑由 TerrainController 负责。
    /// </summary>
    public class TileBuilder : BuilderBase
    {
        public const int TerrainTypeCount = 5;

        // ── 尺寸 / 坐标系 ─────────────────────────────────────────────────────
        public readonly int   width, length, height;
        public readonly float unit;
        public readonly Matrix4x4 worldToLocal;
        public int MaxHeightDiff { get; set; } = BuildingConst.TerrainMaxHeightDiff;

        // ── 地形数据 ──────────────────────────────────────────────────────────
        private struct Point { public sbyte high; public byte terrainMask; }
        private readonly Point[,] _points;

        private static readonly (int dx, int dz)[] _neighbors8 =
            { (-1,-1), (-1,0), (-1,1), (0,-1), (0,1), (1,-1), (1,0), (1,1) };

        // ── 构造 ─────────────────────────────────────────────────────────────

        public TileBuilder(int width, int length, int height, float unit, Vector3 worldPosition)
        {
            if (width != length)
                throw new System.ArgumentException(
                    $"TerrainBuilder: width({width}) 必须等于 length({length})");
            if (width <= 0 || (width & (width - 1)) != 0)
                throw new System.ArgumentException(
                    $"TerrainBuilder: width({width}) 必须是 2 的次幂");

            this.width  = width;
            this.length = length;
            this.height = height;
            this.unit   = unit;

            localToWorld = Matrix4x4.TRS(worldPosition, Quaternion.identity, Vector3.one * unit);
            worldToLocal = localToWorld.inverse;

            _points = new Point[length + 1, width + 1];
        }

        // ── 地形操作（返回 dirty 集合，由 Controller 驱动视觉刷新）────────────

        public bool BrushMapHigh(MarchingCubes.Sample.Cursor brush, int delta, out HashSet<(int px, int pz)> changedPoints)
        {
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            var queue = new Queue<(int x, int z)>();
            changedPoints = new HashSet<(int, int)>();
            bool dirty = false;

            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude <= radiusSqr && SetPointHeightDelta(x, z, delta))
                {
                    queue.Enqueue((x, z));
                    changedPoints.Add((x, z));
                    dirty = true;
                }
            }

            if (dirty)
                EnforceHeightConstraint(queue, changedPoints);
            return dirty;
        }

        public bool PaintTerrainType(MarchingCubes.Sample.Cursor brush, int type, out HashSet<(int px, int pz)> dirtyPoints)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            byte bit = (byte)(1 << type);
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            dirtyPoints = new HashSet<(int, int)>();
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude > radiusSqr) continue;
                byte oldMask = _points[x, z].terrainMask;
                byte newMask = (byte)(oldMask | bit);
                if (newMask == oldMask) continue;
                _points[x, z].terrainMask = newMask;
                dirtyPoints.Add((x, z));
            }
            return dirtyPoints.Count > 0;
        }

        public bool EraseTerrainType(MarchingCubes.Sample.Cursor brush, int type, out HashSet<(int px, int pz)> dirtyPoints)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            byte clearBit = (byte)~(1 << type);
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            dirtyPoints = new HashSet<(int, int)>();
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude > radiusSqr) continue;
                byte oldMask = _points[x, z].terrainMask;
                byte newMask = (byte)(oldMask & clearBit);
                if (newMask == oldMask) continue;
                _points[x, z].terrainMask = newMask;
                dirtyPoints.Add((x, z));
            }
            return dirtyPoints.Count > 0;
        }

        public bool ClearTerrainMask(MarchingCubes.Sample.Cursor brush, out HashSet<(int px, int pz)> dirtyPoints)
        {
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            dirtyPoints = new HashSet<(int, int)>();
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude > radiusSqr) continue;
                if (_points[x, z].terrainMask == 0) continue;
                _points[x, z].terrainMask = 0;
                dirtyPoints.Add((x, z));
            }
            return dirtyPoints.Count > 0;
        }

        // ── 数据查询 ──────────────────────────────────────────────────────────

        public sbyte GetPointHeight(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].high;
        }

        public byte GetTerrainMask(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].terrainMask;
        }

        public bool IsCellFlat(int cx, int cz, out int baseH)
        {
            baseH = 0;
            if (cx < 0 || cx >= length || cz < 0 || cz >= width) return false;
            int h0 = _points[cx,     cz    ].high;
            int h1 = _points[cx + 1, cz    ].high;
            int h2 = _points[cx + 1, cz + 1].high;
            int h3 = _points[cx,     cz + 1].high;
            if (h0 != h1 || h0 != h2 || h0 != h3) return false;
            baseH = h0;
            return true;
        }

        public int GetCaseIndex(int x, int z, out int baseH)
            => TileTable.GetMeshCase(
                _points[x,     z    ].high,
                _points[x + 1, z    ].high,
                _points[x + 1, z + 1].high,
                _points[x,     z + 1].high,
                out baseH);

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        private void ApplyPointHeight(int x, int z, sbyte newHigh)
            => _points[x, z].high = newHigh;

        private bool SetPointHeightDelta(int x, int z, int d)
        {
            ref var pt  = ref _points[x, z];
            sbyte high  = (sbyte)Mathf.Clamp(d + pt.high, -64, 64);
            if (high == pt.high) return false;
            ApplyPointHeight(x, z, high);
            return true;
        }

        private void EnforceHeightConstraint(Queue<(int x, int z)> queue,
                                              HashSet<(int, int)> changed)
        {
            while (queue.Count > 0)
            {
                var (px, pz) = queue.Dequeue();
                int h = _points[px, pz].high;
                foreach (var (dx, dz) in _neighbors8)
                {
                    int nx = px + dx, nz = pz + dz;
                    if (nx < 0 || nx > length || nz < 0 || nz > width) continue;
                    int diff = h - _points[nx, nz].high;
                    int target;
                    if      (diff >  MaxHeightDiff) target = h - MaxHeightDiff;
                    else if (diff < -MaxHeightDiff) target = h + MaxHeightDiff;
                    else continue;
                    target = Mathf.Clamp(target, -64, 64);
                    if (target == _points[nx, nz].high) continue;
                    ApplyPointHeight(nx, nz, (sbyte)target);
                    queue.Enqueue((nx, nz));
                    changed.Add((nx, nz));
                }
            }
        }

        private (Vector3, float) CalculateArea(MarchingCubes.Sample.Cursor brush,
            out int minX, out int minZ, out int maxX, out int maxZ)
        {
            float radius    = brush.Size * 0.5f;
            float radiusSqr = radius * radius;
            Vector3 center  = worldToLocal.MultiplyPoint(brush.transform.position);
            minX = Mathf.Clamp(Mathf.CeilToInt(center.x - radius),  0, length);
            minZ = Mathf.Clamp(Mathf.CeilToInt(center.z - radius),  0, width);
            maxX = Mathf.Clamp(Mathf.FloorToInt(center.x + radius), 0, length);
            maxZ = Mathf.Clamp(Mathf.FloorToInt(center.z + radius), 0, width);
            return (center, radiusSqr);
        }
    }
}
