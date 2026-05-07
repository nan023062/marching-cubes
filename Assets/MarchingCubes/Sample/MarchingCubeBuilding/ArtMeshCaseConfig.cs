using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// ScriptableObject: stores art-mesh prefab assignments for the 53 D4 canonical cases.
    /// The symmetry table (canonical index + rotation for all 256 cases) is computed
    /// automatically from the fixed D4 group on first access — no manual "Compute" step needed.
    /// </summary>
    [CreateAssetMenu(fileName = "ArtMeshCaseConfig", menuName = "MarchingCubes/Art Mesh Case Config")]
    public sealed class ArtMeshCaseConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public GameObject prefab;
            public bool isManualOverride;
        }

        // Prefab slots indexed by canonical cube index (only canonical slots matter)
        [SerializeField] private Entry[] _entries = new Entry[256];

        // ── Runtime symmetry cache (not serialized, recomputed on OnEnable) ──────
        [System.NonSerialized] private int[]        _canonicalIndex;
        [System.NonSerialized] private Quaternion[] _canonicalRotation;
        [System.NonSerialized] private bool[]       _canonicalFlipped;

        private void OnEnable() => EnsureEntries();

        private void EnsureEntries()
        {
            if (_entries == null || _entries.Length != 256)
                _entries = new Entry[256];
            for (int i = 0; i < 256; i++)
                if (_entries[i] == null) _entries[i] = new Entry();
        }

        // ── D4 symmetry — computed once, deterministic for all instances ─────────
        private void EnsureSymmetry()
        {
            if (_canonicalIndex != null) return;

            // D4: 4 Y-axis rotations × (identity / LR-flip). Flip applied first.
            float[] rotY   = { 0, 90, 180, 270, 0, 90, 180, 270 };
            bool[]  flip   = { false, false, false, false, true, true, true, true };
            int[]   mirror = { 1, 0, 3, 2, 5, 4, 7, 6 };   // LR mirror permutation
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
                    Vector3 rotated = rots[t] * (new Vector3(v.x, v.y, v.z) - cen) + cen;
                    int best = 0; float bd = float.MaxValue;
                    for (int j = 0; j < 8; j++)
                    {
                        var vj = CubeTable.Vertices[j];
                        float d = (rotated - new Vector3(vj.x, vj.y, vj.z)).sqrMagnitude;
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

        // ── Public API ───────────────────────────────────────────────────────────

        public bool TryGetEntry(int cubeIndex, out GameObject prefab,
                                out Quaternion rotation, out bool isFlipped)
        {
            prefab = null; rotation = Quaternion.identity; isFlipped = false;
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureEntries(); EnsureSymmetry();
            int canonical = _canonicalIndex[cubeIndex];
            prefab    = _entries[canonical]?.prefab;
            rotation  = _canonicalRotation[cubeIndex];
            isFlipped = _canonicalFlipped[cubeIndex];
            return prefab != null;
        }

        public bool HasEntry(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureEntries(); EnsureSymmetry();
            return _entries[_canonicalIndex[cubeIndex]]?.prefab != null;
        }

        public int GetCanonicalIndex(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return cubeIndex;
            EnsureEntries(); EnsureSymmetry();
            return _canonicalIndex[cubeIndex];
        }

        public bool IsCanonical(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureEntries(); EnsureSymmetry();
            return _canonicalIndex[cubeIndex] == cubeIndex;
        }

        public Entry GetEntry(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return null;
            EnsureEntries();
            return _entries[cubeIndex];
        }

        public bool GetFlipped(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureSymmetry();
            return _canonicalFlipped[cubeIndex];
        }

        public Quaternion GetRotation(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return Quaternion.identity;
            EnsureSymmetry();
            return _canonicalRotation[cubeIndex];
        }
    }
}
