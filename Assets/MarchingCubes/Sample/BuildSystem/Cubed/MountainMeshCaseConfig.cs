using UnityEngine;

namespace MarchingCubes
{
    [CreateAssetMenu(fileName = "MountainMeshCaseConfig", menuName = "MarchingCubes/Mountain Mesh Case Config")]
    public sealed class MountainMeshCaseConfig : CasePrefabConfig
    {
        [SerializeField] private GameObject[] _prefabs = new GameObject[256];

#if UNITY_EDITOR
        [HideInInspector] public string   editorMeshFolder   = "Assets/MarchingCubes/Sample/Resources/mountain/meshes";
        [HideInInspector] public string   editorPrefabFolder = "Assets/MarchingCubes/Sample/Resources/mountain/prefabs";
        [HideInInspector] public Material editorMaterial;
        [HideInInspector] public float    editorSolidBias    = 0.35f;
#endif

        private void OnEnable() => EnsureArray();

        private void EnsureArray()
        {
            if (_prefabs == null || _prefabs.Length != 256)
                _prefabs = new GameObject[256];
        }

        public override GameObject GetPrefab(int caseIndex)
        {
            EnsureArray();
            return (caseIndex >= 0 && caseIndex < 256) ? _prefabs[caseIndex] : null;
        }

        public void SetPrefab(int caseIndex, GameObject prefab)
        {
            EnsureArray();
            if (caseIndex >= 0 && caseIndex < 256)
                _prefabs[caseIndex] = prefab;
        }
    }
}
