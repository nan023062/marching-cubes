using UnityEngine;

namespace MarchingCubes.Sample
{
    [CreateAssetMenu(fileName = "New SamplePrefabTable", menuName = "MarchingCubes/SamplePrefabTable")]
    public class SamplePrefabTable : ScriptableObject
    {
        #region 1点
        
        /// <summary>
        /// 1点上角   -- 对应4种
        /// </summary>
        public GameObject case1_top;
        
        /// <summary>
        /// 1点下角  -- 对应4种
        /// </summary>
        public GameObject case1_down;
        
        #endregion
        
        #region 2点
        
        /// <summary>
        /// 水平2点 下、上 
        /// </summary>
        public GameObject case2_horizontalD, case2_horizontalT;
        
        /// <summary>
        /// 垂直2点
        /// </summary>
        public GameObject case2_vertical;
        
        /// <summary>
        /// 垂直对角线2点
        /// </summary>
        public GameObject case2_vDiagonal;
        
        /// <summary>
        /// 水平对角线2点
        /// </summary>
        public GameObject case2_hDiagonal;
        
        
        #endregion
        
        #region 3点
        
        /// <summary>
        /// 水平3点 下、上 -- 各对应4种
        /// </summary>
        public GameObject case3_hD, case3_hT;
        
        /// <summary>
        /// 垂直平3点  -- 对应16种
        /// </summary>
        public GameObject case3_vertical;
        
        /// <summary>
        /// 对角3点  -- 对应8种
        /// </summary>
        public GameObject case3_diagonal;
        
        #endregion
    }
    

}