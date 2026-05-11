using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// 地形 tile 配置（base-3 编码 81 槽：65 个真实几何 + 16 个死槽）。
    ///
    /// case_idx = r0 + r1*3 + r2*9 + r3*27，r_i = h_i - min(h0..h3) ∈ {0,1,2}。
    /// 65 个有效 case（min(r) == 0）需要对应 prefab；
    /// 16 个死槽（min(r) > 0）永远不会被 TileTable.GetMeshCase 产出，槽位常驻 null。
    /// 角点编号：V3(TL)─V2(TR) / V0(BL)─V1(BR)
    /// </summary>
    [CreateAssetMenu(fileName = "TileCaseConfig", menuName = "MarchingCubes/Mq Mesh Config")]
    public sealed class TileCaseConfig : ScriptableObject
    {
        public const int TerrainCaseCount = 81;

        [Header("地形 Tile（case 0-80，base-3 编码；16 个死槽常驻 null）")]
        [SerializeField] private GameObject[] _prefabs = new GameObject[TerrainCaseCount];

#if UNITY_EDITOR
        // ── Editor 缓存（仅编辑器使用，随 .asset 持久化，不进运行时）────────────
        [HideInInspector] public string   editorFbxFolder    = "Assets/MarchingCubes/Sample/Resources/mq";
        [HideInInspector] public string   editorPrefabFolder = "Assets/MarchingCubes/Sample/Resources/mq/prefabs";
        [HideInInspector] public Material editorTerrainMat;
#endif

        private void OnEnable()
        {
            EnsureArray(ref _prefabs, TerrainCaseCount);
        }

        private static void EnsureArray<T>(ref T[] arr, int count) where T : class
        {
            if (arr == null || arr.Length != count)
            {
                var old = arr;
                arr = new T[count];
                if (old != null)
                    for (int i = 0; i < Mathf.Min(old.Length, count); i++)
                        arr[i] = old[i];
            }
        }

        // ── 地形 API ──────────────────────────────────────────────────────────

        public GameObject GetPrefab(int ci)
        {
            EnsureArray(ref _prefabs, TerrainCaseCount);
            return (ci >= 0 && ci < TerrainCaseCount) ? _prefabs[ci] : null;
        }

        public void SetPrefab(int ci, GameObject prefab)
        {
            EnsureArray(ref _prefabs, TerrainCaseCount);
            if (ci >= 0 && ci < TerrainCaseCount) _prefabs[ci] = prefab;
        }
    }
}
