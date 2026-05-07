using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 自动处理所有 case_*.fbx 的导入设置。
    /// bakeAxisConversion=true：将 Blender→Unity 轴旋转烘焙进顶点，
    ///   根节点 localRotation=identity，GetMesh 的 D4 旋转公式才能正确工作。
    /// globalScale 保持 Unity 默认 (0.01)，与 Blender 侧 FBX_SCALE_ALL (×100) 抵消，
    ///   最终几何尺寸为 1:1。
    /// </summary>
    public class ArtMeshCaseFbxPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            string fileName = Path.GetFileName(assetPath);
            if (!Regex.IsMatch(fileName, @"^case_\d+\.fbx$", RegexOptions.IgnoreCase))
                return;

            var imp = (ModelImporter)assetImporter;
            if (!imp.bakeAxisConversion)
            {
                imp.bakeAxisConversion = true;
                UnityEngine.Debug.Log($"[ArtMeshCase] bakeAxisConversion=true → {fileName}");
            }
        }
    }
}
