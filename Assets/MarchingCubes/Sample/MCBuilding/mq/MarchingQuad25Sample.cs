using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MarchingQuad25Sample : MonoBehaviour
    {
        [SerializeField] private int width = 50;
        [SerializeField] private int height = 10;
        [SerializeField] private int pow = BuildingConst.Unit;

        [SerializeField, Header("涂刷类型 0=泥 1=草 2=岩 3=雪 4=腐"), Range(0, 4)]
        private int textureLayer = 1;

        [SerializeField] private Brush bush;
        [SerializeField] private Material cliffMaterial;

        private MSQTerrain _terrain;
        private MeshCollider _meshCollider;
        private MeshFilter _meshFilter;
        private MeshFilter _cliffFilter;
        private MeshRenderer _cliffRenderer;

        // ── 供 BuildingManager / TerrainState 使用的接口 ─────────────────────

        public MSQTerrain Terrain      => _terrain;
        public Brush      Brush        => bush;
        public int        Pow          => pow;
        public int        TextureLayer { get => textureLayer; set => textureLayer = value; }

        public void SetBrushVisible(bool visible)
        {
            if (bush != null) bush.gameObject.SetActive(visible);
        }

        public void RefreshMeshes()
        {
            _meshFilter.sharedMesh   = _terrain.mesh;
            _meshCollider.sharedMesh = _terrain.mesh;
            _cliffFilter.sharedMesh  = _terrain.cliffMesh;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public void Awake()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _meshFilter   = GetComponent<MeshFilter>();
            Transform t   = transform;
            float unit    = 1f / pow;
            _terrain      = new MSQTerrain(width, width, height, unit, t.position);
            t.localScale  = _terrain.localToWorld.lossyScale;
            _meshFilter.sharedMesh   = _terrain.mesh;
            _meshCollider.sharedMesh = _terrain.mesh;

            var cliffGO = new GameObject("CliffWalls");
            cliffGO.transform.SetParent(t, false);
            _cliffFilter  = cliffGO.AddComponent<MeshFilter>();
            _cliffRenderer = cliffGO.AddComponent<MeshRenderer>();
            _cliffFilter.sharedMesh = _terrain.cliffMesh;
            if (cliffMaterial != null)
                _cliffRenderer.sharedMaterial = cliffMaterial;
        }

        public void OnDrawGizmos()
        {
            if (_terrain == null) return;
            Color color = Gizmos.color;
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
