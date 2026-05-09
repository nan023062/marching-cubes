using System.Collections.Generic;
using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形核心逻辑（纯 C#，类比 McStructureBuilder）。
    /// 持有地形数据（高度 + 地形类型）、碰撞 Mesh、视觉 Tile 生命周期。
    /// </summary>
    public class MqTerrainBuilder
    {
        public const int TerrainTypeCount = 5;

        // ── 尺寸 / 坐标系 ─────────────────────────────────────────────────────
        public readonly int   width, length, height;
        public readonly float unit;
        public readonly Matrix4x4 localToWorld;
        public readonly Matrix4x4 worldToLocal;
        public int MaxHeightDiff { get; set; } = 1;

        // ── 地形数据 ──────────────────────────────────────────────────────────
        private struct Point { public sbyte high; public byte terrainType; }
        private readonly Point[,] _points; // [length+1, width+1]

        // ── 碰撞 Mesh（供笔刷射线检测用）────────────────────────────────────
        public readonly Mesh   colliderMesh;
        private readonly Vector3[] _vertices;

        // ── 视觉 Tile（case prefab 实例）────────────────────────────────────
        private readonly MqMeshConfig  _config;
        private readonly Transform     _parent;
        private readonly GameObject[,] _tiles; // [length, width]

        private static readonly (int dx, int dz)[] _neighbors4 =
            { (-1, 0), (1, 0), (0, -1), (0, 1) };

        // ── 构造 ─────────────────────────────────────────────────────────────

        public MqTerrainBuilder(int width, int length, int height, float unit,
                                 Vector3 worldPosition, MqMeshConfig config, Transform parent)
        {
            this.width  = width;
            this.length = length;
            this.height = height;
            this.unit   = unit;
            localToWorld = Matrix4x4.TRS(worldPosition, Quaternion.identity, Vector3.one * unit);
            worldToLocal = localToWorld.inverse;

            _config = config;
            _parent = parent;
            _points = new Point[length + 1, width + 1];
            _tiles  = new GameObject[length, width];

            // 碰撞 Mesh：只存顶点位置，不含 UV / 颜色
            int totalVertex = length * width * 2 * 3;
            colliderMesh = new Mesh();
            _vertices    = new Vector3[totalVertex];
            var triangles = new int[totalVertex];
            for (int i = 0; i < totalVertex; i++) triangles[i] = i;

            for (int z = 0; z < width; z++)
            for (int x = 0; x < length; x++)
            {
                int idx = (x + length * z) * 6;
                _vertices[idx + 0] = new Vector3(x,     0, z);
                _vertices[idx + 1] = new Vector3(x,     0, z + 1);
                _vertices[idx + 2] = new Vector3(x + 1, 0, z);
                _vertices[idx + 3] = new Vector3(x + 1, 0, z);
                _vertices[idx + 4] = new Vector3(x,     0, z + 1);
                _vertices[idx + 5] = new Vector3(x + 1, 0, z + 1);
            }
            colliderMesh.vertices  = _vertices;
            colliderMesh.triangles = triangles;
            colliderMesh.RecalculateNormals();
        }

        // ── 地形操作 ──────────────────────────────────────────────────────────

        /// <summary>高度刷绘。返回 dirty=true 时碰撞 Mesh 和视觉 Tile 已同步更新。</summary>
        public bool BrushMapHigh(Brush brush, int delta)
        {
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            var queue        = new Queue<(int x, int z)>();
            var changedPoints = new HashSet<(int, int)>();
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
            {
                EnforceHeightConstraint(queue, changedPoints);
                colliderMesh.vertices = _vertices;
                colliderMesh.RecalculateNormals();
                foreach (var (px, pz) in changedPoints)
                    RefreshAffectedTiles(px, pz);
            }
            return dirty;
        }

        /// <summary>地形类型刷绘。只更新 MaterialPropertyBlock，不重建 Tile。</summary>
        public bool PaintTerrainType(Brush brush, int type)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            bool dirty = false;
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude <= radiusSqr && _points[x, z].terrainType != (byte)type)
                {
                    _points[x, z].terrainType = (byte)type;
                    UpdateAffectedTileColors(x, z);
                    dirty = true;
                }
            }
            return dirty;
        }

        // ── 视觉 Tile ─────────────────────────────────────────────────────────

        public void RefreshAllTiles()
        {
            for (int x = 0; x < length; x++)
            for (int z = 0; z < width; z++)
                RefreshTile(x, z);
        }

        public void RefreshAffectedTiles(int px, int pz)
        {
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < length && cz >= 0 && cz < width)
                    RefreshTile(cx, cz);
            }
        }

        private void RefreshTile(int x, int z)
        {
            if (_tiles[x, z] != null)
            {
                Object.DestroyImmediate(_tiles[x, z]);
                _tiles[x, z] = null;
            }
            if (_config == null) return;

            int caseIndex = GetCaseIndex(x, z, out int baseH);
            var prefab    = _config.GetPrefab(caseIndex);
            if (prefab == null) return;

            var tile = Object.Instantiate(prefab);
            tile.transform.SetParent(_parent);
            tile.transform.localPosition = new Vector3(x, baseH, z);
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale    = Vector3.one;

            ApplyTileTerrainColors(tile, x, z);

            // Debug 组件：记录 case index 和 base 高度，Editor Gizmos 可视化
            var dbg = tile.GetComponent<MqTilePrefab>();
            if (dbg != null) { dbg.caseIndex = caseIndex; dbg.baseHeight = baseH; }

            _tiles[x, z] = tile;
        }

        private void ApplyTileTerrainColors(GameObject tile, int x, int z)
        {
            var mpb = new MaterialPropertyBlock();
            mpb.SetFloat("_T0", _points[x,     z    ].terrainType); // BL
            mpb.SetFloat("_T1", _points[x + 1, z    ].terrainType); // BR
            mpb.SetFloat("_T2", _points[x + 1, z + 1].terrainType); // TR
            mpb.SetFloat("_T3", _points[x,     z + 1].terrainType); // TL
            foreach (var mr in tile.GetComponentsInChildren<MeshRenderer>())
                mr.SetPropertyBlock(mpb);
        }

        private void UpdateAffectedTileColors(int px, int pz)
        {
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < length && cz >= 0 && cz < width && _tiles[cx, cz] != null)
                    ApplyTileTerrainColors(_tiles[cx, cz], cx, cz);
            }
        }

        private int GetCaseIndex(int x, int z, out int baseH)
            => MqTable.GetMeshCase(
                _points[x,     z    ].high,
                _points[x + 1, z    ].high,
                _points[x + 1, z + 1].high,
                _points[x,     z + 1].high,
                out baseH);

        // ── 数据访问 ─────────────────────────────────────────────────────────

        public sbyte GetPointHeight(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].high;
        }

        public byte GetTerrainType(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].terrainType;
        }

        // ── Gizmos ────────────────────────────────────────────────────────────

        public void DrawGizmos()
        {
            Gizmos.color = Color.red;
            for (int i = 0; i <= length; i++)
            for (int j = 0; j <= width; j++)
            {
                int h = _points[i, j].high;
                Gizmos.DrawSphere(new Vector3(i, h, j), 0.05f);
            }
        }

        // ── 内部辅助 ─────────────────────────────────────────────────────────

        private void ApplyPointHeight(int x, int z, sbyte newHigh)
        {
            _points[x, z].high = newHigh;
            var p = new Vector3(x, newHigh, z);
            if (x > 0 && z > 0)
                _vertices[(x - 1 + (z - 1) * length) * 6 + 5] = p;
            if (x > 0 && z < width)
            {
                int i = (x - 1 + z * length) * 6;
                _vertices[i + 2] = p; _vertices[i + 3] = p;
            }
            if (x < length && z < width)
                _vertices[(x + z * length) * 6 + 0] = p;
            if (x < length && z > 0)
            {
                int i = (x + (z - 1) * length) * 6;
                _vertices[i + 1] = p; _vertices[i + 4] = p;
            }
        }

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
                foreach (var (dx, dz) in _neighbors4)
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

        private (Vector3, float) CalculateArea(Brush brush,
            out int minX, out int minZ, out int maxX, out int maxZ)
        {
            float radius    = brush.Size * 0.5f;
            float radiusSqr = radius * radius;
            Vector3 center  = worldToLocal.MultiplyPoint(brush.transform.position);
            minX = Mathf.Clamp(Mathf.CeilToInt(center.x - radius), 0, length);
            minZ = Mathf.Clamp(Mathf.CeilToInt(center.z - radius), 0, width);
            maxX = Mathf.Clamp(Mathf.FloorToInt(center.x + radius), 0, length);
            maxZ = Mathf.Clamp(Mathf.FloorToInt(center.z + radius), 0, width);
            return (center, radiusSqr);
        }
    }
}
