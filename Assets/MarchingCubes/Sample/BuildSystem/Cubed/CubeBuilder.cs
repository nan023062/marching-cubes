using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class CubeBuilder : BuilderBase
    {
        public readonly int X, Y, Z;

        private readonly Point[,,] _points;
        private readonly Cube[,,]  _cubes;
        private bool[,] _quadActive;
        private int[,]  _quadBaseH;

        struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly CubeBuilder building;

            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeTable.Vertices[index];
                    return ref building._points[x + v.x, y + v.y, z + v.z];
                }
            }

            public Cube(CubeBuilder building, int x, int y, int z)
            {
                this.building = building;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }

        public CubeBuilder(int x, int y, int z, Matrix4x4 matrix)
        {
            X = x; Y = y; Z = z;
            localToWorld = matrix;

            _points = new Point[X + 1, Y + 1, Z + 1];
            for (int i = 0; i <= X; i++)
            for (int j = 0; j <= Y; j++)
            for (int k = 0; k <= Z; k++)
                _points[i, j, k] = new Point(i, j, k);

            _cubes = new Cube[X, Y, Z];
            for (int i = 0; i < X; i++)
            for (int j = 0; j < Y; j++)
            for (int k = 0; k < Z; k++)
                _cubes[i, j, k] = new Cube(this, i, j, k);

            _quadActive = new bool[X, Z];
            _quadBaseH  = new int [X, Z];
        }

        // ── 数据查询 ──────────────────────────────────────────────────────────

        public bool IsPointActive(int x, int y, int z)
        {
            if (x < 0 || x > X || y < 0 || y > Y || z < 0 || z > Z) return false;
            return _points[x, y, z].iso > 0.5f;
        }

        public int GetCubeIndex(int i, int j, int k)
        {
            int cubeIndex = 0;
            ref var cube = ref _cubes[i, j, k];
            for (int v = 0; v < CubeTable.VertexCount; v++)
                if (cube[v].iso > 0.5f)
                    cubeIndex |= 1 << v;
            return cubeIndex;
        }

        public bool IsQuadActive(int cx, int cz)
        {
            if (cx < 0 || cx >= X || cz < 0 || cz >= Z) return false;
            return _quadActive[cx, cz];
        }

        public int GetQuadBaseH(int cx, int cz) => _quadBaseH[cx, cz];

        // ── 数据写入 ──────────────────────────────────────────────────────────

        public void SetPointStatus(int x, int y, int z, bool active)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].iso = active ? 1 : 0;
        }

        public void SetQuadActive(int cx, int cz, bool active, int baseH = 0)
        {
            if (cx < 0 || cx >= X || cz < 0 || cz >= Z) return;
            _quadActive[cx, cz] = active;
            _quadBaseH[cx, cz]  = baseH;
        }

        // ── 碰撞网格数据生成 ──────────────────────────────────────────────────

        public void AppendExposedFaces(List<Vector3> verts, List<int> tris)
        {
            for (int cx = 1; cx <= X; cx++)
            for (int cy = 1; cy <= Y; cy++)
            for (int cz = 1; cz <= Z; cz++)
            {
                if (!IsPointActive(cx, cy, cz)) continue;

                if (!IsPointActive(cx + 1, cy, cz))
                    AddFace(verts, tris,
                        new Vector3(cx, cy-1, cz-1), new Vector3(cx, cy, cz-1),
                        new Vector3(cx, cy, cz),     new Vector3(cx, cy-1, cz));

                if (!IsPointActive(cx - 1, cy, cz))
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz),   new Vector3(cx-1, cy, cz),
                        new Vector3(cx-1, cy, cz-1),   new Vector3(cx-1, cy-1, cz-1));

                if (!IsPointActive(cx, cy + 1, cz))
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy, cz-1), new Vector3(cx-1, cy, cz),
                        new Vector3(cx, cy, cz),     new Vector3(cx, cy, cz-1));

                if (!IsPointActive(cx, cy - 1, cz))
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz),   new Vector3(cx, cy-1, cz),
                        new Vector3(cx, cy-1, cz-1),   new Vector3(cx-1, cy-1, cz-1));

                if (!IsPointActive(cx, cy, cz + 1))
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz), new Vector3(cx, cy-1, cz),
                        new Vector3(cx, cy, cz),     new Vector3(cx-1, cy, cz));

                if (!IsPointActive(cx, cy, cz - 1))
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz-1), new Vector3(cx-1, cy, cz-1),
                        new Vector3(cx, cy, cz-1),     new Vector3(cx, cy-1, cz-1));
            }
        }

        static void AddFace(List<Vector3> verts, List<int> tris,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i+1); tris.Add(i+2);
            tris.Add(i); tris.Add(i+2); tris.Add(i+3);
        }
    }
}
