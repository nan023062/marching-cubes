using UnityEngine;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// Marching Edges prefab 配置：618 个 canonical case（D4 水平对称）各对应一个 prefab。
    /// prefab 以格点为中心，local scale = 1（由 FaceController 乘以 cell size）。
    /// 运行时通过 FaceBuilder.GetCanonical 获取 canonIdx + rot + flip，flip 时 scale.x 取反。
    /// </summary>
    [CreateAssetMenu(menuName = "MarchingCubes/ME Case Config")]
    public class MeCaseConfig : ScriptableObject
    {
        public static int CaseCount => FaceBuilder.CanonicalCount;

        [SerializeField] GameObject[] _prefabs = new GameObject[0];

        void OnEnable()
        {
            FaceBuilder.InitLookup();
            EnsureSize();
        }

        void EnsureSize()
        {
            int n = FaceBuilder.CanonicalCount;
            if (_prefabs == null || _prefabs.Length == n) return;
            var tmp = new GameObject[n];
            if (_prefabs != null)
                System.Array.Copy(_prefabs, tmp, Mathf.Min(_prefabs.Length, n));
            _prefabs = tmp;
        }

        public GameObject GetPrefab(int canonicalIndex)
        {
            EnsureSize();
            if (canonicalIndex < 0 || canonicalIndex >= _prefabs.Length) return null;
            return _prefabs[canonicalIndex];
        }

        public void SetPrefab(int canonicalIndex, GameObject prefab)
        {
            EnsureSize();
            if (canonicalIndex < 0 || canonicalIndex >= _prefabs.Length) return;
            _prefabs[canonicalIndex] = prefab;
        }
    }
}
