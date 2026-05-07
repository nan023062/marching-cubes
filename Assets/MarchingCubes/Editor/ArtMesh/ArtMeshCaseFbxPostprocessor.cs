using UnityEditor;
using System.IO;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 自动处理所有 case_*.fbx 的导入设置：
    ///   - bakeAxisConversion = true  → 将 Blender→Unity 轴旋转烘焙进顶点，
    ///     根节点保持 identity rotation，GetMesh 的对称旋转公式才能正确工作
    ///   - globalScale = 1            → FBX_SCALE_ALL 已在 Blender 侧处理换算，
    ///     Unity 这里不再额外缩放
    /// </summary>
    public class ArtMeshCaseFbxPostprocessor : AssetPostprocessor
    {
        void OnPreprocessModel()
        {
            string fileName = Path.GetFileName(assetPath);
            if (!System.Text.RegularExpressions.Regex.IsMatch(
                    fileName, @"^case_\d+\.fbx$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return;

            var importer = (ModelImporter)assetImporter;
            bool changed = false;

            if (!importer.bakeAxisConversion) { importer.bakeAxisConversion = true; changed = true; }
            if (importer.globalScale != 1f)   { importer.globalScale = 1f;          changed = true; }

            if (changed)
                UnityEngine.Debug.Log($"[ArtMeshCaseFbx] Fixed import settings: {fileName}");
        }
    }
}
