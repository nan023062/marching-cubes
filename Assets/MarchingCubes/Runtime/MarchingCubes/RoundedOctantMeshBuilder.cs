using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// 默认算法：八分体填充 + 圆角 arc bevel。
    /// 对应 Blender mc_artmesh 插件的 Quick Export 生成风格。
    /// </summary>
    [CreateAssetMenu(
        fileName = "RoundedOctantMeshBuilder",
        menuName  = "MarchingCubes/CaseMeshBuilder/RoundedOctant")]
    public sealed class RoundedOctantMeshBuilder : CaseMeshBuilderAsset
    {
        [Range(0f, 0.24f)] public float sideRadius = 0.08f;
        [Range(0f, 0.24f)] public float topRadius  = 0f;
        [Range(1, 12)]     public int   segments   = 4;

        public override Mesh Build(int caseIndex)
            => ProceduralCaseMesh.Build(caseIndex, sideRadius, topRadius, segments);
    }
}
