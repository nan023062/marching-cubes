using System.Collections.Generic;
using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 程序化地形 mesh 生成器（类比 CubeMesh）。
    /// 持有 TilePoint[,] 高度数据，Rebuild() 时为每个格子生成两个三角面，
    /// 输出单张连续 Mesh 供 MeshFilter / MeshCollider 使用。
    /// </summary>
    public sealed class TileTerrain
    {
        public readonly int Width, Length;

        private readonly TilePoint[,] _points; // [length+1, width+1]

        private readonly List<Vector3> _vertices  = new List<Vector3>(256);
        private readonly List<int>     _triangles = new List<int>(512);
        private readonly List<Vector2> _uvs       = new List<Vector2>(256);

        public Mesh mesh;

        private ISquareTerrainReceiver _receiver;

        public TileTerrain(int width, int length, ISquareTerrainReceiver receiver = null)
        {
            Width    = width;
            Length   = length;
            _receiver = receiver;

            _points = new TilePoint[length + 1, width + 1];
            for (int x = 0; x <= length; x++)
            for (int z = 0; z <= width; z++)
                _points[x, z] = new TilePoint(x, z);

            mesh = new Mesh { name = "TileTerrain" };
            Rebuild();
        }

        // ── 数据写入 ─────────────────────────────────────────────────────────

        public void SetHeight(int x, int z, sbyte high)
        {
            x = Mathf.Clamp(x, 0, Length);
            z = Mathf.Clamp(z, 0, Width);
            _points[x, z].high = high;
        }

        public void SetTerrainType(int x, int z, byte type)
        {
            x = Mathf.Clamp(x, 0, Length);
            z = Mathf.Clamp(z, 0, Width);
            _points[x, z].terrainType = type;
        }

        public ref TilePoint GetPoint(int x, int z) => ref _points[x, z];

        // ── 重建 Mesh ─────────────────────────────────────────────────────────

        /// <summary>
        /// 全量重建。每个格子生成两个三角面（不共享顶点，法线独立）。
        /// UV0 = 格点坐标 (x, z)，供 shader 采样底层纹理。
        /// </summary>
        public void Rebuild()
        {
            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();

            for (int x = 0; x < Length; x++)
            for (int z = 0; z < Width; z++)
            {
                ref TilePoint p0 = ref _points[x,     z    ]; // BL
                ref TilePoint p1 = ref _points[x + 1, z    ]; // BR
                ref TilePoint p2 = ref _points[x + 1, z + 1]; // TR
                ref TilePoint p3 = ref _points[x,     z + 1]; // TL

                int idx = _vertices.Count;

                _vertices.Add(new Vector3(x,     p0.high, z    ));
                _vertices.Add(new Vector3(x + 1, p1.high, z    ));
                _vertices.Add(new Vector3(x + 1, p2.high, z + 1));
                _vertices.Add(new Vector3(x,     p3.high, z + 1));

                _uvs.Add(new Vector2(x,     z    ));
                _uvs.Add(new Vector2(x + 1, z    ));
                _uvs.Add(new Vector2(x + 1, z + 1));
                _uvs.Add(new Vector2(x,     z + 1));

                // Tri 1: BL–BR–TR
                _triangles.Add(idx);
                _triangles.Add(idx + 1);
                _triangles.Add(idx + 2);

                // Tri 2: BL–TR–TL
                _triangles.Add(idx);
                _triangles.Add(idx + 2);
                _triangles.Add(idx + 3);
            }

            mesh.Clear();
            mesh.SetVertices(_vertices);
            mesh.SetUVs(0, _uvs);
            mesh.SetTriangles(_triangles, 0);
            mesh.RecalculateNormals();

            _receiver?.OnRebuildCompleted(mesh);
        }

        /// <summary>只更新受影响格子周围顶点的 Y 值，不重建三角拓扑。</summary>
        public void RebuildHeightOnly()
        {
            var verts = mesh.vertices;
            int idx   = 0;

            for (int x = 0; x < Length; x++)
            for (int z = 0; z < Width; z++)
            {
                verts[idx]     = new Vector3(x,     _points[x,     z    ].high, z    );
                verts[idx + 1] = new Vector3(x + 1, _points[x + 1, z    ].high, z    );
                verts[idx + 2] = new Vector3(x + 1, _points[x + 1, z + 1].high, z + 1);
                verts[idx + 3] = new Vector3(x,     _points[x,     z + 1].high, z + 1);
                idx += 4;
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
            _receiver?.OnRebuildCompleted(mesh);
        }
    }
}
