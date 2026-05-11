using System.Collections.Generic;
using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形核心逻辑（纯 C#，类比 McStructureBuilder）。
    /// 持有地形数据（高度 + 地形类型）、碰撞 Mesh、视觉 Tile 生命周期。
    /// </summary>
    public class TerrainBuilder
    {
        public const int TerrainTypeCount = 5;   // R 通道 8-bit bitmask 上限

        // ── 尺寸 / 坐标系 ─────────────────────────────────────────────────────
        public readonly int   width, length, height;
        public readonly float unit;
        public readonly Matrix4x4 localToWorld;
        public readonly Matrix4x4 worldToLocal;
        public int MaxHeightDiff { get; set; } = BuildingConst.TerrainMaxHeightDiff;

        // ── 地形数据 ──────────────────────────────────────────────────────────
        // terrainMask: 8-bit bitmask，bit i=1 表示 type i 存在；同点最多 8 type 叠加
        private struct Point { public sbyte high; public byte terrainMask; }
        private readonly Point[,] _points; // [length+1, width+1]

        // ── 碰撞 Mesh（供笔刷射线检测用）────────────────────────────────────
        public readonly Mesh   colliderMesh;
        private readonly Vector3[] _vertices;

        // ── 5 layer ms_idx 走 per-tile MPB uniform 推送（魔兽风格），无纹理采样 ─────
        // 旧 cell 纹理 + sampler 方案因 bilinear 误差导致 byte 解码错位，弃用

        // ── 视觉 Tile（地形 prefab 实例）───────────────────────────────────────
        private readonly TileCaseConfig  _config;
        private readonly Transform       _parent;
        private readonly GameObject[,]   _tiles;      // [length, width]

        // 8 方向（含对角）：cell 由 4 角组成，对角点也参与同一 cell；
        // 必须约束 8 邻才能保证「任意 cell 4 角 max-min ≤ MaxHeightDiff（=2）」
        // 否则中心点连续升 +3 时，4 邻被拉到 +1 但对角仍 0 → cell 4 角 = (3,1,0,1)
        // → r0=3 ∉ {0,1,2}，GetMeshCase 算出错位 case_idx → 视觉穿帮
        private static readonly (int dx, int dz)[] _neighbors8 =
            { (-1, -1), (-1, 0), (-1, 1), (0, -1), (0, 1), (1, -1), (1, 0), (1, 1) };

        // ── 构造 ─────────────────────────────────────────────────────────────

        public TerrainBuilder(int width, int length, int height, float unit,
                                 Vector3 worldPosition, TileCaseConfig config, Transform parent)
        {
            // 点阵长宽必须相等，且必须是 2 的次幂（如 16/32/64/128/256）。
            // 等宽方阵 + 2^n 对齐让未来 chunk 划分 / 分级 LOD / quadtree 寻址都零特例处理。
            if (width != length)
                throw new System.ArgumentException(
                    $"TerrainBuilder: width({width}) 必须等于 length({length})（点阵必须是方阵）");
            if (width <= 0 || (width & (width - 1)) != 0)
                throw new System.ArgumentException(
                    $"TerrainBuilder: width({width}) 必须是 2 的次幂（16/32/64/128/256/...）");

            this.width  = width;
            this.length = length;
            this.height = height;
            this.unit   = unit;
            localToWorld = Matrix4x4.TRS(worldPosition, Quaternion.identity, Vector3.one * unit);
            worldToLocal = localToWorld.inverse;

            _config     = config;
            _parent     = parent;
            _points     = new Point[length + 1, width + 1];
            _tiles      = new GameObject[length, width];

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

        /// <summary>地形类型刷绘（格点粒度，Add 语义：mask |= 1<<type）。只更新 cell 纹理，不重建 Tile。</summary>
        public bool PaintTerrainType(Brush brush, int type)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            byte bit = (byte)(1 << type);
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            var dirtyPoints = new HashSet<(int, int)>();
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
            if (dirtyPoints.Count > 0) { RefreshAffectedTilesMPB(dirtyPoints); return true; }
            return false;
        }

        /// <summary>地形类型擦除（格点粒度，Erase 语义：mask &= ~(1<<type)）。只重推 MPB，不重建 Tile。</summary>
        public bool EraseTerrainType(Brush brush, int type)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            byte clearBit = (byte)~(1 << type);
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            var dirtyPoints = new HashSet<(int, int)>();
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
            if (dirtyPoints.Count > 0) { RefreshAffectedTilesMPB(dirtyPoints); return true; }
            return false;
        }

        /// <summary>笔刷范围内所有格点 mask = 0（一键清空：fallback 到 _BaseTex）。返回 dirty。</summary>
        public bool ClearTerrainMask(Brush brush)
        {
            (Vector3 center, float radiusSqr) = CalculateArea(
                brush, out int minX, out int minZ, out int maxX, out int maxZ);

            var dirtyPoints = new HashSet<(int, int)>();
            for (int x = minX; x <= maxX; x++)
            for (int z = minZ; z <= maxZ; z++)
            {
                Vector2 d = new Vector2(x - center.x, z - center.z);
                if (d.sqrMagnitude > radiusSqr) continue;
                if (_points[x, z].terrainMask == 0) continue;
                _points[x, z].terrainMask = 0;
                dirtyPoints.Add((x, z));
            }
            if (dirtyPoints.Count > 0) { RefreshAffectedTilesMPB(dirtyPoints); return true; }
            return false;
        }

        // 一组格点变化 → 算受影响 cells（每点影响 4 邻 cell）→ 重推 MPB
        private void RefreshAffectedTilesMPB(HashSet<(int x, int z)> dirtyPoints)
        {
            var dirtyCells = new HashSet<(int, int)>();
            foreach (var (px, pz) in dirtyPoints)
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < length && cz >= 0 && cz < width)
                    dirtyCells.Add((cx, cz));
            }
            foreach (var (cx, cz) in dirtyCells)
                if (_tiles[cx, cz] != null) ApplyTileMPB(_tiles[cx, cz], cx, cz);
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
            // 地形 tile：2×2 cells 范围（每个格点影响其 4 邻 cell）
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
                Object.Destroy(_tiles[x, z]);
                _tiles[x, z] = null;
            }
            int caseIndex = GetCaseIndex(x, z, out int baseH);
            // 当前 case 未配置时回退到 case 0（平地默认块），确保初始地形可见
            var prefab = _config.GetPrefab(caseIndex);
            if (prefab == null) prefab = _config.GetPrefab(0);
            if (prefab == null) return;
            var tile = Object.Instantiate(prefab);
            Transform t = tile.transform;
            t.SetParent(_parent);
            t.localPosition = new Vector3(x, baseH, z);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            ApplyTileMPB(tile, x, z);

            // Debug 组件：记录 case index 和 base 高度，Editor Gizmos 可视化
            var dbg = tile.GetComponent<TilePrefab>();
            if (dbg != null) { dbg.caseIndex = caseIndex; dbg.baseHeight = baseH; }

            _tiles[x, z] = tile;
        }

        /// <summary>
        /// 为 tile 设置 MPB：点阵纹理引用 + 该 tile 在纹理中的 UV 偏移/缩放。
        /// 纹理内容变化时（Apply()）所有 tile 自动生效，无需重推 MPB。
        /// </summary>
        // WC3 风格 per-tile uniform：5 layer 的 atlas case_idx 直接推 MPB，无纹理采样无误差
        // 4 角 mask → atlas case_idx 走 TileTable.GetAtlasCase（atlas 美术约定单点维护）
        private void ApplyTileMPB(GameObject tile, int cx, int cz)
        {
            var mpb = new MaterialPropertyBlock();
            byte mBL = _points[cx,     cz    ].terrainMask;
            byte mBR = _points[cx + 1, cz    ].terrainMask;
            byte mTR = _points[cx + 1, cz + 1].terrainMask;
            byte mTL = _points[cx,     cz + 1].terrainMask;
            int idx0 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 0);
            int idx1 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 1);
            int idx2 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 2);
            int idx3 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 3);
            int idx4 = TileTable.GetAtlasCase(mBL, mBR, mTR, mTL, 4);
            mpb.SetVector("_TileMsIdx",  new Vector4(idx0, idx1, idx2, idx3));
            mpb.SetFloat ("_TileMsIdx4", idx4);
            foreach (var mr in tile.GetComponentsInChildren<MeshRenderer>())
                mr.SetPropertyBlock(mpb);
        }

        private int GetCaseIndex(int x, int z, out int baseH)
            => TileTable.GetMeshCase(
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

        public byte GetTerrainMask(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].terrainMask;
        }

        /// <summary>
        /// cell 4 角 high 完全相等返回 true，baseH 给出统一高度。
        /// cx ∈ [0, length), cz ∈ [0, width)；越界返回 false。
        /// </summary>
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

        // ── Gizmos ────────────────────────────────────────────────────────────

        // 每 N cells 一条黄色粗线（chunk 边界）。WC3 默认 4。
        private const int ChunkSize = 4;

        public void DrawGizmos()
        {
            // 世界坐标系（与 Terrain transform 对齐：Builder 持有 localToWorld 矩阵）
            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = localToWorld;

            // ── 白色细线：所有 cell 边（跟随 high 起伏）──────────────────────
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            for (int x = 0; x <= length; x++)
            for (int z = 0; z <= width; z++)
            {
                Vector3 p = new Vector3(x, _points[x, z].high, z);
                if (x < length)
                    Gizmos.DrawLine(p, new Vector3(x + 1, _points[x + 1, z].high, z));
                if (z < width)
                    Gizmos.DrawLine(p, new Vector3(x, _points[x, z + 1].high, z + 1));
            }

            // ── 顶点小球：当前格点 terrainMask 的最高置位 bit → layer 颜色 ─
            for (int x = 0; x <= length; x++)
            for (int z = 0; z <= width; z++)
            {
                byte mask = _points[x, z].terrainMask;
                if (mask == 0) continue;
                Gizmos.color = MaskGizmoColor(mask);
                Gizmos.DrawSphere(new Vector3(x, _points[x, z].high, z), 0.03f);
            }

            Gizmos.matrix = prevMatrix;

#if UNITY_EDITOR
            // ── 黄色粗线：每 ChunkSize 格的 chunk 边界（屏幕空间宽度 3 像素）──
            var prevHandlesMatrix = UnityEditor.Handles.matrix;
            UnityEditor.Handles.matrix = localToWorld;
            UnityEditor.Handles.color = new Color(1f, 0.85f, 0f, 0.9f);
            // 沿 X 方向的 chunk 边线（z = 0, ChunkSize, 2*ChunkSize, ..., width）
            for (int z = 0; z <= width; z += ChunkSize)
            {
                var line = new Vector3[length + 1];
                for (int x = 0; x <= length; x++)
                    line[x] = new Vector3(x, _points[x, z].high, z);
                UnityEditor.Handles.DrawAAPolyLine(3f, line);
            }
            // 沿 Z 方向的 chunk 边线
            for (int x = 0; x <= length; x += ChunkSize)
            {
                var line = new Vector3[width + 1];
                for (int z = 0; z <= width; z++)
                    line[z] = new Vector3(x, _points[x, z].high, z);
                UnityEditor.Handles.DrawAAPolyLine(3f, line);
            }
            UnityEditor.Handles.matrix = prevHandlesMatrix;
#endif
        }

        // mask byte → Gizmo 颜色：取最高置位 bit 对应 layer 的实际 atlas 中央色
        private static Color MaskGizmoColor(byte mask)
        {
            if ((mask & 0x10) != 0) return new Color(0.36f, 0.15f, 0.41f); // bit 4 紫
            if ((mask & 0x08) != 0) return new Color(0.88f, 0.91f, 0.96f); // bit 3 雪
            if ((mask & 0x04) != 0) return new Color(0.50f, 0.50f, 0.48f); // bit 2 岩
            if ((mask & 0x02) != 0) return new Color(0.18f, 0.62f, 0.17f); // bit 1 草
            if ((mask & 0x01) != 0) return new Color(0.60f, 0.47f, 0.20f); // bit 0 泥
            return Color.gray;
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
