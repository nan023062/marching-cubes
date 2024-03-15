//****************************************************************************
// File: CubeMesh.cs
// Author: Li Nan
// Date: 2023-09-09 12:00
// Version: 1.0
//****************************************************************************

using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes
{
    public sealed class CubeMesh
    {
        public readonly int X, Y, Z;
        private readonly Point[,,] _points;
        private readonly Cube[,,] _cubes;
        private readonly IMarchingCubeReceiver _receiver;
        
        public Mesh mesh;
        private readonly List<Vector3> _vertices;
        private readonly List<Vector2> _uvs;
        private readonly Triangle[] tempTriangles;
        
        public CubeMesh(int x, int y, int z, IMarchingCubeReceiver receiver)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this._receiver = receiver;
            
            // points
            _points = new Point[X + 1, Y + 1, Z + 1];
            for (int i = 0; i <= X; i++)
            {
                for (int j = 0; j <= Y; j++)
                {
                    for (int k = 0; k <= Z; k++)
                        _points[i, j, k] = new Point(i, j, k);
                }
            }
            
            // cubes
            _cubes = new Cube[X, Y, Z];
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                        _cubes[i, j, k] = new Cube(this, i, j, k);
                }
            }
            
            tempTriangles = new Triangle[5];
            _vertices = new List<Vector3>(128);
            _uvs = new List<Vector2>(128);
            mesh = new Mesh();
            Rebuild();
        }
        
        public void SetPointISO(int x, int y, int z, float iso)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].iso = iso;
        }
        
        private int Polygon(in Cube cube, Triangle[] triangles)
        {
            int cubeIndex = 0;
            float isoLevel = _receiver.GetIsoLevel();
            for (int v = 0; v < CubeTable.VertexCount; v++)
            {
                ref readonly var point = ref cube[v];
                if (_receiver.IsoPass(point.iso))
                    cubeIndex |= 1 << v;
            }
            
            int edgeMask = CubeTable.GetCubeKindEdgeMask(cubeIndex);
            if (edgeMask == 0)
                return 0;
            
            Vector3[] vertices = new Vector3[CubeTable.EdgeCount];
            for (int edge = 0; edge < CubeTable.EdgeCount; edge++)
            {
                if ((edgeMask & (1 << edge)) > 0)
                {
                    ref readonly var t = ref CubeTable.Edges[edge];
                    ref readonly Point p1 = ref cube[t.p1];
                    ref readonly Point p2 = ref cube[t.p2];
                    vertices[edge] = CubeTable.InterpolateVerts(p1.position, p2.position, p1.iso, p2.iso, isoLevel);
                }
            }
            
            int nTri = 0;
            ref readonly int[] cubeTri = ref CubeTable.GetCubeKindTriangles(cubeIndex);
            for (int i = 0; cubeTri[i] != -1; i += 3)
            {
                ref var triangle = ref triangles[nTri];
                triangle.v1.position = vertices[cubeTri[i + 2]];
                triangle.v2.position = vertices[cubeTri[i + 1]];
                triangle.v3.position = vertices[cubeTri[i]];
                nTri++;
            }

            return nTri;
        }
        
        /// <summary>
        /// 不共边上的顶点
        /// </summary>
        public void Rebuild()
        {
            _vertices.Clear();
            _uvs.Clear();
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                    {
                        ref readonly Cube cube = ref _cubes[i, j, k];
                        int count = Polygon(cube, tempTriangles);
                        if (count > 0)
                        {
                            for (int l = 0; l < count; l++)
                            {
                                ref var triangle = ref tempTriangles[l];

                                _vertices.Add(triangle.v1.position);
                                _uvs.Add(triangle.v1.uv);

                                _vertices.Add(triangle.v2.position);
                                _uvs.Add(triangle.v2.uv);

                                _vertices.Add(triangle.v3.position);
                                _uvs.Add(triangle.v3.uv);
                            }
                        }
                    }
                }
            }

            int vertexCount = _vertices.Count;
            int[] indices = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                indices[i] = i;

            if (mesh.vertexCount > _vertices.Count)
            {
                mesh = null;
                mesh = new Mesh();
            }

            mesh.vertices = _vertices.ToArray();
            mesh.uv = _uvs.ToArray();
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            
            _receiver.OnRebuildCompleted();
        }
        
        struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly CubeMesh mesh;
            
            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeTable.Vertices[index];
                    return ref mesh._points[x + v.x, y + v.y, z + v.z];
                }
            }

            public Cube(CubeMesh mesh, int x, int y, int z)
            {
                this.mesh = mesh;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }
    }
}