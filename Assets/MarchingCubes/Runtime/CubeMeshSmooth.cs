//****************************************************************************
// File: CubeMesh.cs
// Author: Li Nan
// Date: 2023-09-09 12:00
// Version: 1.0
//****************************************************************************

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;

namespace MarchingCubes
{
    public sealed class CubeMeshSmooth
    {
        public readonly int X, Y, Z;
        private readonly Point[,,] _points;
        private readonly Cube[,,] _cubes;
        private readonly IMarchingCubeReceiver _receiver;
        
        public Mesh mesh;
        private readonly List<int> _triangles;
        private readonly Edge[] _tempEdges;
        private readonly EdgeTriangle[] _tempTriangles;
        private int _tempVertexCount;
        private readonly Dictionary<long, EdgeVertex> _edgeVertices;
        
        public CubeMeshSmooth(int x, int y, int z, IMarchingCubeReceiver receiver)
        {
            this.X = x;
            this.Y = y;
            this.Z = z;
            this._receiver = receiver;
            _tempVertexCount = 0;
            _edgeVertices = new Dictionary<long, EdgeVertex>();
            
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
            
            _tempTriangles = new EdgeTriangle[5];
            _tempEdges = new Edge[CubeTable.EdgeCount];
            _triangles = new List<int>(128);
            mesh = new Mesh();
        }
        
        public void SetPointISO(int x, int y, int z, float iso)
        {
            x = Mathf.Clamp(x, 0, X);
            y = Mathf.Clamp(y, 0, Y);
            z = Mathf.Clamp(z, 0, Z);
            _points[x, y, z].iso = iso;
        }
        
        private int Polygon(in Cube cube, EdgeTriangle[] triangles)
        {
            int cubeIndex = 0;
            float isoLevel = _receiver.GetIsoLevel();
            for (int v = 0; v < CubeTable.VertexCount; v++)
            {
                ref readonly var point = ref cube[v];
                if (_receiver.IsoPass(point.iso))
                    cubeIndex |= 1 << v;
            }
            
            int edgeMask = CubeTable.cubeTable[cubeIndex];
            if (edgeMask == 0)
                return 0;
            
            for (int edgeIndex = 0; edgeIndex < CubeTable.EdgeCount; edgeIndex++)
            {
                if ((edgeMask & (1 << edgeIndex)) > 0)
                {
                    Edge edge = cube.GetEdge(edgeIndex);
                    _tempEdges[edgeIndex] = edge;
                    
                    // 缓存边顶点坐标
                    if(!_edgeVertices.ContainsKey(edge))
                    {
                        ref readonly var t = ref CubeTable.Edges[edgeIndex];
                        ref readonly Point p1 = ref cube[t.p1];
                        ref readonly Point p2 = ref cube[t.p2];
                        Vector3 pos = CubeTable.InterpolateVerts(p1.position, p2.position, p1.iso, p2.iso, isoLevel);
                        _edgeVertices.Add(edge, new EdgeVertex
                        {
                            vertexIndex = _tempVertexCount++,
                            vertex = new Vertex
                            {
                                position = pos,
                                // TODO: fix uv
                                //uv = new Vector2(pos.x / X, pos.z / Z)
                            },
                        });
                    }
                }
            }
            
            int nTri = 0;
            int[] cubeTri = CubeTable.triTable[cubeIndex];
            for (int i = 0; cubeTri[i] != -1; i += 3)
            {
                ref var triangle = ref triangles[nTri];
                triangle.v1 = _tempEdges[cubeTri[i + 2]];
                triangle.v2 = _tempEdges[cubeTri[i + 1]];
                triangle.v3 = _tempEdges[cubeTri[i]];
                nTri++;
            }
            
            return nTri;
        }
        
        /// <summary>
        /// 不共边上的顶点
        /// </summary>
        public void Rebuild()
        {
            Profiler.BeginSample("Rebuild---1");
            // 遍历cube， 刷新边顶点缓存和triangles
            _edgeVertices.Clear();
            _tempVertexCount = 0;
            _triangles.Clear();
            for (int i = 0; i < X; i++)
            {
                for (int j = 0; j < Y; j++)
                {
                    for (int k = 0; k < Z; k++)
                    {
                        ref readonly Cube cube = ref _cubes[i, j, k];
                        int count = Polygon(cube, _tempTriangles);
                        if (count > 0)
                        {
                            for (int l = 0; l < count; l++)
                            {
                                ref var triangle = ref _tempTriangles[l];
                                
                                _edgeVertices.TryGetValue(triangle.v1, out var edgeVertex1);
                                _triangles.Add(edgeVertex1.vertexIndex);
                                
                                _edgeVertices.TryGetValue(triangle.v2, out var edgeVertex2);
                                _triangles.Add(edgeVertex2.vertexIndex);
                                 
                                _edgeVertices.TryGetValue(triangle.v3, out var edgeVertex3);
                                _triangles.Add(edgeVertex3.vertexIndex);
                            }
                        }
                    }
                }
            }
            
            // 读取缓存的顶点，并按顺序排序
            Vector3[] vertices = new Vector3[_tempVertexCount];
            Vector2[] uvs = new Vector2[_tempVertexCount];
            if (mesh.vertexCount > _tempVertexCount)
            {
                mesh = null;
                mesh = new Mesh();
            }
            foreach (var vertex in _edgeVertices.Values)
            {
                vertices[vertex.vertexIndex] = vertex.vertex.position;
                uvs[vertex.vertexIndex] = vertex.vertex.uv;
            }
            
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = _triangles.ToArray();
            mesh.RecalculateNormals();
            Profiler.EndSample();
            _receiver.OnRebuildCompleted();
        }
        
        struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly CubeMeshSmooth mesh;
            
            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeTable.Vertices[index];
                    return ref mesh._points[x + v.x, y + v.y, z + v.z];
                }
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public readonly Edge GetEdge(int edgeIndex)
            {
                ref readonly var edgeOffset = ref CubeTable.EdgesOffset[edgeIndex];
                return new Edge(x + edgeOffset.x, y + edgeOffset.y, z + edgeOffset.z, edgeOffset.axis);
            }

            public Cube(CubeMeshSmooth mesh, int x, int y, int z)
            {
                this.mesh = mesh;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }
        
        struct EdgeTriangle
        {
            public Edge v1, v2, v3;
        }

        struct EdgeVertex
        {
            public Vertex vertex;
            public int vertexIndex;
        }
    }
}