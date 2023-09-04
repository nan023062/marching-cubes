//****************************************************************************
// File: EdgeTable.cs
// Author: Li Nan
// Date: 2023-09-02 12:00
// Version: 1.0
//****************************************************************************
using UnityEngine;

namespace MarchingCubes
{
    public interface IMarchingMeshTable
    {
    }
    
    public partial class MarchingCubes
    {
        public const int VertexCount = 8;
        public const int EdgeCount = 12;
        public const int CubeKind = 256;
        
        private static int Polygonise(in Cube cube, Triangle[] triangles)
        {
            int cubeIndex = 0;
            for (int v = 0; v < VertexCount; v++)
            {
                if (cube[v].mark > 0)
                    cubeIndex |= 1 << v;
            }
                
            int edgeMask = PolygonTable.cubeTable[cubeIndex];
            if (edgeMask == 0)
                return 0;
            
            Vector3[] vertices = new Vector3[EdgeCount];
            for (int edge = 0; edge < EdgeCount; edge++)
            {
                if ((edgeMask & (1 << edge)) > 0)
                {
                    ref readonly var t = ref PolygonTable.edgeTable[edge];
                    ref readonly Point p1 = ref cube[t.p1];
                    ref readonly Point p2 = ref cube[t.p2];
                    vertices[edge] = Vector3.Lerp(p1.position, p2.position, 0.5f);
                }
            }

            int nTri = 0;
            int[] cubeTri = PolygonTable.triTable[cubeIndex];
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
    }
}