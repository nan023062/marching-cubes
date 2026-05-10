using System.Collections.Generic;
using UnityEngine;

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
        public int MaxHeightDiff { get; set; } = 1;

        // ── 地形数据 ──────────────────────────────────────────────────────────
        // terrainMask: 8-bit bitmask，bit i=1 表示 type i 存在；同点最多 8 type 叠加
        private struct Point { public sbyte high; public byte terrainMask; }
        private readonly Point[,] _points; // [length+1, width+1]

        // ── 碰撞 Mesh（供笔刷射线检测用）────────────────────────────────────
        public readonly Mesh   colliderMesh;
        private readonly Vector3[] _vertices;

        // ── 5 layer ms_idx 走 per-tile MPB uniform 推送（魔兽风格），无纹理采样 ─────
        // 旧 cell 纹理 + sampler 方案因 bilinear 误差导致 byte 解码错位，弃用

        // ── 视觉 Tile（地形 + 悬崖 prefab 实例）────────────────────────────────
        private readonly TileCaseConfig  _config;
        private readonly Transform       _parent;
        private readonly GameObject[,]   _tiles;      // [length, width]
        private readonly GameObject[,]   _cliffTiles; // [length, width]

        // 4 方向：只约束上下左右相邻点，对角点高差最大为 2，由 case 15-18 专门处理
        private static readonly (int dx, int dz)[] _neighbors4 =
            { (-1, 0), (1, 0), (0, -1), (0, 1) };

        // ── 构造 ─────────────────────────────────────────────────────────────

        public TerrainBuilder(int width, int length, int height, float unit,
                                 Vector3 worldPosition, TileCaseConfig config, Transform parent)
        {
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
            _cliffTiles = new GameObject[length, width];

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
            {
                RefreshTile(x, z);
                RefreshCliffTile(x, z);
            }
        }

        public void RefreshAffectedTiles(int px, int pz)
        {
            // 地形 tile：2×2 cells 范围
            for (int dx = -1; dx <= 0; dx++)
            for (int dz = -1; dz <= 0; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < length && cz >= 0 && cz < width)
                    RefreshTile(cx, cz);
            }
            // 悬崖 tile：4×4 cells 范围（cliff case 依赖相邻格 base，需要更大范围）
            for (int dx = -2; dx <= 1; dx++)
            for (int dz = -2; dz <= 1; dz++)
            {
                int cx = px + dx, cz = pz + dz;
                if (cx >= 0 && cx < length && cz >= 0 && cz < width)
                    RefreshCliffTile(cx, cz);
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
            var tile = Object.Instantiate(prefab);
            Transform t = tile.transform;
            t.SetParent(_parent);
            t.localPosition = new Vector3(x, baseH, z);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            ApplyTileMPB(tile, x, z);

            // Debug 组件：记录 case index 和 base 高度，Editor Gizmos 可视化
            var dbg = tile.GetComponent<TilePrefab>();
            if (dbg != null) { dbg.tileType = TileType.Terrain; dbg.caseIndex = caseIndex; dbg.baseHeight = baseH; }

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

        // ── 悬崖 Tile ─────────────────────────────────────────────────────────

        private int GetCellBaseHeight(int cx, int cz)
        {
            int h = _points[cx, cz].high;
            if (_points[cx + 1, cz    ].high < h) h = _points[cx + 1, cz    ].high;
            if (_points[cx + 1, cz + 1].high < h) h = _points[cx + 1, cz + 1].high;
            if (_points[cx,     cz + 1].high < h) h = _points[cx,     cz + 1].high;
            return h;
        }

        private int GetCliffCase(int cx, int cz, out int baseH)
        {
            baseH = GetCellBaseHeight(cx, cz);
            int ci = 0;
            if (cz > 0       && baseH > GetCellBaseHeight(cx,     cz - 1)) ci |= 1; // E0 南
            if (cx < length-1 && baseH > GetCellBaseHeight(cx + 1, cz    )) ci |= 2; // E1 东
            if (cz < width-1  && baseH > GetCellBaseHeight(cx,     cz + 1)) ci |= 4; // E2 北
            if (cx > 0        && baseH > GetCellBaseHeight(cx - 1, cz    )) ci |= 8; // E3 西
            return ci;
        }

        private void RefreshCliffTile(int cx, int cz)
        {
            if (_cliffTiles[cx, cz] != null)
            {
                Object.Destroy(_cliffTiles[cx, cz]);
                _cliffTiles[cx, cz] = null;
            }
            int cliffCase = GetCliffCase(cx, cz, out int baseH);
            if (cliffCase == 0) return;

            var prefab = _config.GetCliffPrefab(cliffCase); // case 0 = 无悬崖，不作为 fallback
            if (prefab == null) return;

            // 实际悬崖高度 = 当前格 base 与最低相邻格 base 的最大高差
            int cliffH = 0;
            if ((cliffCase & 1) != 0 && cz > 0)        cliffH = Mathf.Max(cliffH, baseH - GetCellBaseHeight(cx,     cz - 1));
            if ((cliffCase & 2) != 0 && cx < length - 1) cliffH = Mathf.Max(cliffH, baseH - GetCellBaseHeight(cx + 1, cz    ));
            if ((cliffCase & 4) != 0 && cz < width - 1)  cliffH = Mathf.Max(cliffH, baseH - GetCellBaseHeight(cx,     cz + 1));
            if ((cliffCase & 8) != 0 && cx > 0)          cliffH = Mathf.Max(cliffH, baseH - GetCellBaseHeight(cx - 1, cz    ));
            if (cliffH <= 0) return;

            var tile = Object.Instantiate(prefab);
            tile.transform.SetParent(_parent);
            // 悬崖 Mesh 1 unit 高，Y 轴按实际高差缩放；底部放在低侧地面（baseH - cliffH）
            tile.transform.localPosition = new Vector3(cx + 0.5f, baseH - cliffH, cz + 0.5f);
            tile.transform.localRotation = Quaternion.identity;
            tile.transform.localScale    = new Vector3(1f, cliffH, 1f);

            var dbg = tile.GetComponent<TilePrefab>();
            if (dbg != null) { dbg.tileType = TileType.Cliff; dbg.caseIndex = cliffCase; dbg.baseHeight = baseH - cliffH; }

            _cliffTiles[cx, cz] = tile;
        }


        // ── Gizmos ────────────────────────────────────────────────────────────

        public void DrawGizmos()
        {
            for (int i = 0; i <= length; i++)
            for (int j = 0; j <= width; j++)
            {
                int h = _points[i, j].high;
                byte mask = _points[i, j].terrainMask;
                Vector3 pos = new Vector3(i, h, j);
                if (mask == 0)
                {
                    Gizmos.color = Color.gray;
                    Gizmos.DrawSphere(pos, 0.05f);
                }
                else
                {
                    Gizmos.color = MaskGizmoColor(mask);
                    Gizmos.DrawSphere(pos, 0.08f);
#if UNITY_EDITOR
                    UnityEditor.Handles.Label(pos + Vector3.up * 0.6f, MaskBitsString(mask));
#endif
                }
            }
        }

        // mask byte → Gizmo 颜色：取最高置位 bit 对应 layer 的实际 atlas 中央色
        private static Color MaskGizmoColor(byte mask)
        {
            if ((mask & 0x10) != 0) return new Color(0.36f, 0.15f, 0.41f); // bit 4 紫 [92,37,105]
            if ((mask & 0x08) != 0) return new Color(0.88f, 0.91f, 0.96f); // bit 3 雪 [225,231,246]
            if ((mask & 0x04) != 0) return new Color(0.50f, 0.50f, 0.48f); // bit 2 岩 [127,127,122]
            if ((mask & 0x02) != 0) return new Color(0.18f, 0.62f, 0.17f); // bit 1 草 [45,158,43]
            if ((mask & 0x01) != 0) return new Color(0.60f, 0.47f, 0.20f); // bit 0 泥 [152,119,50]
            return Color.gray;
        }

        // mask byte → 5 位 0/1 字符串（bit 4 → bit 0），如 mask=3 → "00011"
        private static string MaskBitsString(byte mask)
        {
            char b4 = ((mask >> 4) & 1) == 1 ? '1' : '0';
            char b3 = ((mask >> 3) & 1) == 1 ? '1' : '0';
            char b2 = ((mask >> 2) & 1) == 1 ? '1' : '0';
            char b1 = ((mask >> 1) & 1) == 1 ? '1' : '0';
            char b0 = ( mask       & 1) == 1 ? '1' : '0';
            return $"{b4}{b3}{b2}{b1}{b0}";
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
