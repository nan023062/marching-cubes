using UnityEngine;

namespace MarchingCubes.Sample
{
    public class ArtMeshBuilding : MonoBehaviour, IMeshStore
    {
        public int x = 5, y = 3, z = 5;
        public uint unit = 1;

        [SerializeField] private MarchingCubes.ArtMeshCaseConfig _config;
        [SerializeField] private bool showPoint;

        public int debugHighlightIndex = -1;

        private BlockBuilding _building;

        private void Awake()
        {
            Vector3 scale = Vector3.one / unit;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(x, y, z, matrix, this);
            transform.localScale = scale;
        }

        public void SetPoint(int px, int py, int pz, bool active)
        {
            _building?.SetPointStatus(px, py, pz, active);
        }

        private void OnDrawGizmos()
        {
            if (showPoint)
                _building?.DrawPoints();
        }

        GameObject IMeshStore.GetMesh(int cubeIndex)
        {
            if (_config == null) return null;
            var prefab = _config.GetPrefab(cubeIndex);
            if (prefab == null) return null;
            return Object.Instantiate(prefab);
        }
    }
}
