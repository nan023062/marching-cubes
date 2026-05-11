using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// 存储 255 个 case（1-254）各自对应的已归一化 Prefab 引用。
    /// 每个 Prefab 内部已烘焙好正确的坐标系旋转 + D4 对称变换 + LR flip，
    /// GetMesh 直接 Instantiate，无需任何运行时旋转计算。
    /// </summary>
    [CreateAssetMenu(fileName = "D4FbxCaseConfig", menuName = "MarchingCubes/D4 Fbx Case Config")]
    public sealed class D4FbxCaseConfig : CasePrefabConfig
    {
        // index 0 = 空，index 255 = 全实心（均为 null）
        [SerializeField] private GameObject[] _prefabs = new GameObject[256];

        // ── Runtime symmetry cache（仅供 Editor 工具使用，不序列化）─────────
        [System.NonSerialized] private int[]        _canonicalIndex;
        [System.NonSerialized] private Quaternion[] _canonicalRotation;
        [System.NonSerialized] private bool[]       _canonicalFlipped;

        private void OnEnable() => EnsurePrefabs();

        private void EnsurePrefabs()
        {
            if (_prefabs == null || _prefabs.Length != 256)
                _prefabs = new GameObject[256];
        }

        // ── D4 symmetry（Editor 工具在生成预制体时调用）──────────────────────
        public void EnsureSymmetry()
        {
            if (_canonicalIndex != null) return;

            float[] rotY   = { 0, 90, 180, 270, 0, 90, 180, 270 };
            bool[]  flip   = { false, false, false, false, true, true, true, true };
            int[]   mirror = { 1, 0, 3, 2, 5, 4, 7, 6 };
            var     cen    = new Vector3(0.5f, 0.5f, 0.5f);

            var rots  = new Quaternion[8];
            var perms = new int[8][];
            for (int t = 0; t < 8; t++)
            {
                rots[t]  = Quaternion.Euler(0, rotY[t], 0);
                perms[t] = new int[8];
                for (int i = 0; i < 8; i++)
                {
                    int src = flip[t] ? mirror[i] : i;
                    var v   = CubeTable.Vertices[src];
                    Vector3 rot = rots[t] * (new Vector3(v.x, v.y, v.z) - cen) + cen;
                    int best = 0; float bd = float.MaxValue;
                    for (int j = 0; j < 8; j++)
                    {
                        var vj = CubeTable.Vertices[j];
                        float d = (rot - new Vector3(vj.x, vj.y, vj.z)).sqrMagnitude;
                        if (d < bd) { bd = d; best = j; }
                    }
                    perms[t][i] = best;
                }
            }

            _canonicalIndex    = new int[256];
            _canonicalRotation = new Quaternion[256];
            _canonicalFlipped  = new bool[256];

            for (int ci = 0; ci < 256; ci++)
            {
                int  bestIdx  = ci;
                var  bestRot  = Quaternion.identity;
                bool bestFlip = false;
                for (int t = 0; t < 8; t++)
                {
                    int mapped = 0;
                    for (int i = 0; i < 8; i++)
                        if ((ci & (1 << i)) != 0) mapped |= 1 << perms[t][i];
                    if (mapped < bestIdx)
                    {
                        bestIdx  = mapped;
                        bestRot  = rots[t];
                        bestFlip = flip[t];
                    }
                }
                _canonicalIndex[ci]    = bestIdx;
                _canonicalRotation[ci] = bestRot;
                _canonicalFlipped[ci]  = bestFlip;
            }
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>运行时获取 prefab，直接 Instantiate 即可，无需旋转。</summary>
        public override GameObject GetPrefab(int cubeIndex)
        {
            if (cubeIndex <= 0 || cubeIndex >= 255) return null;
            EnsurePrefabs();
            return _prefabs[cubeIndex];
        }

        /// <summary>Editor 工具生成后写入 prefab 引用。</summary>
        public void SetPrefab(int cubeIndex, GameObject prefab)
        {
            EnsurePrefabs();
            if (cubeIndex >= 0 && cubeIndex < 256)
                _prefabs[cubeIndex] = prefab;
        }

        // Editor 工具查询对称数据
        public int        GetCanonicalIndex(int ci) { EnsureSymmetry(); return _canonicalIndex[ci]; }
        public Quaternion GetRotation(int ci)       { EnsureSymmetry(); return _canonicalRotation[ci]; }
        public bool       GetFlipped(int ci)        { EnsureSymmetry(); return _canonicalFlipped[ci]; }
        public bool       IsCanonical(int ci)       { EnsureSymmetry(); return _canonicalIndex[ci] == ci; }
    }
}
