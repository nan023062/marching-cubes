using UnityEngine;

namespace MarchingCubes
{
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
                    ref (int x, int y, int z) v = ref PolygonTable.cubeVertex[index];
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