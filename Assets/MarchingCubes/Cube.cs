using System.Runtime.CompilerServices;
using UnityEngine;

namespace MarchingCubes
{
    public partial class MarchingCubes
    {
        public struct Point
        {
            public static readonly sbyte Min = 0;
            public static readonly sbyte Max = 10;
            
            public readonly sbyte x, y, z;
            public sbyte value;

            public Vector3 position => new (x + 1, y + 1, z + 1);
            
            public Point(int x, int y, int z, sbyte value)
            {
                this.value = value;
                this.x = (sbyte)x;
                this.y = (sbyte)y;
                this.z = (sbyte)z;
            }
        }


        readonly struct Cube
        {
            public readonly sbyte x, y, z;
            private readonly MarchingCubes cubes;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public Point Get(int index)
            {
                ref (int x, int y, int z) v = ref cubeVertex[index];
                int x0 = x + v.x;
                int y0 = y + v.y;
                int z0 = z + v.z;
                
                if (x0 < 0 || y0 < 0 || z0 < 0) 
                    return new Point(x0, y0, z0, Point.Max);
                if (x0 >= cubes.X - 1 || y0 >= cubes.X - 1 || z0 >= cubes.X - 1) 
                    return new Point(x0, y0, z0, Point.Max);
                
                return cubes._points[x + v.x, y + v.y, z + v.z];
            }
            
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public sbyte GetValue(int index)
            {
                ref (int x, int y, int z) v = ref cubeVertex[index];
                int x0 = x + v.x;
                int y0 = y + v.y;
                int z0 = z + v.z;
                
                if (x0 < 0 || y0 < 0 || z0 < 0) 
                    return Point.Max;
                if (x0 >= cubes.X - 1 || y0 >= cubes.X - 1 || z0 >= cubes.X - 1) 
                    return Point.Max;
                return cubes._points[x + v.x, y + v.y, z + v.z].value;
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