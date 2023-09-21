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

    public struct Triangle
    {
        public Vertex v1, v2, v3;
    }
}