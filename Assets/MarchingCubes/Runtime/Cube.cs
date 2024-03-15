using System;
using System.Runtime.InteropServices;
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
        E0,
        E1,
        E2,
        E3,
        E4,
        E5,
        E6,
        E7,
        E8,
        E9,
        E10,
        E11
    }

    public enum CubeVertex
    {
        V0,
        V1,
        V2,
        V3,
        V4,
        V5,
        V6,
        V7
    }
    
    [Flags]
    public enum CubeVertexMask
    {
        Null = 0x00,
        V0 = 0x01,
        V1 = 0x02,
        V2 = 0x04,
        V3 = 0x08,
        V4 = 0x10,
        V5 = 0x20,
        V6 = 0x40,
        V7 = 0x80,
        All = 0xFF
    }

    public enum Axis : byte
    {
        X,
        Y,
        Z
    }

    public struct Coord
    {
        public int x, y, z;
        
        public Coord(int x, int y, int z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }
    }
    
    public struct Point
    {
        public readonly sbyte x, y, z;
        public float iso;
        
        public Vector3 position => new(x, y, z);

        public Point(int x, int y, int z)
        {
            this.iso = 0;
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
    
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct Edge : IEquatable<Edge>
    {
        [FieldOffset(0)]
        private readonly long _index;
        
        [FieldOffset(0)]
        public readonly sbyte x;
         
        [FieldOffset(1)]
        public readonly sbyte y;
         
        [FieldOffset(2)]
        public readonly sbyte z;
        
        [FieldOffset(3)]
        public readonly Axis axis;
        
        public Edge(int x, int y, int z, Axis axis)
        {
            _index = 0;
            this.x = (sbyte)x;
            this.y = (sbyte)y;
            this.z = (sbyte)z;
            this.axis = axis;
        }
        
        Edge(long index)
        {
            this.x = 0;
            this.y = 0;
            this.z = 0;
            this.axis = Axis.X;
            _index = index;
        }
        
        public static implicit operator long(Edge edge)
        {
            return edge._index;
        }
        
        public static implicit operator Edge(long edge)
        {
            return new Edge(edge);
        }
        
        public bool Equals(Edge other)
        {
            return _index == other._index;
        }

        public override bool Equals(object obj)
        {
            return obj is Edge other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _index.GetHashCode();
        }
    }
    
    public struct Triangle
    {
        public Vertex v1, v2, v3;
    }

    public struct IntTriangle
    {
        public int v1, v2, v3;
    }
    
    public interface IMarchingCubeReceiver
    {
        float GetIsoLevel();
        
        void OnRebuildCompleted();
        
        bool IsoPass(float iso);
    }
}