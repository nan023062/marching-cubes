using UnityEngine;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// Marching Edges prefab 配置：1044 个 canonical case 各对应一个 prefab。
    /// prefab 以格点为中心，local scale = 1（由 MeController 乘以 cell size）。
    /// </summary>
    [CreateAssetMenu(menuName = "MarchingCubes/ME Case Config")]
    public class MeCaseConfig : ScriptableObject
    {
        public const int CaseCount = 1044;

        [SerializeField] GameObject[] _prefabs = new GameObject[CaseCount];

        public GameObject GetPrefab(int canonicalIndex)
        {
            if (_prefabs == null || canonicalIndex < 0 || canonicalIndex >= _prefabs.Length)
                return null;
            return _prefabs[canonicalIndex];
        }

        public void SetPrefab(int canonicalIndex, GameObject prefab)
        {
            if (_prefabs == null || canonicalIndex < 0 || canonicalIndex >= CaseCount) return;
            if (_prefabs.Length != CaseCount)
            {
                var tmp = new GameObject[CaseCount];
                System.Array.Copy(_prefabs, tmp, Mathf.Min(_prefabs.Length, CaseCount));
                _prefabs = tmp;
            }
            _prefabs[canonicalIndex] = prefab;
        }
    }
}
