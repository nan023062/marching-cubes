using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形 MonoBehaviour 薄壳（类比 McStructure）。
    /// 持有 MqTerrainBuilder，提供 Unity 组件层（MeshCollider / Brush / Config）。
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class Terrain : MonoBehaviour
    {
        [SerializeField] private Brush           _brush;
        [SerializeField] private TileCaseConfig  _meshConfig;
        [SerializeField, Header("涂刷类型 (0~7，bit 位越高视觉权重越大)"), Range(0, 7)]
        private int _textureLayer = 1;

        public Brush            Brush        => _brush;
        public int              TextureLayer { get => _textureLayer; set => _textureLayer = value; }
        public TerrainBuilder Builder      { get; private set; }

        private MeshFilter   _meshFilter;
        private MeshCollider _meshCollider;

        // ── 由 BuildingManager 驱动初始化 ────────────────────────────────────

        public void Init(int renderWidth, int renderDepth, int heightRange)
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
            GetComponent<MeshRenderer>().enabled = false; // 视觉由 tile prefab 负责，MeshFilter 仅供 MeshCollider 射线检测

            float unit = 1f / BuildingConst.Unit;
            Builder = new TerrainBuilder(
                renderWidth, renderDepth, heightRange,
                unit, transform.position, _meshConfig, transform);
            Builder.MaxHeightDiff = BuildingConst.TerrainMaxHeightDiff * BuildingConst.Unit;

            transform.localScale     = Vector3.one * unit;
            _meshFilter.sharedMesh   = Builder.colliderMesh;
            _meshCollider.sharedMesh = Builder.colliderMesh;

            Builder.RefreshAllTiles();
        }

        // ── TerrainState 调用的接口 ───────────────────────────────────────────

        public bool BrushMapHigh(int delta)
        {
            bool dirty = Builder.BrushMapHigh(_brush, delta);
            if (dirty)
            {
                _meshFilter.sharedMesh   = Builder.colliderMesh;
                _meshCollider.sharedMesh = Builder.colliderMesh;
            }
            return dirty;
        }

        public bool PaintTerrainType(int type)
            => Builder.PaintTerrainType(_brush, type);

        public bool EraseTerrainType(int type)
            => Builder.EraseTerrainType(_brush, type);

        public bool ClearTerrainMask()
            => Builder.ClearTerrainMask(_brush);

        public void SetBrushVisible(bool visible)
        {
            if (_brush != null) _brush.gameObject.SetActive(visible);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnDrawGizmos() => Builder?.DrawGizmos();
        private void OnDestroy()    { Builder = null; }
    }
}
