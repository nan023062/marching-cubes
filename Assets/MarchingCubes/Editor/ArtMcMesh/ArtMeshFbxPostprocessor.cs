using UnityEditor;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 对 Sample/Resources 下所有 FBX 统一开启 Bake Axis Conversion。
    /// 覆盖 MC case FBX 和 MQ case FBX，无需各自单独配置。
    /// Blender 导出坐标系旋转会被烧进顶点，GameObject 导入后 transform 为 identity。
    /// </summary>
    public sealed class ArtMeshFbxPostprocessor : AssetPostprocessor
    {
        private const string k_ResourcesFolder = "Assets/MarchingCubes/Sample/Resources";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(k_ResourcesFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
        }
    }
}
