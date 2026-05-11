using UnityEngine;
using UnityEngine.EventSystems;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// MC 结构体渲染节点 + 参数配置节点（类比 MqTerrain）。
    /// 只负责：持有预制体引用、config 配置、IMeshStore、Transform 坐标系初始化。
    /// 建造逻辑（McStructureBuilder、PointCube/Quad、点击处理）全部在 BuildState 中实现。
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class Structure : MonoBehaviour, IPointerClickHandler
    {
        public uint unit = BuildingConst.Unit;

        [SerializeField] private CasePrefabConfig[] _configs;
        [SerializeField] private int _currentConfigIndex = 0;

        [SerializeField] private GameObject _pointCubePrefab;

        public GameObject PointCubePrefab => _pointCubePrefab;

        public StructureBuilder Builder { get; set; }

        // ── 尺寸（由 BuildingManager.Init 注入，BuildState 读取）────────────

        public int RenderWidth  { get; private set; }
        public int BuildHeight  { get; private set; }
        public int RenderDepth  { get; private set; }

        // ── Config 访问 ───────────────────────────────────────────────────────

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
            _onConfigChanged?.Invoke();
        }

        // ── 委托桥（BuildState 注入，PointElement.mcs 回调时转发）───────────

        private System.Action<PointElement, bool, Vector3> _clickHandler;
        private System.Action<Vector3, bool>               _gridClickHandler;
        private System.Action                              _onConfigChanged;

        public void SetBuildHandlers(
            System.Action<PointElement, bool, Vector3> clickHandler,
            System.Action<Vector3, bool>               gridClickHandler,
            System.Action                              onConfigChanged)
        {
            _clickHandler     = clickHandler;
            _gridClickHandler = gridClickHandler;
            _onConfigChanged  = onConfigChanged;
        }

        public void OnClicked(PointElement element, bool left, in Vector3 normal)
            => _clickHandler?.Invoke(element, left, normal);

        void IPointerClickHandler.OnPointerClick(PointerEventData eventData)
        {
            bool left = eventData.button == PointerEventData.InputButton.Left;
            _gridClickHandler?.Invoke(eventData.pointerCurrentRaycast.worldPosition, left);
        }

        // ── 初始化（由 BuildingManager 驱动，只做 Transform 坐标系设置）──────

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

        // ── IMeshStore ────────────────────────────────────────────────────────

        public GameObject GetMesh(int cubeIndex)
        {
            if (_configs == null || _configs.Length == 0) return null;
            int idx    = Mathf.Clamp(_currentConfigIndex, 0, _configs.Length - 1);
            var prefab = _configs[idx]?.GetPrefab(cubeIndex);
            return prefab != null ? Object.Instantiate(prefab) : null;
        }
    }
}
