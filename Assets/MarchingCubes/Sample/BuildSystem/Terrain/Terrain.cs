using UnityEngine;
using MarchingCubes.Sample;

namespace MarchingSquares
{
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class Terrain : MonoBehaviour
    {
        [SerializeField] private Brush          _brush;
        [SerializeField] private TileCaseConfig _meshConfig;
        [SerializeField, Header("涂刷类型 (0~7，bit 位越高视觉权重越大)"), Range(0, 7)]
        private int _textureLayer = 1;

        public Brush          Brush        => _brush;
        public int            TextureLayer { get => _textureLayer; set => _textureLayer = value; }
        public TerrainBuilder Builder      { get; private set; }

        private MeshFilter   _meshFilter;
        private MeshCollider _meshCollider;
        private Mesh         _gridMesh;
        private int _terrainMask;
        private int _pressButton = -1;

        // ── 指针事件（TerrainState 订阅）────────────────────────────────────
        // px/pz：已 snap 到格点索引
        public event System.Action<int, int>       OnPointMove;
        public event System.Action<int, int, bool> OnPointClicked; // px, pz, left

        public void Init(int renderWidth, int renderDepth, int heightRange)
        {
            _meshFilter   = GetComponent<MeshFilter>();
            _meshCollider = GetComponent<MeshCollider>();
            _terrainMask  = 1 << LayerMask.NameToLayer("MarchingQuads");

            float unit = 1f / BuildingConst.Unit;
            Builder = new TerrainBuilder(
                renderWidth, renderDepth, heightRange,
                unit, transform.position, _meshConfig, transform);
            Builder.MaxHeightDiff = BuildingConst.TerrainMaxHeightDiff * BuildingConst.Unit;

            transform.localScale     = Vector3.one * unit;
            _meshCollider.sharedMesh = Builder.colliderMesh;

            _gridMesh = new Mesh { name = "TerrainGrid" };
            _meshFilter.sharedMesh = _gridMesh;

            Builder.RefreshAllTiles();
            RebuildGridMesh();
        }

        // ── Raycast + 事件派发 ───────────────────────────────────────────────

        void Update()
        {
            if (OnPointMove == null && OnPointClicked == null) return;
            if (Camera.main == null) return;

            float unit      = 1f / BuildingConst.Unit;
            Ray   ray       = Camera.main.ScreenPointToRay(Input.mousePosition);
            bool  onTerrain = Physics.Raycast(ray, out var hit, 1000f, _terrainMask);

            // ── 笔刷 hover ────────────────────────────────────────────────────
            Vector3 pos;
            if (onTerrain)
            {
                pos = hit.point;
            }
            else
            {
                var   bt       = _brush != null ? _brush.transform.position : Vector3.zero;
                float northDis = Vector3.Project(bt - ray.origin, Vector3.up).magnitude;
                float cos      = Vector3.Dot(Vector3.down, ray.direction);
                pos = Mathf.Abs(cos) > 0.001f ? ray.origin + ray.direction * (northDis / cos) : bt;
            }

            int px = Mathf.RoundToInt(pos.x / unit);
            int pz = Mathf.RoundToInt(pos.z / unit);
            OnPointMove?.Invoke(px, pz);

            // ── click 检测 ────────────────────────────────────────────────────
            if (!onTerrain) { _pressButton = -1; return; }

            for (int btn = 0; btn <= 1; btn++)
                if (Input.GetMouseButtonDown(btn)) _pressButton = btn;

            for (int btn = 0; btn <= 1; btn++)
            {
                if (Input.GetMouseButtonUp(btn) && _pressButton == btn)
                {
                    _pressButton = -1;
                    OnPointClicked?.Invoke(px, pz, btn == 0);
                    break;
                }
            }
        }

        // ── 地形操作 ─────────────────────────────────────────────────────────

        public bool BrushMapHigh(int delta)
        {
            bool dirty = Builder.BrushMapHigh(_brush, delta);
            if (dirty)
            {
                _meshCollider.sharedMesh = Builder.colliderMesh;
                RebuildGridMesh();
            }
            return dirty;
        }

        public bool PaintTerrainType(int type)            => Builder.PaintTerrainType(_brush, type);
        public bool EraseTerrainType(int type)            => Builder.EraseTerrainType(_brush, type);
        public bool ClearTerrainMask()                    => Builder.ClearTerrainMask(_brush);
        public bool IsCellFlat(int cx, int cz, out int h) => Builder.IsCellFlat(cx, cz, out h);
        public void SetBrushVisible(bool visible)          { if (_brush != null) _brush.gameObject.SetActive(visible); }

        // ── 网格线重建 ───────────────────────────────────────────────────────

        void RebuildGridMesh()
        {
            int W = Builder.width, L = Builder.length;
            int cols = W + 1, rows = L + 1;

            var verts = new Vector3[cols * rows];
            for (int x = 0; x < cols; x++)
            for (int z = 0; z < rows; z++)
                verts[x * rows + z] = new Vector3(x, Builder.GetPointHeight(x, z), z);

            int segCount = cols * L + rows * W;
            var idx = new int[segCount * 2];
            int i = 0;

            for (int x = 0; x < cols; x++)
            for (int z = 0; z < L; z++)
            { idx[i++] = x * rows + z; idx[i++] = x * rows + z + 1; }

            for (int z = 0; z < rows; z++)
            for (int x = 0; x < W; x++)
            { idx[i++] = x * rows + z; idx[i++] = (x + 1) * rows + z; }

            _gridMesh.Clear();
            _gridMesh.vertices = verts;
            _gridMesh.SetIndices(idx, MeshTopology.Lines, 0);
            _gridMesh.RecalculateBounds();
        }

        private void OnDrawGizmos()
        {
            if (Application.isPlaying &&
                BuildingManager.Instance?.CurrentMode != BuildMode.Terrain) return;
            Builder?.DrawGizmos();
        }

        private void OnDestroy() { Builder = null; }
    }
}
