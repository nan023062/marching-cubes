using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public enum BuildMode { Terrain, Build }

    /// <summary>
    /// 建造系统总管。对外暴露地形接口和 Cube 建造接口，
    /// 通过 TerrainState / BuildState 两个游戏状态管理输入和交互方式。
    /// </summary>
    public class BuildingManager : MonoBehaviour
    {
        public static BuildingManager Instance { get; private set; }

        [SerializeField] private MarchingQuad25Sample _terrain;
        [SerializeField] private MCBuilding           _building;
        [SerializeField] private KeyCode              _switchKey   = KeyCode.Tab;
        [SerializeField] private BuildMode            _initialMode = BuildMode.Build;

        public BuildMode CurrentMode { get; private set; }

        private IBuildState[] _states;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void Awake()
        {
            Instance = this;
            _states = new IBuildState[]
            {
                new TerrainState(this, _terrain),
                new BuildState(this, _building),
            };
        }

        private void Start()
        {
            // 直接赋值并调 OnEnter，不走 SwitchTo，避免初始 CurrentMode == _initialMode 时跳过
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

        // ── 地形接口 ──────────────────────────────────────────────────────────

        public bool BrushTerrain(int delta)
        {
            bool dirty = _terrain.Terrain.BrushMapHigh(_terrain.Brush, delta);
            if (dirty) _terrain.RefreshMeshes();
            return dirty;
        }

        public bool PaintTerrain(int type)
        {
            bool dirty = _terrain.Terrain.PaintTerrainType(_terrain.Brush, type);
            if (dirty) _terrain.RefreshMeshes();
            return dirty;
        }

        // ── Cube 建造接口 ─────────────────────────────────────────────────────

        public void CreateAtGround(Vector3 worldHit) => _building.TryCreateAtGround(worldHit);

        public void SwitchConfig(int index) => _building.SwitchConfig(index);

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

        // ═════════════════════════════════════════════════════════════════════
        // States
        // ═════════════════════════════════════════════════════════════════════

        interface IBuildState
        {
            void OnEnter();
            void OnExit();
            void OnUpdate();
            void OnGUI();
        }

        // ── TerrainState：刷子入口，全权管理地形输入与 GUI ──────────────────

        class TerrainState : IBuildState
        {
            readonly BuildingManager      _mgr;
            readonly MarchingQuad25Sample _sample;
            readonly int                  _terrainMask;

            static readonly string[] _layerNames = { "泥", "草", "岩", "雪", "腐" };

            // 焦点进入触发：刷子移入新格子才生效，停留不重复，必须移出再移入才再触发
            Vector3 _lastAppliedPos = new Vector3(float.NaN, 0f, float.NaN);

            public TerrainState(BuildingManager mgr, MarchingQuad25Sample sample)
            {
                _mgr         = mgr;
                _sample      = sample;
                _terrainMask = 1 << LayerMask.NameToLayer("MarchingQuads");
            }

            public void OnEnter() => _sample.SetBrushVisible(true);
            public void OnExit()  => _sample.SetBrushVisible(false);

            public void OnGUI()
            {
                const float btnW = 80f, btnH = 28f, pad = 8f, gap = 4f;
                // 高度刷子 / 涂色刷子 切换
                float y = Screen.height - btnH * 3 - pad * 3 - 22f;
                var brush = _sample.Brush;

                if (GUI.Button(new Rect(pad, y, btnW, btnH),
                        !brush.colorBrush ? "[ 高度 ]" : "  高度  "))
                    brush.colorBrush = false;

                if (GUI.Button(new Rect(pad + btnW + gap, y, btnW, btnH),
                        brush.colorBrush ? "[ 涂色 ]" : "  涂色  "))
                    brush.colorBrush = true;

                // 涂色模式下显示图层选择
                if (brush.colorBrush)
                {
                    y -= btnH + gap;
                    for (int i = 0; i < _layerNames.Length; i++)
                    {
                        bool sel = (_sample.TextureLayer == i);
                        if (GUI.Button(new Rect(pad + i * (btnW * 0.7f + gap), y, btnW * 0.7f, btnH),
                                sel ? $"[{_layerNames[i]}]" : _layerNames[i]))
                            _sample.TextureLayer = i;
                    }
                }
            }

            public void OnUpdate()
            {
                var brush = _sample.Brush;
                Transform t = brush.transform;
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

                if (Physics.Raycast(ray, out var hit, 1000f, _terrainMask))
                {
                    var lw = _sample.Terrain.localToWorld;
                    t.position   = hit.point;
                    t.localScale = lw.lossyScale;
                    t.rotation   = lw.rotation;
                }
                else
                {
                    float northDis = Vector3.Project(t.position - ray.origin, Vector3.up).magnitude;
                    float cos      = Vector3.Dot(Vector3.down, ray.direction);
                    if (Mathf.Abs(cos) > 0.001f)
                        t.position = ray.origin + ray.direction * (northDis / cos);
                }

                float unit = 1f / _sample.Pow;
                var p = t.position;
                p.x = Mathf.RoundToInt(p.x / unit) * unit;
                p.z = Mathf.RoundToInt(p.z / unit) * unit;
                t.position = p;

                // 抬起 = 失去焦点，重置触发位置
                if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
                {
                    _lastAppliedPos = new Vector3(float.NaN, 0f, float.NaN);
                    return;
                }
                if (p == _lastAppliedPos) return;
                _lastAppliedPos = p;

                if (brush.colorBrush)
                {
                    int type = Input.GetMouseButton(0) ? _sample.TextureLayer : 0;
                    _mgr.PaintTerrain(type);
                }
                else
                {
                    int delta = Input.GetMouseButton(0) ? 1 : -1;
                    _mgr.BrushTerrain(delta);
                }
            }
        }

        // ── BuildState：建造入口，全权管理 Cube 交互与 GUI ───────────────────

        class BuildState : IBuildState
        {
            readonly BuildingManager _mgr;
            readonly MCBuilding      _building;
            readonly int             _terrainMask;

            public BuildState(BuildingManager mgr, MCBuilding building)
            {
                _mgr         = mgr;
                _building    = building;
                _terrainMask = 1 << LayerMask.NameToLayer("MarchingQuads");
            }

            public void OnEnter() => _building.EnableInteraction(true);
            public void OnExit()  => _building.EnableInteraction(false);

            public void OnGUI()
            {
                int count = _building.ConfigCount;
                if (count <= 1) return;

                const float btnW = 140f, btnH = 28f, pad = 8f;
                float totalW = count * (btnW + pad) + pad;
                GUI.Box(new Rect(pad, pad, totalW, btnH + pad * 2), GUIContent.none);

                for (int i = 0; i < count; i++)
                {
                    string label = _building.GetConfigName(i);
                    Rect r = new Rect(pad + i * (btnW + pad), pad + pad * 0.5f, btnW, btnH);
                    if (i == _building.CurrentConfigIndex)
                        GUI.Box(r, label);
                    else if (GUI.Button(r, label))
                        _mgr.SwitchConfig(i);
                }
            }

            public void OnUpdate()
            {
                if (!Input.GetMouseButtonDown(0)) return;
                var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out var hit, 1000f, _terrainMask))
                    _mgr.CreateAtGround(hit.point);
            }
        }
    }
}
