using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// 存储 256 个 case（0-255）各自对应的 Prefab 引用。
    /// ios_mesh/ 已为每个 case 提供独立 mesh，无需对称归约。
    /// </summary>
    [CreateAssetMenu(fileName = "IosMeshCaseConfig", menuName = "MarchingCubes/Ios Mesh Case Config")]
    public sealed class IosMeshCaseConfig : CasePrefabConfig
    {
        [SerializeField] private GameObject[] _prefabs = new GameObject[256];

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
