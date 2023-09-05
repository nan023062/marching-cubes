using System.Collections.Generic;
using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// mesh operation tool
    /// todo: 目前之多凸面体有效，非凸面体会出现三角面错乱
    /// </summary>
    public static class MeshUtility
    {
        /// <summary>
        /// 使用一个平面切分mesh
        /// </summary>
        /// <param name="mesh">需切分的mesh</param>
        /// <param name="plane">使用的平面</param>
        /// <param name="closedSubMesh">是否将切分后的子网格封闭</param> 
        /// <returns>切分后的子网格列表</returns>
        public static Mesh[] CutMesh(Mesh mesh, in Plane plane, bool closedSubMesh = false)
        {
            List<Mesh> meshList = new List<Mesh>();
            
            
            
            
            
            
            
            return meshList.ToArray();
        }

        /// <summary>
        /// 将多个网格合并成一个
        /// </summary>
        /// <param name="meshes">需合并的网格数组</param>
        /// <param name="mergeCoincident">是否合并重合点</param>
        /// <returns>合并后网格</returns>
        public static Mesh MergeMesh(Mesh[] meshes, bool mergeCoincident = false)
        {
            return null;
        }
    }
}