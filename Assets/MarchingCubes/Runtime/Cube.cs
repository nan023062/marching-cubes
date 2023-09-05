using UnityEngine;

namespace MarchingCubes
{
/************算法中使用的顶点和边的索引约定****************
 
  0-11 : Edge Index
  ⓪-⑦: Vertex Index
      ④      4           ⑤
        _________________
    7 / |            5  /|
     /  |8  6         /  |9
 ⑦ /________________/⑥  | 
   |  ⓪ |___________|____|①
 11|   /      0      |10/
   |  /3             | / 1
   |/________________/
  ③      2        ②
    
 ****************************************************/

    public enum CubeEdge
    {
        E0,E1,E2,E3,E4,E5,E6,E7,E8,E9,E10,E11
    }
        
    public enum CubeVertex
    {
        V0,V1,V2,V3,V4,V5,V6,V7
    }
    
    public static class CubeConst
    {
        public const int VertexCount = 8;
        public const int EdgeCount = 12;
        public const int CubeKind = 256;
        
        /// <summary>
        /// cube 8 个顶点与顺序
        /// </summary>
        public static readonly (int x, int y, int z)[] Vertices =
            new (int x, int y, int z)[VertexCount]
            {
                (0, 0, 1),
                (1, 0, 1),
                (1, 0, 0),
                (0, 0, 0),
                (0, 1, 1),
                (1, 1, 1),
                (1, 1, 0),
                (0, 1, 0)
            };

        /// <summary>
        /// 12条边索引的顶点index
        /// </summary>
        public static readonly (int p1, int p2)[] Edges = new (int, int)[EdgeCount]
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };
    }
    
    public partial class MarchingCubes
    {
        public void DrawPoints()
        {
            Color color = Gizmos.color;
            Gizmos.matrix = localToWorld;
            
            foreach (var point in _points)
            {
                Gizmos.color = point.mark > 0 ? Color.red : Color.green;
                Gizmos.DrawSphere( point.position, 0.1f );
            }

            Gizmos.color = color;
            Gizmos.matrix = Matrix4x4.identity;
        }
        
        public struct Point
        {
            public readonly sbyte x, y, z;
            public sbyte mark;
            
            public Vector3 position => new (x, y, z);
            
            public Point(int x, int y, int z)
            {
                this.mark = 0;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }
        
        readonly struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly MarchingCubes cubes;
            
            public ref readonly Point this[int index]
            {
                get
                {
                    ref (int x, int y, int z) v = ref CubeConst.Vertices[index];
                    return ref cubes._points[x + v.x, y + v.y, z + v.z];
                }
            }

            public Cube(MarchingCubes cubes, int x, int y, int z)
            {
                this.cubes = cubes;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }
        
        public struct Vertex
        {
            public Vector3 position;
            public Vector2 uv;
        }
        
        public struct Triangle
        {
            public Vertex v1, v2, v3;
        }
    }
}