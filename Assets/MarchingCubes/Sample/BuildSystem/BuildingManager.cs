using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public enum BuildMode { Terrain, Build }

    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Header("建造区域（渲染格数，顶点数 = 渲染格数 + 1）")]
        [SerializeField] private int _areaWidth  = 10;
        [SerializeField] private int _areaDepth  = 10;
        [SerializeField] private int _buildHeight = 5;

        [Header("组件引用")]
        [SerializeField] private TileController terrain;
        [SerializeField] private CubeController structure;
        [SerializeField] private KeyCode   _switchKey   = KeyCode.Tab;
        [SerializeField] private BuildMode _initialMode = BuildMode.Terrain;

        [Header("Grid 渲染")]
        [SerializeField] private Material _gridMaterial;

        public Material GridMaterial => _gridMaterial;

        // ── 数据层 ─────────────────────────────────────────────────────────

        public TileBuilder Tiles { get; private set; }
        public CubeBuilder Cubes { get; private set; }

        public BuildMode CurrentMode { get; private set; }

        private IBuildState[] _states;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            float unit = 1f / BuildingConst.Unit;

            Tiles = new TileBuilder(_areaWidth, _areaDepth, _buildHeight, unit, terrain.transform.position);
            Tiles.MaxHeightDiff = BuildingConst.TerrainMaxHeightDiff * BuildingConst.Unit;

            var cubePos    = structure.transform.position - new Vector3(0.5f, 0.5f, 0.5f);
            var cubeMatrix = Matrix4x4.TRS(cubePos, Quaternion.identity, Vector3.one / BuildingConst.Unit);
            Cubes = new CubeBuilder(_areaWidth + 1, _buildHeight, _areaDepth + 1, cubeMatrix);

            terrain.Init(_areaWidth, _areaDepth, _buildHeight, Tiles);
            structure.Init(_areaWidth, _buildHeight, _areaDepth, Cubes, Tiles);
            terrain.SetSyncCallback(() => structure.SyncWithTerrain());

            _states = new IBuildState[] { terrain, structure };
        }

        private void Start()
        {
            CurrentMode = _initialMode;
            _states[(int)CurrentMode].OnEnter();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (Input.GetKeyDown(_switchKey))
                SwitchTo(CurrentMode == BuildMode.Terrain ? BuildMode.Build : BuildMode.Terrain);

            _states[(int)CurrentMode].OnUpdate();
        }

        private void OnGUI()
        {
            DrawSwitchUI();
            _states[(int)CurrentMode].DrawGUI();
        }

        // ── State machine ─────────────────────────────────────────────────────

        public void SwitchTo(BuildMode mode)
        {
            if (mode == CurrentMode) return;
            _states[(int)CurrentMode].OnExit();
            CurrentMode = mode;
            _states[(int)CurrentMode].OnEnter();
        }

        // ── UI ────────────────────────────────────────────────────────────────

        private void DrawSwitchUI()
        {
            const float btnW = 100f, btnH = 32f, pad = 8f, gap = 4f;
            float y = Screen.height - btnH - 12f;

            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    CurrentMode == BuildMode.Terrain ? "[ 地形 ]" : "  地形  "))
                SwitchTo(BuildMode.Terrain);

            if (GUI.Button(new Rect(pad + btnW + gap, y, btnW, btnH),
                    CurrentMode == BuildMode.Build ? "[ 建造 ]" : "  建造  "))
                SwitchTo(BuildMode.Build);

            GUI.Label(new Rect(pad, y - 28f, 320f, 20f),
                $"[{_switchKey}] 切换  当前：{(CurrentMode == BuildMode.Terrain ? "地形" : "建造")}");
            GUI.Label(new Rect(pad, y - 56f, 440f, 20f),
                "WASD 移动  滚轮 缩放  按住右键拖动 转视角");
        }
    }
}
