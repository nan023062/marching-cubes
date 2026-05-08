using System.Collections.Generic;
using UnityEngine;

namespace MarchingSquares
{
    public partial class MSQTerrain
    {
        public const int TerrainTypeCount = 5;

        public readonly int width, length, height;
        public readonly float unit;
        private readonly Point[,] _points;
        public readonly Matrix4x4 localToWorld;
        public readonly Matrix4x4 worldToLocal;
        public readonly Mesh mesh;
        private readonly Vector3[] _vertices;
        private readonly Vector2[] _uv0;
        private readonly Vector2[] _uv1;
        private readonly Vector2[] _uv2;
        private readonly Vector2[] _uv3;
        private readonly Color32[] _colors;
        public readonly Mesh cliffMesh;
        private readonly List<Vector3> _cliffVertices = new List<Vector3>(256);
        private readonly List<int> _cliffTriangles = new List<int>(384);
        private readonly List<Vector2> _cliffUVs = new List<Vector2>(256);

        static readonly Vector2 TileUVSize = new Vector2(0.25f, 0.25f);

        static byte EncodeType(int type) { return (byte)(type * 51); }

        public MSQTerrain(int width, int length, int height, float unit, Vector3 position)
        {
            this.width = width;
            this.length = length;
            this.height = height;
            this.unit = unit;
            localToWorld = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one * unit);
            worldToLocal = localToWorld.inverse;
            _points = new Point[length + 1, width + 1];
            for (int i = 0; i <= length; i++)
            {
                for (int j = 0; j <= width; j++)
                    _points[i, j] = new Point();
            }

            int totalTriangle = length * width * 2;
            int totalVertex = totalTriangle * 3;
            mesh = new Mesh();
            cliffMesh = new Mesh();
            _vertices = new Vector3[totalVertex];
            int[] triangles = new int[totalVertex];
            _uv0 = new Vector2[totalVertex];
            _uv1 = new Vector2[totalVertex];
            _uv2 = new Vector2[totalVertex];
            _uv3 = new Vector2[totalVertex];
            _colors = new Color32[totalVertex];

            for (int i = 0; i < totalVertex; i++)
                triangles[i] = i;

            for (int z = 0; z < width; z++)
            {
                for (int x = 0; x < length; x++)
                {
                    int idx = (x + length * z) * 6;
                    _vertices[idx + 0] = new Vector3(x, 0, z);
                    _vertices[idx + 1] = new Vector3(x, 0, z + 1);
                    _vertices[idx + 2] = new Vector3(x + 1, 0, z);
                    _vertices[idx + 3] = new Vector3(x + 1, 0, z);
                    _vertices[idx + 4] = new Vector3(x, 0, z + 1);
                    _vertices[idx + 5] = new Vector3(x + 1, 0, z + 1);

                    _uv0[idx + 0] = new Vector2(x, z);
                    _uv0[idx + 1] = new Vector2(x, z + 1);
                    _uv0[idx + 2] = new Vector2(x + 1, z);
                    _uv0[idx + 3] = new Vector2(x + 1, z);
                    _uv0[idx + 4] = new Vector2(x, z + 1);
                    _uv0[idx + 5] = new Vector2(x + 1, z + 1);

                    UpdateCellRendering(x, z);
                }
            }

            mesh.vertices = _vertices;
            mesh.triangles = triangles;
            mesh.uv = _uv0;
            mesh.uv2 = _uv1;
            mesh.uv3 = _uv2;
            mesh.uv4 = _uv3;
            mesh.colors32 = _colors;
            mesh.RecalculateNormals();
        }

        public bool PaintTerrainType(Brush brush, int type)
        {
            type = Mathf.Clamp(type, 0, TerrainTypeCount - 1);
            (Vector3 center, float radiusSqr) = CalculateArea(brush, out int minX,
                out int minZ, out int maxX, out int maxZ);

            bool dirty = false;
            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 d = new Vector2(x - center.x, z - center.z);
                    if (d.sqrMagnitude <= radiusSqr)
                    {
                        ref var point = ref _points[x, z];
                        if (point.terrainType != (byte)type)
                        {
                            point.terrainType = (byte)type;
                            UpdatePointCells(x, z);
                            dirty = true;
                        }
                    }
                }
            }

            if (dirty)
            {
                mesh.uv2 = _uv1;
                mesh.uv3 = _uv2;
                mesh.uv4 = _uv3;
                mesh.colors32 = _colors;
            }

            return dirty;
        }

        private void UpdatePointCells(int px, int pz)
        {
            if (px > 0 && pz > 0) UpdateCellRendering(px - 1, pz - 1);
            if (px > 0 && pz < width) UpdateCellRendering(px - 1, pz);
            if (px < length && pz > 0) UpdateCellRendering(px, pz - 1);
            if (px < length && pz < width) UpdateCellRendering(px, pz);
        }

        private void UpdateCellRendering(int cellX, int cellZ)
        {
            byte tBL = _points[cellX, cellZ].terrainType;
            byte tTL = _points[cellX, cellZ + 1].terrainType;
            byte tTR = _points[cellX + 1, cellZ + 1].terrainType;
            byte tBR = _points[cellX + 1, cellZ].terrainType;

            byte baseType = Min4(tBL, tTL, tTR, tBR);

            int overlayCount = 0;
            byte o1 = 0, o2 = 0, o3 = 0;
            for (byte t = (byte)(baseType + 1); t < TerrainTypeCount; t++)
            {
                if (t == tBL || t == tTL || t == tTR || t == tBR)
                {
                    if (overlayCount == 0) o1 = t;
                    else if (overlayCount == 1) o2 = t;
                    else o3 = t;
                    overlayCount++;
                }
            }

            int vIdx = (cellX + cellZ * length) * 6;

            byte baseEnc = EncodeType(baseType);
            byte o1Enc = EncodeType(o1);
            byte o2Enc = EncodeType(o2);
            byte o3Enc = EncodeType(o3);
            Color32 c = new Color32(baseEnc, o1Enc, o2Enc, o3Enc);
            for (int i = 0; i < 6; i++)
                _colors[vIdx + i] = c;

            SetCellOverlayUV(_uv1, vIdx, overlayCount >= 1 ? ComputeMSIndex(tBL, tTL, tTR, tBR, o1) : 0);
            SetCellOverlayUV(_uv2, vIdx, overlayCount >= 2 ? ComputeMSIndex(tBL, tTL, tTR, tBR, o2) : 0);
            SetCellOverlayUV(_uv3, vIdx, overlayCount >= 3 ? ComputeMSIndex(tBL, tTL, tTR, tBR, o3) : 0);
        }

        static int ComputeMSIndex(byte tBL, byte tTL, byte tTR, byte tBR, byte overlayType)
        {
            int idx = 0;
            if (tBL >= overlayType) idx |= 8;
            if (tTL >= overlayType) idx |= 4;
            if (tTR >= overlayType) idx |= 2;
            if (tBR >= overlayType) idx |= 1;
            return idx;
        }

        const float HalfPixel = 0.5f / 256f;

        static void SetCellOverlayUV(Vector2[] uvs, int vIdx, int msIndex)
        {
            int tileX = msIndex % 4;
            int tileY = msIndex / 4;
            Vector2 min = new Vector2(tileX * TileUVSize.x + HalfPixel, tileY * TileUVSize.y + HalfPixel);
            Vector2 max = new Vector2((tileX + 1) * TileUVSize.x - HalfPixel, (tileY + 1) * TileUVSize.y - HalfPixel);

            uvs[vIdx + 0] = min;
            uvs[vIdx + 1] = new Vector2(min.x, max.y);
            uvs[vIdx + 2] = new Vector2(max.x, min.y);
            uvs[vIdx + 3] = new Vector2(max.x, min.y);
            uvs[vIdx + 4] = new Vector2(min.x, max.y);
            uvs[vIdx + 5] = max;
        }

        static byte Min4(byte a, byte b, byte c, byte d)
        {
            byte min = a;
            if (b < min) min = b;
            if (c < min) min = c;
            if (d < min) min = d;
            return min;
        }

        /// <summary>
        /// 相邻格点最大允许高差（超过则 BFS 传播约束）。
        /// 由外部（MCTerrain.Init）从 BuildingConst.TerrainMaxHeightDiff 注入。
        /// </summary>
        public int MaxHeightDiff { get; set; } = 1;

        /// <summary>获取指定格点的高度值（terrain 本地网格单位）。</summary>
        public sbyte GetPointHeight(int x, int z)
        {
            x = Mathf.Clamp(x, 0, length);
            z = Mathf.Clamp(z, 0, width);
            return _points[x, z].high;
        }

        public bool BrushMapHigh(Brush brush, int delta)
        {
            (Vector3 center, float radiusSqr) = CalculateArea(brush, out int minX,
                out int minZ, out int maxX, out int maxZ);

            var queue = new Queue<(int x, int z)>();
            bool dirty = false;

            for (int x = minX; x <= maxX; x++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector2 d = new Vector2(x - center.x, z - center.z);
                    if (d.sqrMagnitude <= radiusSqr && SetPointHeightDelta(x, z, delta))
                    {
                        queue.Enqueue((x, z));
                        dirty = true;
                    }
                }
            }

            if (dirty)
            {
                EnforceHeightConstraint(queue);
                mesh.vertices = _vertices;
                mesh.RecalculateNormals();
                RebuildCliffMesh();
            }
            return dirty;
        }

        static readonly (int dx, int dz)[] _neighbors4 = { (-1, 0), (1, 0), (0, -1), (0, 1) };

        private void EnforceHeightConstraint(Queue<(int x, int z)> queue)
        {
            while (queue.Count > 0)
            {
                var (px, pz) = queue.Dequeue();
                int h = _points[px, pz].high;

                foreach (var (dx, dz) in _neighbors4)
                {
                    int nx = px + dx, nz = pz + dz;
                    if (nx < 0 || nx > length || nz < 0 || nz > width) continue;

                    int nh   = _points[nx, nz].high;
                    int diff = h - nh;

                    int target;
                    if      (diff >  MaxHeightDiff) target = h - MaxHeightDiff;
                    else if (diff < -MaxHeightDiff) target = h + MaxHeightDiff;
                    else continue;

                    target = Mathf.Clamp(target, -64, 64);
                    if (target == nh) continue;

                    ApplyPointHeight(nx, nz, (sbyte)target);
                    queue.Enqueue((nx, nz));
                }
            }
        }

        private (Vector3, float) CalculateArea(Brush brush, out int minX, out int minZ, out int maxX, out int maxZ)
        {
            float radius = brush.Size * 0.5f;
            Vector3 half = Vector3.one * radius;
            float radiusSqr = radius * radius;
            Vector3 center = worldToLocal.MultiplyPoint(brush.transform.position);
            Vector3 min = center - half;
            Vector3 max = center + half;
            minX = Mathf.Clamp(Mathf.CeilToInt(min.x), 0, length);
            minZ = Mathf.Clamp(Mathf.CeilToInt(min.z), 0, width);
            maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 0, length);
            maxZ = Mathf.Clamp(Mathf.FloorToInt(max.z), 0, width);
            return (center, radiusSqr);
        }

        // 顶点更新逻辑（BrushMapHigh 和 EnforceHeightConstraint 共用）
        private void ApplyPointHeight(int x, int z, sbyte newHigh)
        {
            _points[x, z].high = newHigh;
            Vector3 p = new Vector3(x, newHigh, z);

            if (x > 0 && z > 0)
                _vertices[(x - 1 + (z - 1) * length) * 6 + 5] = p;
            if (x > 0 && z < width)
            {
                int index = (x - 1 + z * length) * 6;
                _vertices[index + 2] = p;
                _vertices[index + 3] = p;
            }
            if (x < length && z < width)
                _vertices[(x + z * length) * 6 + 0] = p;
            if (x < length && z > 0)
            {
                int index = (x + (z - 1) * length) * 6;
                _vertices[index + 1] = p;
                _vertices[index + 4] = p;
            }
        }

        private bool SetPointHeightDelta(int x, int z, int d)
        {
            ref var chunk = ref _points[x, z];
            sbyte high = (sbyte)Mathf.Clamp(d + chunk.high, -64, 64);
            if (high == chunk.high) return false;
            ApplyPointHeight(x, z, high);
            return true;
        }

        private void DetectChunkUpdated(int x, int z, int d)
        {
            SetPointHeightDelta(x, z, d);
        }

        const int CliffSegX = 8;
        const int CliffSegPerLevel = 8;
        const float CliffDepth = 0.25f;

        private static CliffTemplate _cliff1;
        private static CliffTemplate _cliff2;

        struct CliffTemplate
        {
            public Vector3[] vertices;
            public Vector2[] uvs;
            public int[] triangles;
        }

        static CliffTemplate GenerateCliffTemplate(int levels)
        {
            int segY = CliffSegPerLevel * levels;
            int vertCount = (CliffSegX + 1) * (segY + 1);
            var verts = new Vector3[vertCount];
            var uvs = new Vector2[vertCount];
            var tris = new int[CliffSegX * segY * 6];

            float seed = levels * 13.7f;

            for (int j = 0; j <= segY; j++)
            {
                for (int i = 0; i <= CliffSegX; i++)
                {
                    int idx = j * (CliffSegX + 1) + i;
                    float u = (float)i / CliffSegX;
                    float v = (float)j / segY;

                    float edgeFalloff = Mathf.Sin(u * Mathf.PI);
                    float vertFalloff = Mathf.Sin(v * Mathf.PI);
                    float falloff = edgeFalloff * vertFalloff;

                    float n1 = Mathf.PerlinNoise(u * 4f + seed, v * 4f * levels + seed);
                    float n2 = Mathf.PerlinNoise(u * 8f + seed + 50f, v * 8f * levels + seed + 50f);
                    float d = (n1 * 0.7f + n2 * 0.3f - 0.3f) * CliffDepth * falloff;

                    verts[idx] = new Vector3(u, v * levels, d);
                    uvs[idx] = new Vector2(u, v * levels);
                }
            }

            int tri = 0;
            for (int j = 0; j < segY; j++)
            {
                for (int i = 0; i < CliffSegX; i++)
                {
                    int v00 = j * (CliffSegX + 1) + i;
                    int v10 = v00 + 1;
                    int v01 = v00 + (CliffSegX + 1);
                    int v11 = v01 + 1;

                    tris[tri++] = v00;
                    tris[tri++] = v01;
                    tris[tri++] = v10;
                    tris[tri++] = v10;
                    tris[tri++] = v01;
                    tris[tri++] = v11;
                }
            }

            return new CliffTemplate { vertices = verts, uvs = uvs, triangles = tris };
        }

        static void EnsureCliffTemplates()
        {
            if (_cliff1.vertices != null) return;
            _cliff1 = GenerateCliffTemplate(1);
            _cliff2 = GenerateCliffTemplate(2);
        }

        public void RebuildCliffMesh()
        {
            EnsureCliffTemplates();
            _cliffVertices.Clear();
            _cliffTriangles.Clear();
            _cliffUVs.Clear();

            for (int z = 0; z <= width; z++)
                for (int x = 0; x < length; x++)
                    TryAddCliffWall(x, z, x + 1, z, true);

            for (int x = 0; x <= length; x++)
                for (int z = 0; z < width; z++)
                    TryAddCliffWall(x, z, x, z + 1, false);

            cliffMesh.Clear();
            cliffMesh.SetVertices(_cliffVertices);
            cliffMesh.SetTriangles(_cliffTriangles, 0);
            cliffMesh.SetUVs(0, _cliffUVs);
            cliffMesh.RecalculateNormals();
        }

        private void TryAddCliffWall(int x0, int z0, int x1, int z1, bool isHorizontal)
        {
            int h0 = _points[x0, z0].high;
            int h1 = _points[x1, z1].high;
            if (h0 == h1) return;

            int minH = Mathf.Min(h0, h1);
            int maxH = Mathf.Max(h0, h1);

            Vector3 right, forward;
            if (isHorizontal)
            {
                right = new Vector3(1, 0, 0);
                forward = (h0 < h1) ? new Vector3(0, 0, -1) : new Vector3(0, 0, 1);
            }
            else
            {
                right = new Vector3(0, 0, 1);
                forward = (h0 > h1) ? new Vector3(-1, 0, 0) : new Vector3(1, 0, 0);
            }

            Vector3 origin = new Vector3(x0, 0, z0);
            int currentH = minH;

            while (currentH < maxH)
            {
                int levels = Mathf.Min(maxH - currentH, 2);
                if (maxH - currentH > 2) levels = 1;

                ref CliffTemplate tmpl = ref (levels == 2 ? ref _cliff2 : ref _cliff1);
                AppendCliffTemplate(ref tmpl, origin, right, forward, currentH);

                currentH += levels;
            }
        }

        private void AppendCliffTemplate(ref CliffTemplate tmpl, Vector3 origin,
            Vector3 right, Vector3 forward, int heightOffset)
        {
            int baseVert = _cliffVertices.Count;

            for (int i = 0; i < tmpl.vertices.Length; i++)
            {
                Vector3 v = tmpl.vertices[i];
                Vector3 worldPos = origin
                    + right * v.x
                    + Vector3.up * (v.y + heightOffset)
                    + forward * v.z;
                _cliffVertices.Add(worldPos);
                _cliffUVs.Add(tmpl.uvs[i]);
            }

            for (int i = 0; i < tmpl.triangles.Length; i++)
                _cliffTriangles.Add(baseVert + tmpl.triangles[i]);
        }

        public void OnDrawGizmos()
        {
            Gizmos.color = Color.red;
            for (int i = 0; i <= length; i++)
            {
                for (int j = 0; j <= width; j++)
                {
                    ref readonly var point1 = ref _points[i, j];
                    Vector3 p1 = new Vector3(i, point1.high, j);
                    Gizmos.DrawSphere(p1, 0.05F);
                }
            }
        }
    }
}
