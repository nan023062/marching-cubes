//****************************************************************************
// File: MarchingCubes.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************

using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes
{
    public partial class MarchingCubes
    {
        public readonly int X, Y, Z;
        private readonly Point[,,] _points;
        private readonly Cube[,,] _cubes;
        private Matrix4x4 _localToWorld;

        public Mesh mesh;
        private readonly List<Vector3> _vertices;
        private readonly List<Vector2> _uvs;
        private readonly Triangle[] tempTriangles;
        
        public Matrix4x4 localToWorld
        {
            get => _localToWorld;
            set => _localToWorld = value;
        }

        public MarchingCubes(int x, int y, int z, Matrix4x4 localToWorld)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
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

            tempTriangles = new Triangle[5];
            _vertices = new List<Vector3>(128);
            _uvs = new List<Vector2>(128);
            mesh = new Mesh();
            RebuildMesh();
        }

        public void MarkPoint(int x, int y, int z, bool add)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].mark = (sbyte)(add ? 1 : 0);
        }
        
        public void RebuildMesh()
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
                        int count = Polygonise(cube, tempTriangles);
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
        }
    }
}