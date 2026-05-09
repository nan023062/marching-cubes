using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// 存储 16 个 Marching Squares case（0-15）各自对应的 Prefab。
    /// 通过 D4 对称归约，只需准备 6 个 canonical FBX，Editor 自动生成全 16 个 prefab。
    ///
    /// 角点编号（每个 quad 的四顶点，bit 0~3）：
    ///   V3(TL) ─── V2(TR)
    ///     │           │
    ///   V0(BL) ─── V1(BR)
    ///
    /// case index = bit0(V0) | bit1(V1) | bit2(V2) | bit3(V3)
    /// bit=1 表示该角点处于"高"位（相对 base+1），bit=0 表示 base 高度。
    ///
    /// 6 个 canonical cases：0, 1, 3, 5, 7, 15
    /// </summary>
    [CreateAssetMenu(fileName = "MQMeshConfig", menuName = "MarchingCubes/MQ Mesh Config")]
    public sealed class MqMeshConfig : ScriptableObject
    {
        [SerializeField] private GameObject[] _prefabs = new GameObject[16];

        [System.NonSerialized] private int[]        _canonicalIndex;
        [System.NonSerialized] private Quaternion[] _canonicalRotation;
        [System.NonSerialized] private bool[]       _canonicalFlipped;

        private void OnEnable() => EnsureArray();

        private void EnsureArray()
        {
            if (_prefabs == null || _prefabs.Length != 16)
                _prefabs = new GameObject[16];
        }

        // ── D4 Symmetry ───────────────────────────────────────────────────────

        /// <summary>
        /// 计算并缓存 D4 对称表。
        /// D4 角点置换（dest[src]）：8 个变换对应 4 旋转 × 2 翻转。
        ///
        /// 旋转：rotY=-90（CW from above）对应 r，perm_r = [3,0,1,2]
        ///       即 V0→V3, V1→V0, V2→V1, V3→V2
        /// 翻转：镜像 X，V0↔V1, V2↔V3
        /// </summary>
        public void EnsureSymmetry()
        {
            if (_canonicalIndex != null) return;

            // 直接使用 MqTable 中的置换表和预计算结果
            _canonicalIndex    = new int[MqTable.CaseCount];
            _canonicalRotation = new Quaternion[MqTable.CaseCount];
            _canonicalFlipped  = new bool[MqTable.CaseCount];

            for (int ci = 0; ci < MqTable.CaseCount; ci++)
            {
                int  bestIdx  = ci;
                var  bestRot  = UnityEngine.Quaternion.identity;
                bool bestFlip = false;

                for (int t = 0; t < MqTable.D4Perms.Length; t++)
                {
                    int mapped = 0;
                    for (int i = 0; i < MqTable.CornerCount; i++)
                        if ((ci & (1 << i)) != 0)
                            mapped |= 1 << MqTable.D4Perms[t][i];

                    if (mapped < bestIdx)
                    {
                        bestIdx  = mapped;
                        bestRot  = UnityEngine.Quaternion.Euler(0, MqTable.D4RotY[t], 0);
                        bestFlip = MqTable.D4Flipped[t];
                    }
                }

                _canonicalIndex[ci]    = bestIdx;
                _canonicalRotation[ci] = bestRot;
                _canonicalFlipped[ci]  = bestFlip;
            }

            // Case 15 复用 case 0（MqTable 已预计算，此处与之保持一致）
            _canonicalIndex[15]    = 0;
            _canonicalRotation[15] = UnityEngine.Quaternion.identity;
            _canonicalFlipped[15]  = false;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public GameObject GetPrefab(int caseIndex)
        {
            EnsureArray();
            return (caseIndex >= 0 && caseIndex < 16) ? _prefabs[caseIndex] : null;
        }

        public void SetPrefab(int caseIndex, GameObject prefab)
        {
            EnsureArray();
            if (caseIndex >= 0 && caseIndex < 16)
                _prefabs[caseIndex] = prefab;
        }

        public int        GetCanonicalIndex(int ci) { EnsureSymmetry(); return _canonicalIndex[ci]; }
        public Quaternion GetRotation(int ci)       { EnsureSymmetry(); return _canonicalRotation[ci]; }
        public bool       GetFlipped(int ci)        { EnsureSymmetry(); return _canonicalFlipped[ci]; }
        public bool       IsCanonical(int ci)       { EnsureSymmetry(); return _canonicalIndex[ci] == ci; }
    }
}
