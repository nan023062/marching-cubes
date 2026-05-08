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
    public sealed class MQMeshConfig : ScriptableObject
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

            // 8 D4 permutations: perm[t][i] = which position corner i moves to
            int[][] perms = {
                new[]{0, 1, 2, 3},  // e      rotY=0
                new[]{3, 0, 1, 2},  // r      rotY=270 (90° CW viewed from above)
                new[]{2, 3, 0, 1},  // r²     rotY=180
                new[]{1, 2, 3, 0},  // r³     rotY=90
                new[]{1, 0, 3, 2},  // s      rotY=0, flip
                new[]{2, 1, 0, 3},  // sr     rotY=270, flip
                new[]{3, 2, 1, 0},  // sr²    rotY=180, flip
                new[]{0, 3, 2, 1},  // sr³    rotY=90, flip
            };

            float[] rotY  = { 0f, 270f, 180f, 90f, 0f, 270f, 180f, 90f };
            bool[]  flip  = { false, false, false, false, true, true, true, true };

            _canonicalIndex    = new int[16];
            _canonicalRotation = new Quaternion[16];
            _canonicalFlipped  = new bool[16];

            for (int ci = 0; ci < 16; ci++)
            {
                int  bestIdx  = ci;
                var  bestRot  = Quaternion.identity;
                bool bestFlip = false;

                for (int t = 0; t < 8; t++)
                {
                    int mapped = 0;
                    for (int i = 0; i < 4; i++)
                        if ((ci & (1 << i)) != 0)
                            mapped |= 1 << perms[t][i];

                    if (mapped < bestIdx)
                    {
                        bestIdx  = mapped;
                        bestRot  = Quaternion.Euler(0, rotY[t], 0);
                        bestFlip = flip[t];
                    }
                }

                _canonicalIndex[ci]    = bestIdx;
                _canonicalRotation[ci] = bestRot;
                _canonicalFlipped[ci]  = bestFlip;
            }
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
