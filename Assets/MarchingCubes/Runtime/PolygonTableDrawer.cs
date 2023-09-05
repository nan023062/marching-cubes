using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

namespace MarchingCubes
{
    public class PolygonTableDrawer : MonoBehaviour
    {
        public Mode mode = Mode.DrawOneCube;
        public bool V0,V1,V2,V3,V4,V5,V6,V7;
        private readonly OneCube[] _cubes = new OneCube[CubeConst.CubeKind];
        
        public enum Mode
        {
            DrawOneCube, Draw256Cube
        }
        
        private void Awake()
        {
            for (int i = 0; i < CubeConst.CubeKind; i++)
            {
                Vector3 position = new Vector3(i * 1.2f, 0f, 0f);
                _cubes[i] = new OneCube(i, position);
            }
        }

        public void OnDrawGizmos()
        {
            switch (mode)
            {
                case Mode.DrawOneCube:
                {
                    int cubeIndex = V0 ? 1 : 0;
                    if (V1) cubeIndex |= 1 << 1;
                    if (V2) cubeIndex |= 1 << 2;
                    if (V3) cubeIndex |= 1 << 3;
                    if (V4) cubeIndex |= 1 << 4;
                    if (V5) cubeIndex |= 1 << 5;
                    if (V6) cubeIndex |= 1 << 6;
                    if (V7) cubeIndex |= 1 << 7;
                    _cubes[cubeIndex]?.Draw();
                    break;
                }
                case Mode.Draw256Cube:
                {
                    for (int i = 0; i < CubeConst.CubeKind; i++)
                    {
                        _cubes[i]?.Draw();
                    }
                    break;
                }
            }
        }
        

        class OneCube
        {
            public readonly int index;
            public Vector3 position;
            private readonly Mesh mesh = new Mesh();
            
            public OneCube(int index, Vector3 vector3)
            {
                this.index = index;
                position = vector3;
                
                int edgeMask = PolygonTable.cubeTable[index];
                if (edgeMask == 0)
                    return;
            
                Vector3[] vertices = new Vector3[CubeConst.EdgeCount];
                for (int edge = 0; edge < CubeConst.EdgeCount; edge++)
                {
                    if ((edgeMask & (1 << edge)) > 0)
                    {
                        ref readonly var t = ref CubeConst.Edges[edge];
                        ref readonly var p1 = ref CubeConst.Vertices[t.p1];
                        ref readonly var p2 = ref CubeConst.Vertices[t.p2];
                        vertices[edge] = Vector3.Lerp(new Vector3(p1.x, p1.y, p1.z), new Vector3(p2.x, p2.y, p2.z), 0.5f);
                    }
                }
            
                List<Vector3> vertexList = new List<Vector3>();
                List<int> triangleList = new List<int>();
                int[] cubeTri = PolygonTable.triTable[index];
                for (int i = 0; cubeTri[i] != -1; i += 3)
                {
                    vertexList.Add(vertices[cubeTri[i + 2]]);
                    triangleList.Add(triangleList.Count);
                
                    vertexList.Add(vertices[cubeTri[i + 1]]);
                    triangleList.Add(triangleList.Count);
                
                    vertexList.Add(vertices[cubeTri[i + 0]]);
                    triangleList.Add(triangleList.Count);
                }
                
                mesh.vertices = vertexList.ToArray();
                mesh.triangles = triangleList.ToArray();
                mesh.RecalculateNormals();
            }
            
            public void Draw()
            {
                Gizmos.matrix = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                if (mesh.vertices.Length > 0)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawMesh(mesh);
                    for (int i = 0; i < CubeConst.VertexCount; i++)
                    {
                        if ((index & 1 << i) > 0)
                        {
                            Gizmos.color = Color.blue;
                            ref var p = ref CubeConst.Vertices[i];
                            Gizmos.DrawSphere(new Vector3(p.x,p.y,p.z), 0.03f);
                        }
                    }
                }
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(Vector3.one * 0.5f, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}