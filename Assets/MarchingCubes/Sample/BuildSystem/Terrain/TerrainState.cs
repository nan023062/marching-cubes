using UnityEngine;
using MarchingSquares;
using Terrain = MarchingSquares.Terrain;

namespace MarchingCubes.Sample
{
    public class TerrainState : IBuildState
    {
        readonly Terrain       _sample;
        readonly System.Action _onTerrainChanged;

        static readonly string[] LayerNames = { "泥", "草", "岩", "雪", "紫" };

        public TerrainState(Terrain sample, System.Action onTerrainChanged)
        {
            _sample           = sample;
            _onTerrainChanged = onTerrainChanged;
        }

        public void OnEnter()
        {
            _sample.SetBrushVisible(true);
            _sample.OnPointMove    += HandlePointMove;
            _sample.OnPointClicked += HandlePointClicked;
        }

        public void OnExit()
        {
            _sample.SetBrushVisible(false);
            _sample.OnPointMove    -= HandlePointMove;
            _sample.OnPointClicked -= HandlePointClicked;
        }

        // ── 笔刷位置（吸附到格点高度）────────────────────────────────────────

        void HandlePointMove(int px, int pz)
        {
            if (_sample.Brush == null) return;
            float unit = 1f / BuildingConst.Unit;
            var   lw   = _sample.Builder.localToWorld;
            var   t    = _sample.Brush.transform;
            t.position   = new Vector3(px * unit, _sample.Builder.GetPointHeight(px, pz) * unit + 0.01f, pz * unit);
            t.localScale = lw.lossyScale;
            t.rotation   = lw.rotation;
        }

        // ── 地形编辑 ─────────────────────────────────────────────────────────

        void HandlePointClicked(int px, int pz, bool left)
        {
            bool dirty;
            var  brush = _sample.Brush;
            if (brush.colorBrush)
            {
                int type = _sample.TextureLayer;
                dirty = left ? _sample.PaintTerrainType(type) : _sample.EraseTerrainType(type);
            }
            else
            {
                dirty = _sample.BrushMapHigh(left ? 1 : -1);
            }
            if (dirty) _onTerrainChanged?.Invoke();
        }

        public void OnUpdate() { }

        public void OnGUI()
        {
            const float btnW = 80f, btnH = 28f, pad = 8f, gap = 4f;
            float y = Screen.height - btnH * 3 - pad * 3 - 22f;
            var brush = _sample.Brush;

            if (GUI.Button(new Rect(pad, y, btnW, btnH),
                    !brush.colorBrush ? "[ 高度 ]" : "  高度  "))
                brush.colorBrush = false;

            if (GUI.Button(new Rect(pad + btnW + gap, y, btnW, btnH),
                    brush.colorBrush ? "[ 涂色 ]" : "  涂色  "))
                brush.colorBrush = true;

            if (brush.colorBrush)
            {
                y -= btnH + gap;
                float typeBtnW = btnW * 0.55f;
                for (int i = 0; i < LayerNames.Length; i++)
                {
                    bool sel = (_sample.TextureLayer == i);
                    if (GUI.Button(new Rect(pad + i * (typeBtnW + gap), y, typeBtnW, btnH),
                            sel ? $"[{LayerNames[i]}]" : LayerNames[i]))
                        _sample.TextureLayer = i;
                }
                float clearBtnX = pad + LayerNames.Length * (typeBtnW + gap) + gap;
                if (GUI.Button(new Rect(clearBtnX, y, btnW, btnH), "清空"))
                    if (_sample.ClearTerrainMask()) _onTerrainChanged?.Invoke();

                y -= btnH + gap;
                GUI.Label(new Rect(pad, y, 480f, btnH),
                    "左键: 叠加当前 type   |   右键: 擦除当前 type   |   清空: 笔刷内所有 type 归零");
            }
        }
    }
}
