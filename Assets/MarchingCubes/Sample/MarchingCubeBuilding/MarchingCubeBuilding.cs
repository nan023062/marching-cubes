using UnityEngine;

namespace MarchingCubes.Sample
{
    public class MarchingCubeBuilding : MonoBehaviour, IMeshStore
    {
        public int x, y, z;
        public uint unit = 1;

        [SerializeField] private ArtMeshCaseConfig _config;
        [SerializeField] private GameObject pointCubePrefab;
        [SerializeField] private GameObject pointQuadPrefab;
        [SerializeField] private Material planMaterial;
        [SerializeField] private bool showPoint;
        [SerializeField] private bool debugCube;

        private BlockBuilding _building;
        private PointCube[,,] _pointCubes;
        private static readonly Vector3 s_center = new Vector3(0.5f, 0.5f, 0.5f);

        private void Awake()
        {
            Vector3 scale = Vector3.one / unit;
            Matrix4x4 matrix = Matrix4x4.TRS(transform.position, Quaternion.identity, scale);
            _building = new BlockBuilding(x, y, z, matrix, this);
            transform.localScale = scale;

            CreatePlan();

            _pointCubes = new PointCube[x + 1, y + 1, z + 1];
            for (int i = 1; i < x; i++)
            {
                for (int j = 1; j < z; j++)
                {
                    GameObject go = Instantiate(pointQuadPrefab);
                    Transform t = go.transform;
                    t.SetParent(transform);
                    t.localPosition = new Vector3(i, 0.5f, j);
                    t.localRotation = Quaternion.identity;
                    t.localScale    = new Vector3(1, 0, 1);
                    var quad = go.GetComponent<PointQuad>();
                    quad.marchingCubes = this;
                    quad.x = i;
                    quad.z = j;
                }
            }
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

        private void CreatePlan()
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "plan";
            Object.Destroy(go.GetComponent<MeshCollider>());
            if (planMaterial != null)
                go.GetComponent<MeshRenderer>().sharedMaterial = planMaterial;

            var t = go.transform;
            t.SetParent(transform);
            t.localPosition = new Vector3(x * 0.5f, 0.498f, z * 0.5f);
            t.localRotation = Quaternion.Euler(90f, 0f, 0f);
            t.localScale    = new Vector3(x - 1, z - 1, 1f);
        }

        // ── Interactive building ──────────────────────────────────────────────

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
            else if (Input.GetMouseButtonUp(1))
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
            cube.marchingCubes = this;
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
            if (_config == null) return null;
            var prefab = _config.GetPrefab(cubeIndex);
            if (prefab == null) return null;
            // p_case_xx 已归一化：旋转/坐标均已烘焙，直接 Instantiate 即可
            return Object.Instantiate(prefab);
        }
    }
}
