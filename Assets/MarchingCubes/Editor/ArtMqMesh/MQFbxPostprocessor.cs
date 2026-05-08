using UnityEditor;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 自动对 MQ case FBX 目录下的 FBX 开启 Bake Axis Conversion。
    /// Blender 导出时使用 axis_forward='Y' / axis_up='Z'，
    /// bakeAxisConversion 会将坐标系旋转烧进顶点，GameObject 导入后 transform 为 identity。
    /// </summary>
    public sealed class MQFbxPostprocessor : AssetPostprocessor
    {
        private const string k_FbxFolder = "Assets/MarchingCubes/ArtMesh/MQ/Cases";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(k_FbxFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
        }
    }
}
