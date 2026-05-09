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
    [CreateAssetMenu(fileName = "MqMeshConfig", menuName = "MarchingCubes/Mq Mesh Config")]
    public sealed class MqMeshConfig : ScriptableObject
    {
        public const int TerrainCaseCount = 19;
        public const int CliffCaseCount   = 16;

        // D4 规范 case（只需导出这 5 个 FBX，其余由旋转派生）
        public static readonly int[] CliffCanonicalCases = { 1, 3, 5, 7, 15 };

        // (规范 case, 旋转次数) for cliff case 0-15，旋转 = Euler(0, 90*n, 0)
        public static readonly (int canonical, int rotCount)[] CliffD4Map =
        {
            (0,  0),  // 0:  无悬崖
            (1,  0),  // 1:  南墙
            (1,  1),  // 2:  东墙
            (3,  0),  // 3:  南+东
            (1,  2),  // 4:  北墙
            (5,  0),  // 5:  南+北（对穿）
            (3,  1),  // 6:  东+北
            (7,  0),  // 7:  南+东+北
            (1,  3),  // 8:  西墙
            (3,  3),  // 9:  南+西
            (5,  1),  // 10: 东+西
            (7,  3),  // 11: 南+东+西
            (3,  2),  // 12: 北+西
            (7,  2),  // 13: 南+北+西
            (7,  1),  // 14: 东+北+西
            (15, 0),  // 15: 四面（孤岛）
        };

        [Header("地形 Tile（case 0-18）")]
        [SerializeField] private GameObject[] _prefabs = new GameObject[TerrainCaseCount];

        [Header("悬崖 Tile（case 0-15，Mesh 以格子中心为原点）")]
        [SerializeField] private GameObject[] _cliffPrefabs = new GameObject[CliffCaseCount];

        private void OnEnable() { EnsureArray(ref _prefabs, TerrainCaseCount); EnsureArray(ref _cliffPrefabs, CliffCaseCount); }

        private static void EnsureArray(ref GameObject[] arr, int count)
        {
            if (arr == null || arr.Length != count)
            {
                var old = arr;
                arr = new GameObject[count];
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
    }
}
