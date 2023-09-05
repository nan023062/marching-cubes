using UnityEngine;

namespace MarchingCubes
{
    [CreateAssetMenu(fileName = "New MarchingMeshTable", menuName = "MarchingCubes/MarchingMeshTable")]
    public class MarchingMeshTable : ScriptableObject, IMarchingMeshTable
    {
        [Header("256种网格")]
        public Mesh[] meshes;
    }
}