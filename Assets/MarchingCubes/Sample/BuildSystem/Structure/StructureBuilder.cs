//****************************************************************************
// File: McStructureBuilder.cs
// Author: Li Nan
// Date: 2023-09-10 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;

namespace MarchingCubes.Sample
{
    public class StructureBuilder
    {
        public readonly int X, Y, Z;
        private readonly Point[,,] _points;
        private readonly Cube[,,]  _cubes;
        private bool[,]            _quadActive;
        private int[,]             _quadBaseH;
        private Matrix4x4 _localToWorld;
        public readonly Structure MeshStore;
        
        public Matrix4x4 localToWorld
        {
            get => _localToWorld;
            set => _localToWorld = value;
        }

        struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly StructureBuilder building;
            public GameObject mesh;

            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeTable.Vertices[index];
                    return ref building._points[x + v.x, y + v.y, z + v.z];
                }
            }

            public Cube(StructureBuilder building, int x, int y, int z)
            {
                this.building = building;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
                mesh = null;
            }
        }

        public void DrawPoints()
        {
            Color color = Gizmos.color;
            Gizmos.matrix = localToWorld;

            foreach (var point in _points)
            {
                Gizmos.color = point.iso > 0 ? Color.red : Color.green;
                Gizmos.DrawSphere(point.position, 0.1f);
            }

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.identity;
        }

        public StructureBuilder(int x, int y, int z, Matrix4x4 localToWorld, Structure meshStore)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            MeshStore = meshStore;
            _localToWorld = localToWorld;
            _points = new Point[X + 1, Y + 1, Z + 1];
            for (int i = 0; i <= X; i++)
            {
                for (int j = 0; j <= Y; j++)
                {
                    for (int k = 0; k <= Z; k++)
                        _points[i, j, k] = new Point(i, j, k);
                }
            }

            _cubes = new Cube[X, Y, Z];
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                        _cubes[i, j, k] = new Cube(this, i, j, k);
                }
            }

            _quadActive = new bool[X, Z];
            _quadBaseH  = new int [X, Z];
        }
        
        public void RefreshAllMeshes()
        {
            for (int i = 0; i < X; i++)
            for (int j = 0; j < Y; j++)
            for (int k = 0; k < Z; k++)
            {
                ref var cube = ref _cubes[i, j, k];
                int cubeIndex = 0;
                for (int v = 0; v < CubeTable.VertexCount; v++)
                    if (cube[v].iso > 0.5f)
                        cubeIndex |= 1 << v;

                var oldMesh = cube.mesh;
                if (oldMesh != null)
                {
                    cube.mesh = null;
                    Object.DestroyImmediate(oldMesh);
                }

                cube.mesh = MeshStore.GetMesh(cubeIndex);
                if (cube.mesh != null)
                {
                    Transform t = cube.mesh.transform;
                    Vector3 pos = _localToWorld.MultiplyPoint(new Vector3(i, j, k));
                    t.SetPositionAndRotation(pos, Quaternion.identity);
                    t.localScale = Vector3.one;
                }
            }
        }

        public void SetQuadActive(int cx, int cz, bool active, int baseH = 0)
        {
            if (cx < 0 || cx >= X || cz < 0 || cz >= Z) return;
            _quadActive[cx, cz] = active;
            _quadBaseH[cx, cz]  = baseH;
        }

        public void DrawGizmos()
        {
            var prevMatrix = Gizmos.matrix;
            Gizmos.matrix = _localToWorld;

            // 外露 cube 面（灰色）
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            for (int x = 0; x <= X; x++)
            for (int y = 0; y <= Y; y++)
            for (int z = 0; z <= Z; z++)
            {
                if (!IsActive(x, y, z)) continue;
                if (!IsActive(x + 1, y,     z    )) DrawFace(new Vector3(x + 0.5f, y,        z       ), Vector3.forward, Vector3.up);
                if (!IsActive(x - 1, y,     z    )) DrawFace(new Vector3(x - 0.5f, y,        z       ), Vector3.forward, Vector3.up);
                if (!IsActive(x,     y + 1, z    )) DrawFace(new Vector3(x,        y + 0.5f, z       ), Vector3.right,   Vector3.forward);
                if (!IsActive(x,     y - 1, z    )) DrawFace(new Vector3(x,        y - 0.5f, z       ), Vector3.right,   Vector3.forward);
                if (!IsActive(x,     y,     z + 1)) DrawFace(new Vector3(x,        y,        z + 0.5f), Vector3.right,   Vector3.up);
                if (!IsActive(x,     y,     z - 1)) DrawFace(new Vector3(x,        y,        z - 0.5f), Vector3.right,   Vector3.up);
            }

            // 地面 quad 面（灰色），被 cube 占据时不显示
            Gizmos.color = new Color(1f, 1f, 1f, 0.35f);
            for (int cx = 0; cx < X; cx++)
            for (int cz = 0; cz < Z; cz++)
            {
                if (!_quadActive[cx, cz]) continue;
                float y = _quadBaseH[cx, cz];
                if (IsActive(cx + 1, Mathf.RoundToInt(y) + 1, cz + 1)) continue;
                DrawFace(new Vector3(cx + 1f, y + 0.5f, cz + 1f), Vector3.right, Vector3.forward);
            }

            Gizmos.matrix = prevMatrix;
        }

        private bool IsActive(int x, int y, int z)
        {
            if (x < 0 || x > X || y < 0 || y > Y || z < 0 || z > Z) return false;
            return _points[x, y, z].iso > 0.5f;
        }

        static void DrawFace(Vector3 center, Vector3 right, Vector3 up)
        {
            var a = center - right * 0.5f - up * 0.5f;
            var b = center + right * 0.5f - up * 0.5f;
            var c = center + right * 0.5f + up * 0.5f;
            var d = center - right * 0.5f + up * 0.5f;
            Gizmos.DrawLine(a, b);
            Gizmos.DrawLine(b, c);
            Gizmos.DrawLine(c, d);
            Gizmos.DrawLine(d, a);
        }

        public bool IsPointActive(int x, int y, int z)
        {
            if (x < 0 || x > X || y < 0 || y > Y || z < 0 || z > Z) return false;
            return _points[x, y, z].iso > 0.5f;
        }

        // 将所有活跃 MC point 的暴露面追加到 verts/tris（Structure 本地坐标）
        // MC point (cx,cy,cz) 的视觉 block 占 [cx-1,cx] x [cy-1,cy] x [cz-1,cz]
        public void AppendExposedFaces(System.Collections.Generic.List<Vector3> verts,
                                       System.Collections.Generic.List<int>     tris)
        {
            for (int cx = 1; cx <= X; cx++)
            for (int cy = 1; cy <= Y; cy++)
            for (int cz = 1; cz <= Z; cz++)
            {
                if (!IsPointActive(cx, cy, cz)) continue;

                if (!IsPointActive(cx + 1, cy, cz)) // +X
                    AddFace(verts, tris,
                        new Vector3(cx, cy-1, cz-1), new Vector3(cx, cy, cz-1),
                        new Vector3(cx, cy, cz),     new Vector3(cx, cy-1, cz));

                if (!IsPointActive(cx - 1, cy, cz)) // -X
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz),   new Vector3(cx-1, cy, cz),
                        new Vector3(cx-1, cy, cz-1),   new Vector3(cx-1, cy-1, cz-1));

                if (!IsPointActive(cx, cy + 1, cz)) // +Y
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy, cz-1), new Vector3(cx-1, cy, cz),
                        new Vector3(cx, cy, cz),     new Vector3(cx, cy, cz-1));

                if (!IsPointActive(cx, cy - 1, cz)) // -Y
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz),   new Vector3(cx, cy-1, cz),
                        new Vector3(cx, cy-1, cz-1),   new Vector3(cx-1, cy-1, cz-1));

                if (!IsPointActive(cx, cy, cz + 1)) // +Z
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz), new Vector3(cx, cy-1, cz),
                        new Vector3(cx, cy, cz),     new Vector3(cx-1, cy, cz));

                if (!IsPointActive(cx, cy, cz - 1)) // -Z
                    AddFace(verts, tris,
                        new Vector3(cx-1, cy-1, cz-1), new Vector3(cx-1, cy, cz-1),
                        new Vector3(cx, cy, cz-1),     new Vector3(cx, cy-1, cz-1));
            }
        }

        static void AddFace(System.Collections.Generic.List<Vector3> verts,
                            System.Collections.Generic.List<int>     tris,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
            tris.Add(i); tris.Add(i+1); tris.Add(i+2);
            tris.Add(i); tris.Add(i+2); tris.Add(i+3);
        }

        public void SetPointStatus(int x, int y, int z, bool active)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].iso = active ? 1 : 0;
            
            int minX = Mathf.Clamp(x -1, 0, X - 1);
            int maxX = Mathf.Clamp(x, 0, X - 1);
            int minY = Mathf.Clamp(y - 1, 0, Y - 1);
            int maxY = Mathf.Clamp(y, 0, Y - 1);
            int minZ = Mathf.Clamp(z - 1, 0, Z - 1);
            int maxZ = Mathf.Clamp(z, 0, Z - 1);
            
            for (int i = minX; i <= maxX; i++)
            {
                for (int j = minY; j <= maxY; j++)
                {
                    for (int k = minZ; k <= maxZ; k++)
                    {
                        ref var cube = ref _cubes[i, j, k];
                        int cubeIndex = 0;
                        for (int v = 0; v < CubeTable.VertexCount; v++)
                        {
                            ref readonly var point = ref cube[v];
                            if (point.iso > 0.5f)
                                cubeIndex |= 1 << v;
                        }

                        var oldMesh = cube.mesh;
                        if (oldMesh != null)
                        {
                            cube.mesh = null;
                            Object.DestroyImmediate(oldMesh);
                        }
                        
                        cube.mesh = MeshStore.GetMesh(cubeIndex);
                        if (null != cube.mesh)
                        {
                            Transform transform = cube.mesh.transform;
                            Vector3 position = localToWorld.MultiplyPoint(new Vector3(i, j, k));
                            transform.SetPositionAndRotation(position, Quaternion.identity);
                            transform.localScale = Vector3.one;
                        }
                    }
                }
            }
        }
    }
}