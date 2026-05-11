using UnityEngine;
using TerrainController = MarchingSquares.TerrainController;

namespace MarchingCubes.Sample
{
    public enum BuildMode { Terrain, Build }

    /// <summary>
    /// 建造系统总管：持有 BuildController 引用、驱动状态机、渲染模式切换 UI。
    /// 每次只激活一个 BuildController，交互逻辑全部封装在各 Controller 内部。
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [Header("建造区域（渲染格数，顶点数 = 渲染格数 + 1）")]
        [SerializeField] private int _areaWidth   = 10;
        [SerializeField] private int _areaDepth   = 10;
        [SerializeField] private int _buildHeight  = 5;

        [Header("组件引用")]
        [SerializeField] private TerrainController terrain;
        [SerializeField] private McController   structure;
        [SerializeField] private KeyCode   _switchKey   = KeyCode.Tab;
        [SerializeField] private BuildMode _initialMode = BuildMode.Build;

        public BuildMode CurrentMode { get; private set; }

        private IBuildState[] _states;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;

            terrain.Init(_areaWidth, _areaDepth, _buildHeight);
            structure.Init(_areaWidth, _buildHeight, _areaDepth);

            structure.SetTerrain(terrain.Builder);
            terrain.SetSyncCallback(() => structure.SyncWithTerrain(terrain.Builder));

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
            _states[(int)CurrentMode].OnGUI();
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
            float y = Screen.height - btnH - pad;

            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    CurrentMode == BuildMode.Terrain ? "[ 刷子 ]" : "  刷子  "))
                SwitchTo(BuildMode.Terrain);

            if (GUI.Button(new Rect(pad + btnW + gap, y, btnW, btnH),
                    CurrentMode == BuildMode.Build ? "[ 建造 ]" : "  建造  "))
                SwitchTo(BuildMode.Build);

            GUI.Label(new Rect(pad, y - 22f, 260f, 18f),
                $"[{_switchKey}] 切换  当前：{(CurrentMode == BuildMode.Terrain ? "地形刷子" : "Cube 建造")}");
        }
    }
}
