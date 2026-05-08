using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MCTerrain : MonoBehaviour
    {
        [SerializeField, Header("涂刷类型 0=泥 1=草 2=岩 3=雪 4=腐"), Range(0, 4)]
        private int textureLayer = 1;

        [SerializeField] private Brush bush;

        private MSQTerrain   _terrain;
        private MeshCollider _meshCollider;
        private MeshFilter   _meshFilter;

        // ── 供 BuildingManager / TerrainState 使用的接口 ─────────────────────

        public MSQTerrain Terrain      => _terrain;
        public Brush      Brush        => bush;
        public int        TextureLayer { get => textureLayer; set => textureLayer = value; }

        public void SetBrushVisible(bool visible)
        {
            if (bush != null) bush.gameObject.SetActive(visible);
        }

        public void RefreshMeshes()
        {
            _meshFilter.sharedMesh   = _terrain.mesh;
            _meshCollider.sharedMesh = _terrain.mesh;
        }

        // ── 由 BuildingManager 驱动初始化 ────────────────────────────────────

        public void Init(int renderWidth, int renderDepth, int heightRange)
        {
            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter   = GetComponent<MeshFilter>();
            Transform t   = transform;
            float unit    = 1f / BuildingConst.Unit;
            _terrain      = new MSQTerrain(renderWidth, renderDepth, heightRange, unit, t.position);
            _terrain.MaxHeightDiff = BuildingConst.TerrainMaxHeightDiff * BuildingConst.Unit;
            t.localScale  = _terrain.localToWorld.lossyScale;
            _meshFilter.sharedMesh   = _terrain.mesh;
            _meshCollider.sharedMesh = _terrain.mesh;
        }

        public void OnDrawGizmos()
        {
            if (_terrain == null) return;
            Color color   = Gizmos.color;
            Gizmos.color  = Color.black;
            Gizmos.matrix = _terrain.localToWorld;
            _terrain.OnDrawGizmos();
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color  = color;
        }

        public void OnDestroy()
        {
            _meshCollider = null;
            _terrain      = null;
        }
    }
}
