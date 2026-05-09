using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形 MonoBehaviour 薄壳（类比 MCBuilding）。
    /// 持有 MQTerrainBuilder，提供 Unity 组件层（MeshCollider / Brush / Config）。
    /// </summary>
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MQTerrain : MonoBehaviour
    {
        [SerializeField] private Brush _brush;
        [SerializeField, Header("涂刷类型 0=泥 1=草 2=岩 3=雪 4=腐"), Range(0, 4)]
        private int _textureLayer = 1;

        public Brush            Brush        => _brush;
        public int              TextureLayer { get => _textureLayer; set => _textureLayer = value; }
        public MQTerrainBuilder Builder      { get; private set; }

        private MeshFilter   _meshFilter;
        private MeshCollider _meshCollider;

        // ── 由 BuildingManager 驱动初始化 ────────────────────────────────────

        public void Init(int renderWidth, int renderDepth, int heightRange, MQMeshConfig config)
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();

            float unit = 1f / BuildingConst.Unit;
            Builder = new MQTerrainBuilder(
                renderWidth, renderDepth, heightRange,
                unit, transform.position, config, transform);
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

        public void SetBrushVisible(bool visible)
        {
            if (_brush != null) _brush.gameObject.SetActive(visible);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnDrawGizmos() => Builder?.DrawGizmos();
        private void OnDestroy()    { Builder = null; }
    }
}
