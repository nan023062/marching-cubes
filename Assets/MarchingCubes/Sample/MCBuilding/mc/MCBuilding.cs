using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MCBuilding : MonoBehaviour, IMeshStore
    {
        public int x, y, z;
        public uint unit = 1;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        [SerializeField] private GameObject pointCubePrefab;
        [SerializeField] private bool showPoint;
        [SerializeField] private bool debugCube;

        private BlockBuilding _building;
        private PointCube[,,] _pointCubes;
        private static readonly Vector3 s_center = new Vector3(0.5f, 0.5f, 0.5f);

        // ── Config 访问 ───────────────────────────────────────────────────────

        public int ConfigCount => _configs != null ? _configs.Length : 0;
        public int CurrentConfigIndex => _currentConfigIndex;

        private CasePrefabConfig CurrentConfig
        {
            get
            {
                if (_configs == null || _configs.Length == 0) return null;
                int idx = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
                return _configs[idx];
            }
        }

        public void SwitchConfig(int index)
        {
            if (_configs == null || index < 0 || index >= _configs.Length) return;
            if (index == _currentConfigIndex) return;
            _currentConfigIndex = index;
            _building?.RefreshAllMeshes();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Vector3 scale = Vector3.one / unit;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(x, y, z, matrix, this);
            transform.localScale = scale;

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
        }

        private void OnDestroy()
        {
            _pointCubes = null;
        }

        private void OnDrawGizmos()
        {
            if (showPoint)
                _building?.DrawPoints();
        }

        // ── Config UI (OnGUI) ─────────────────────────────────────────────────

        public string GetConfigName(int index) =>
            (_configs != null && index >= 0 && index < _configs.Length && _configs[index] != null)
                ? _configs[index].name : "";

        // ── Interactive building ──────────────────────────────────────────────

        /// <summary>BuildState 从地形射线命中点调用，映射到 y=1 格层建造。</summary>
        public void TryCreateAtGround(Vector3 worldHitPoint)
        {
            var local = transform.InverseTransformPoint(worldHitPoint);
            int cx = Mathf.RoundToInt(local.x);
            int cz = Mathf.RoundToInt(local.z);
            CreateCube(cx, 1, cz);
        }

        public void OnClicked(PointElement element, bool left, in Vector3 normal)
        {
            if (left)
            {
                if (element is PointQuad quad)
                {
                    CreateCube(quad.x, 1, quad.z);
                }
                else if (element is PointCube cube)
                {
                    Vector3 local = cube.transform.InverseTransformVector(normal).normalized;
                    Vector3 coord = new Vector3(cube.x, cube.y, cube.z) + local;
                    CreateCube(
                        Mathf.RoundToInt(coord.x),
                        Mathf.RoundToInt(coord.y),
                        Mathf.RoundToInt(coord.z));
                }
            }
            else
            {
                if (element is PointCube cube)
                    DestroyCube(cube);
            }
        }

        public void SetPoint(int px, int py, int pz, bool active)
        {
            _building?.SetPointStatus(px, py, pz, active);
        }

        private void CreateCube(int cx, int cy, int cz)
        {
            if (cx <= 0 || cy <= 0 || cz <= 0) return;
            if (cx >= _building.X || cy >= _building.Y || cz >= _building.Z) return;

            ref var cube = ref _pointCubes[cx, cy, cz];
            if (cube != null) return;

            GameObject go = Instantiate(pointCubePrefab);
            Transform t = go.transform;
            t.SetParent(transform);
            t.localPosition = new Vector3(cx, cy, cz);
            t.localRotation = Quaternion.identity;
            t.localScale    = Vector3.one;

            cube = go.GetComponent<PointCube>();
            cube.mcs = this;
            cube.x = cx; cube.y = cy; cube.z = cz;
            _building.SetPointStatus(cx, cy, cz, true);
        }

        private void DestroyCube(PointCube cube)
        {
            _building.SetPointStatus(cube.x, cube.y, cube.z, false);
            DestroyImmediate(cube.gameObject);
            _pointCubes[cube.x, cube.y, cube.z] = null;
        }

        // ── IMeshStore ────────────────────────────────────────────────────────

        GameObject IMeshStore.GetMesh(int cubeIndex)
        {
            var config = CurrentConfig;
            if (config == null) return null;
            var prefab = config.GetPrefab(cubeIndex);
            if (prefab == null) return null;
            return Object.Instantiate(prefab);
        }
    }
}
