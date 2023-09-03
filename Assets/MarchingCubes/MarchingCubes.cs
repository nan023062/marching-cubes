//****************************************************************************
// File: MarchingCubes.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************

using System.Collections.Generic;
using MarchingCubes.Sample;
using UnityEngine;
using Random = UnityEngine.Random;

namespace MarchingCubes
{
    public partial class MarchingCubes
    {
        private readonly int X, Y, Z;
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
            _points = new Point[X - 1, Y - 1, Z - 1];
            for (int i = 0; i < X - 1; i++)
            {
                for (int j = 0; j < Y - 1; j++)
                {
                    for (int k = 0; k < Z - 1; k++)
                        _points[i, j, k] = new Point(i, j, k, Point.Max);
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

        private void RebuildMesh()
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
                        int count = Polygonise(cube, 5, tempTriangles);
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
            if (vertexCount > 0)
            {
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

        public void DrawGizmos()
        {
            Color color = Gizmos.color;
            Gizmos.matrix = _localToWorld;
            for (int x = 0; x < X - 1; x++)
            {
                for (int y = 0; y < Y - 1; y++)
                {
                    for (int z = 0; z < Z - 1; z++)
                    {
                        ref readonly var point = ref _points[x, y, z];
                        Gizmos.color = point.value == 0 ? Color.red : Color.green;
                        Gizmos.DrawCube(new Vector3(x + 1, y + 1, z + 1), Vector3.one * 0.02F);
                    }
                }
            }

            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = new Color(1, 0, 0, 0.3f);
            Gizmos.DrawSphere(Vector3.one, 0.05f);
            Gizmos.color = color;
        }

        public bool Brush(Brush brush, bool add)
        {
            Test(add);
            return true;
            /*
          bool dirty = false;
        
          (Vector3 center, float radiusSqr) = CalculateArea(brush, out Point min, out Point max);
          for (int x = min.x; x <= max.x; x++)
          {
              for (int y = min.y; y <= max.y; y++)
              {
                  for (int z = min.z; z <= max.z; z++)
                  {
                      Vector2 d = new Vector2(x - center.x, z - center.z);
                      if (d.sqrMagnitude <= radiusSqr)
                      {
                          ref var point = ref _points[x, y, z];
                          sbyte value = add ? (sbyte)0 : sbyte.MaxValue;
                          if (value != point.value)
                          {
                              dirty = true;
                              point.value = value;
                          }
                      } 
                  }
              }
          }
          
          if (dirty)
          {
              RebuildMesh();
          }
          return dirty;
          */
        }

        private int _index = -1;

        public void Test(bool add)
        {
            
            int center = (X - 1) / 2;
            float range = Random.Range(1, center);
            float rangeSqr = range * range;
            int minX = Mathf.Clamp(Mathf.CeilToInt(center - range), 0, X - 2);
            int maxX = Mathf.Clamp(Mathf.FloorToInt(center + range), 0, X - 2);
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minX; y <= maxX; y++)
                {
                    for (int z = minX; z <= maxX; z++)
                    {
                        Vector3 d = new Vector3(x - center, y - center, z - center);
                        if (d.sqrMagnitude <= rangeSqr)
                        {
                            ref var point = ref _points[x, y, z];
                            point.value = 0;
                        }
                    }
                }
            }

            RebuildMesh();
        }

        private (Vector3, float) CalculateArea(Brush brush, out Point pMin, out Point pMax)
        {
            float radius = brush.Size * 0.5f;
            Vector3 half = Vector3.one * radius;
            float radiusSqr = radius * radius;
            Vector3 center = localToWorld.inverse.MultiplyPoint(brush.transform.position);
            Vector3 min = center - half;
            Vector3 max = center + half;
            int minX = Mathf.Clamp(Mathf.CeilToInt(min.x), 0, X);
            int minY = Mathf.Clamp(Mathf.CeilToInt(min.y), 0, Y);
            int minZ = Mathf.Clamp(Mathf.CeilToInt(min.z), 0, Z);
            int maxX = Mathf.Clamp(Mathf.FloorToInt(max.x), 0, Y);
            int maxY = Mathf.Clamp(Mathf.FloorToInt(max.y), 0, Z);
            int maxZ = Mathf.Clamp(Mathf.FloorToInt(max.z), 0, Z);
            pMin = _points[minX, minY, minZ];
            pMax = _points[maxX, maxY, maxZ];
            return (center, radiusSqr);
        }
    }
}