using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// 地形 + 悬崖 tile 配置（统一在一个 ScriptableObject 里）。
    ///
    /// ── 地形 tile（_prefabs，19 个）────────────────────────────────────────────
    ///   角点 bit mask：bit_i=1 表示 Vi 高于 base。
    ///   V3(TL)─V2(TR) / V0(BL)─V1(BR)
    ///   case 0-14：标准（四角高差 ≤ 1）
    ///   case 15-18：对角高差 == 2 的特殊 case
    ///     15=V0+2/V2=base  16=V1+2/V3=base  17=V2+2/V0=base  18=V3+2/V1=base
    ///
    /// ── 悬崖 tile（_cliffPrefabs，16 个）───────────────────────────────────────
    ///   边 bit mask（哪条边有悬崖墙面）：
    ///   bit 0=E0 南(-Z)  bit 1=E1 东(+X)  bit 2=E2 北(+Z)  bit 3=E3 西(-X)
    ///   case 0：无悬崖，留空。case 1-15：由 D4 旋转生成。
    ///   规范 case：1(南墙) 3(南+东) 5(南+北对穿) 7(南+东+北) 15(四面)
    /// </summary>
    [CreateAssetMenu(fileName = "TileCaseConfig", menuName = "MarchingCubes/Mq Mesh Config")]
    public sealed class TileCaseConfig : ScriptableObject
    {
        public const int TerrainCaseCount = 19;
        public const int CliffCaseCount   = 16;

        [Header("地形 Tile（case 0-18）")]
        [SerializeField] private GameObject[] _prefabs = new GameObject[TerrainCaseCount];

        [Header("悬崖 Tile（case 0-15，Mesh 以格子中心为原点）")]
        [SerializeField] private GameObject[] _cliffPrefabs = new GameObject[CliffCaseCount];

        [Header("地形法线贴图（case 0-18，Blender 烘焙 → art-mq-mesh Refresh 写入）")]
        [SerializeField] private Texture2D[] _normalMaps = new Texture2D[TerrainCaseCount];

#if UNITY_EDITOR
        // ── Editor 缓存（仅编辑器使用，随 .asset 持久化，不进运行时）────────────
        // 地形 / 悬崖 共用 FBX 与 Prefab 文件夹；材质各自独立
        [HideInInspector] public string   editorFbxFolder    = "Assets/MarchingCubes/Sample/Resources/mq";
        [HideInInspector] public string   editorPrefabFolder = "Assets/MarchingCubes/Sample/Resources/mq/prefabs";
        [HideInInspector] public Material editorTerrainMat;
        [HideInInspector] public Material editorCliffMat;
#endif

        private void OnEnable()
        {
            EnsureArray(ref _prefabs,      TerrainCaseCount);
            EnsureArray(ref _cliffPrefabs, CliffCaseCount);
            EnsureArray(ref _normalMaps,   TerrainCaseCount);
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

        // ── 悬崖 API ──────────────────────────────────────────────────────────

        public GameObject GetCliffPrefab(int ci)
        {
            EnsureArray(ref _cliffPrefabs, CliffCaseCount);
            return (ci >= 0 && ci < CliffCaseCount) ? _cliffPrefabs[ci] : null;
        }

        public void SetCliffPrefab(int ci, GameObject prefab)
        {
            EnsureArray(ref _cliffPrefabs, CliffCaseCount);
            if (ci >= 0 && ci < CliffCaseCount) _cliffPrefabs[ci] = prefab;
        }

        // ── 法线贴图 API ──────────────────────────────────────────────────────

        public Texture2D GetNormalMap(int ci)
        {
            EnsureArray(ref _normalMaps, TerrainCaseCount);
            return (ci >= 0 && ci < TerrainCaseCount) ? _normalMaps[ci] : null;
        }

        public void SetNormalMap(int ci, Texture2D tex)
        {
            EnsureArray(ref _normalMaps, TerrainCaseCount);
            if (ci >= 0 && ci < TerrainCaseCount) _normalMaps[ci] = tex;
        }
    }
}
