using UnityEditor;

namespace MarchingCubes.Editor
{
    /// <summary>
    /// 自动对 MC_ArtMesh_Cubes/fbx_case/ 下的 FBX 开启 Bake Axis Conversion。
    /// 这样 Blender 导出的坐标系旋转（X=-90）会被烧进顶点，GameObject 导入后 transform 为 identity。
    /// </summary>
    public sealed class ArtMeshFbxPostprocessor : AssetPostprocessor
    {
        private const string k_FbxFolder = "Assets/MarchingCubes/Sample/Resources/MC_ArtMesh_Cubes/fbx_case";

        void OnPreprocessModel()
        {
            if (!assetPath.Replace('\\', '/').StartsWith(k_FbxFolder))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.bakeAxisConversion = true;
        }
    }
}
