using UnityEngine;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Structure : MonoBehaviour
    {
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        public StructureBuilder Builder { get; set; }

        public int RenderWidth  { get; private set; }
        public int BuildHeight  { get; private set; }
        public int RenderDepth  { get; private set; }

        // ── 事件 ─────────────────────────────────────────────────────────────
        // src = 被点击的 MC 格点（source），adj = 法线方向相邻格点（build target）
        public event System.Action<Vector3Int, Vector3Int, bool> OnBuildGridClicked;
        public event System.Action                               OnConfigChanged;

        private int _pressButton = -1;

        // ── Config ───────────────────────────────────────────────────────────

        public int ConfigCount        => _configs != null ? _configs.Length : 0;
        public int CurrentConfigIndex => _currentConfigIndex;

        public string GetConfigName(int index) =>
            (_configs != null && index >= 0 && index < _configs.Length && _configs[index] != null)
                ? _configs[index].name : "";

        public void SwitchConfig(int index)
        {
            if (_configs == null || index < 0 || index >= _configs.Length) return;
            if (index == _currentConfigIndex) return;
            _currentConfigIndex = index;
            OnConfigChanged?.Invoke();
        }

        // ── Raycast：hit 在自身 MeshCollider → 推算 src / adj → 派发事件 ─────
        void Update()
        {
            if (OnBuildGridClicked == null) return;
            if (Camera.main == null) return;

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 1000f)) return;
            if (hit.transform != transform) return;

            for (int btn = 0; btn <= 1; btn++)
                if (Input.GetMouseButtonDown(btn)) _pressButton = btn;

            for (int btn = 0; btn <= 1; btn++)
            {
                if (Input.GetMouseButtonUp(btn) && _pressButton == btn)
                {
                    _pressButton = -1;
                    FireBuildClick(hit, btn == 0);
                    break;
                }
            }
        }

        void FireBuildClick(RaycastHit hit, bool left)
        {
            // 公式：inside = hit.point - normal * 0.5
            // 法线轴：src = RoundToInt(inside + 0.5)
            // 非法线轴：src = FloorToInt(inside) + 1
            Vector3 n  = hit.normal;
            Vector3 ip = hit.point - n * 0.5f;

            int srcX = Mathf.Abs(n.x) > 0.5f ? Mathf.RoundToInt(ip.x + 0.5f) : Mathf.FloorToInt(ip.x) + 1;
            int srcY = Mathf.Abs(n.y) > 0.5f ? Mathf.RoundToInt(ip.y + 0.5f) : Mathf.FloorToInt(ip.y) + 1;
            int srcZ = Mathf.Abs(n.z) > 0.5f ? Mathf.RoundToInt(ip.z + 0.5f) : Mathf.FloorToInt(ip.z) + 1;

            var src = new Vector3Int(srcX, srcY, srcZ);
            var adj = src + new Vector3Int(
                Mathf.RoundToInt(n.x),
                Mathf.RoundToInt(n.y),
                Mathf.RoundToInt(n.z));

            OnBuildGridClicked?.Invoke(src, adj, left);
        }

        // ── 初始化 ───────────────────────────────────────────────────────────

        public void Init(int renderWidth, int buildHeight, int renderDepth)
        {
            RenderWidth = renderWidth;
            BuildHeight = buildHeight;
            RenderDepth = renderDepth;
            transform.localScale = Vector3.one / unit;
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying &&
                BuildingManager.Instance?.CurrentMode != BuildMode.Build) return;
            Builder?.DrawGizmos();
        }

        public GameObject GetMesh(int cubeIndex)
        {
            if (_configs == null || _configs.Length == 0) return null;
            int idx    = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
            var prefab = _configs[idx]?.GetPrefab(cubeIndex);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}
