using System;
using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// 0-8共9种顶点组合
    /// 共有2^8 = 256种变体情况
    /// </summary> 
    [Serializable]
    public class CubeCase
    {
        public MeshRenderer mesh;
    }
    
    public sealed class EdgeIndexAttribute : Attribute
    {
        public readonly int index;
        public EdgeIndexAttribute(int index)
        {
            this.index = index;
        }
    }

    public sealed class TriangleIndexAttribute : Attribute
    {
        public readonly int[] triangles;
        public TriangleIndexAttribute(params int[] triangles)
        {
            this.triangles = triangles;
        }
    }

    #region CubeCase0_8
    
    /// <summary>
    /// 0和顶点 = 1
    /// </summary>
    [EdgeIndex(0b000000000000)]
    [TriangleIndex()]
    public sealed class CubeCase0_0x0 : CubeCase
    {
    }
    
    /// <summary>
    /// 8和顶点 = 1
    /// </summary>
    [EdgeIndex(0b111111111111)]
    [TriangleIndex()]
    public sealed class CubeCase8_0x0 : CubeCase
    {
    }

    #endregion
    
    #region CubeCase1_7
    
    /// <summary>
    /// 1和顶点 C8 = 8
    /// 对应 8个顶点的mesh
    /// </summary>
    [EdgeIndex(0b1001000010000)]
    [TriangleIndex()]
    public sealed class CubeCase1_1001000010000 : CubeCase
    {
    }
    
        
    /// <summary>
    /// 7和顶点 与Case1数量相等 三角面取反 = 8
    /// </summary>
    [Serializable]
    public class CubeCase7 : CubeCase
    {
        //todo: 用1点取反
    }

    #endregion
    
    #region CubeCase2_6
    /// <summary>
    /// 2和顶点 C8^2 = (8 * 7) / (2 * 1) = 28
    /// </summary>
    [Serializable]
    public class CubeCase2 : CubeCase
    {
        // 12条边所在2点
        //public ICubeCaseMesh[] edgeMesh;
        // 12条面对角线所在2点
        //public ICubeCaseMesh[] faceDiagonalMesh;
        // 4条 对角所在2点
        //public ICubeCaseMesh[] diagonalMesh;
    }
    
        

    /// <summary>
    /// 6和顶点 与Case2数量相等 三角面取反 = 28
    /// </summary>
    [Serializable]
    public class CubeCase6 : CubeCase
    {
        //todo: 用2点取反
    }

    
    #endregion
    
    #region CubeCase3_5
    /// <summary>
    /// 3和顶点 C8^3 = (8 * 7 * 6) / (3 * 2 * 1) = 56
    /// </summary>
    [Serializable]
    public class CubeCase3 : CubeCase
    {
        // 24个同面3点（6个面 * 4顶点）
        //public ICubeCaseMesh[] faceMesh;
        // 24个 (边 * 对角点）
        //public ICubeCaseMesh[] edgeDiagonalMesh;
        // 8个（顶点 + 2个对交点
        //public ICubeCaseMesh[] vertexDiagonalMesh;
    }
    
    /// <summary>
    /// 5和顶点 与Case3数量相等 三角面取反 = 56
    /// </summary>
    [Serializable]
    public class CubeCase5 : CubeCase
    {
        //todo: 用3点取反
    }

    
    #endregion
    
    #region CubeCase4
    /// <summary>
    /// 4和顶点 C8^4 = (8 * 7 * 6 * 5) / (4 * 3 * 2 * 1) = 70
    /// </summary>
    [Serializable]
    public class CubeCase4 : CubeCase
    {
        // 4点共面
        // 6个  4点共面的情况: 在cube6个面上4点 
        //public ICubeCaseMesh[] faceEdgeMesh;
        
        // 2点共面
        // 6个  对角边
        //public ICubeCaseMesh[] diagonalFaceMesh;
        
        // 3点共面
        // 
        // 24=12*4/2, 12条边与 相垂直面 
        //public ICubeCaseMesh[] edgeDiagonal1Mesh;
        
        
        // 48=12*4 种对角边 4点
        //public ICubeCaseMesh[] edgeDiagonal2Mesh;
    }
    
    #endregion
}