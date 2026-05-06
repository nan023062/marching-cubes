using UnityEngine;

namespace MarchingCubes.Sample
{
    public class ArtMeshBuilding : MonoBehaviour, IMeshStore
    {
        public int x = 5, y = 3, z = 5;
        public uint unit = 1;

        [SerializeField] private MarchingCubes.CubeArtMeshConfig _config;
        [SerializeField] private bool showPoint;

        public int debugHighlightIndex = -1;

        private BlockBuilding _building;
        private static readonly Vector3 s_center = new Vector3(0.5f, 0.5f, 0.5f);

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
            if (_config == null || cubeIndex == 0)
                return null;

            GameObject prefab;
            Quaternion rotation;
            bool isFlipped;

            if (!_config.TryGetEntry(cubeIndex, out prefab, out rotation, out isFlipped))
                return null;

            GameObject wrapper = new GameObject("art_" + cubeIndex);

            GameObject child = Object.Instantiate(prefab, wrapper.transform);
            child.transform.localPosition = s_center - rotation * s_center;
            child.transform.localRotation = rotation;
            child.transform.localScale = isFlipped ? new Vector3(-1f, 1f, 1f) : Vector3.one;

            return wrapper;
        }
    }
}
