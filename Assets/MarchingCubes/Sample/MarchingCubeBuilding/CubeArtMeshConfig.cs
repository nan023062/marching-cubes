using UnityEngine;

namespace MarchingCubes
{
    /// <summary>
    /// ScriptableObject that stores art mesh prefab assignments for all 256 cube configurations.
    /// Canonical index + rotation data is precomputed by CubeSymmetry and written here.
    /// </summary>
    [CreateAssetMenu(fileName = "CubeArtMeshConfig", menuName = "MarchingCubes/Art Mesh Config")]
    public sealed class CubeArtMeshConfig : ScriptableObject
    {
        [System.Serializable]
        public sealed class Entry
        {
            public GameObject prefab;
            public bool isManualOverride;
        }

        [SerializeField] private Entry[] _entries = new Entry[256];

        // Canonical mapping: _canonicalIndex[i] = canonical cube index for cube i
        [SerializeField] private int[] _canonicalIndex = new int[256];

        // Rotation stored as 4 separate float arrays to avoid Euler gimbal lock
        [SerializeField] private float[] _qx = new float[256];
        [SerializeField] private float[] _qy = new float[256];
        [SerializeField] private float[] _qz = new float[256];
        [SerializeField] private float[] _qw = new float[256];

        // Whether the forward transform for each cube index includes an LR flip
        [SerializeField] private bool[] _isFlipped = new bool[256];

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (_entries == null || _entries.Length != 256)
                _entries = new Entry[256];

            if (_canonicalIndex == null || _canonicalIndex.Length != 256)
                _canonicalIndex = new int[256];

            if (_qx == null || _qx.Length != 256) _qx = new float[256];
            if (_qy == null || _qy.Length != 256) _qy = new float[256];
            if (_qz == null || _qz.Length != 256) _qz = new float[256];
            if (_qw == null || _qw.Length != 256) _qw = new float[256];
            if (_isFlipped == null || _isFlipped.Length != 256)
                _isFlipped = new bool[256];

            for (int i = 0; i < 256; i++)
            {
                if (_entries[i] == null)
                    _entries[i] = new Entry();

                // Default rotation = identity (qw=1)
                if (_qw[i] == 0f && _qx[i] == 0f && _qy[i] == 0f && _qz[i] == 0f)
                    _qw[i] = 1f;
            }
        }

        /// <summary>
        /// Called by editor tool after computing symmetry table.
        /// </summary>
        public void SetSymmetryData(int[] canonicalIndex, Quaternion[] canonicalRotation, bool[] canonicalFlipped)
        {
            if (canonicalIndex == null || canonicalIndex.Length != 256)
                throw new System.ArgumentException("canonicalIndex must have length 256");
            if (canonicalRotation == null || canonicalRotation.Length != 256)
                throw new System.ArgumentException("canonicalRotation must have length 256");
            if (canonicalFlipped == null || canonicalFlipped.Length != 256)
                throw new System.ArgumentException("canonicalFlipped must have length 256");

            EnsureInitialized();

            for (int i = 0; i < 256; i++)
            {
                _canonicalIndex[i] = canonicalIndex[i];
                Quaternion q = canonicalRotation[i];
                _qx[i] = q.x;
                _qy[i] = q.y;
                _qz[i] = q.z;
                _qw[i] = q.w;
                _isFlipped[i] = canonicalFlipped[i];
            }
        }

        /// <summary>
        /// Returns true if a prefab can be resolved for this cube index (via canonical).
        /// Out prefab is the canonical prefab; out rotation and isFlipped are the forward
        /// transform that maps canonical to this cube index.
        /// </summary>
        public bool TryGetEntry(int cubeIndex, out GameObject prefab, out Quaternion rotation, out bool isFlipped)
        {
            if (cubeIndex < 0 || cubeIndex >= 256)
            {
                prefab = null;
                rotation = Quaternion.identity;
                isFlipped = false;
                return false;
            }

            EnsureInitialized();

            int canonical = _canonicalIndex[cubeIndex];
            Entry entry = _entries[canonical];
            prefab = (entry != null) ? entry.prefab : null;
            rotation = new Quaternion(_qx[cubeIndex], _qy[cubeIndex], _qz[cubeIndex], _qw[cubeIndex]);
            isFlipped = _isFlipped[cubeIndex];
            return prefab != null;
        }

        /// <summary>
        /// Returns whether the forward transform for this cube index includes an LR flip.
        /// </summary>
        public bool GetFlipped(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureInitialized();
            return _isFlipped[cubeIndex];
        }

        public bool HasEntry(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256)
                return false;

            EnsureInitialized();

            int canonical = _canonicalIndex[cubeIndex];
            Entry entry = _entries[canonical];
            return entry != null && entry.prefab != null;
        }

        public int GetCanonicalIndex(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return cubeIndex;
            EnsureInitialized();
            return _canonicalIndex[cubeIndex];
        }

        public bool IsCanonical(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return false;
            EnsureInitialized();
            return _canonicalIndex[cubeIndex] == cubeIndex;
        }

        public Entry GetEntry(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return null;
            EnsureInitialized();
            return _entries[cubeIndex];
        }

        public Quaternion GetRotation(int cubeIndex)
        {
            if (cubeIndex < 0 || cubeIndex >= 256) return Quaternion.identity;
            EnsureInitialized();
            return new Quaternion(_qx[cubeIndex], _qy[cubeIndex], _qz[cubeIndex], _qw[cubeIndex]);
        }
    }
}
