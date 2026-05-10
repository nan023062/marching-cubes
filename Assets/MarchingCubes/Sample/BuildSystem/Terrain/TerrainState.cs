using UnityEngine;
using MarchingSquares;
using Terrain = MarchingSquares.Terrain;

namespace MarchingCubes.Sample
{
    public class TerrainState : IBuildState
    {
        readonly Terrain _sample;
        readonly int                  _terrainMask;
        readonly System.Action        _onTerrainChanged;

        // 5 种 type（与 atlas layer 0~4 对齐）
        static readonly string[] LayerNames = { "泥", "草", "岩", "雪", "紫" };

        float _pressTime   = -1f;
        int   _pressButton = -1;
        const float ClickMaxDuration = 0.3f;

        public TerrainState(Terrain sample, System.Action onTerrainChanged)
        {
            _sample           = sample;
            _terrainMask      = 1 << LayerMask.NameToLayer("MarchingQuads");
            _onTerrainChanged = onTerrainChanged;
        }

        public void OnEnter() => _sample.SetBrushVisible(true);
        public void OnExit()
        {
            _sample.SetBrushVisible(false);
            _pressTime   = -1f;
            _pressButton = -1;
        }

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
                // 「清空」按钮：与叠加 / 擦除并列，笔刷范围内所有 type 一键清掉，恢复 base
                float clearBtnX = pad + LayerNames.Length * (typeBtnW + gap) + gap;
                if (GUI.Button(new Rect(clearBtnX, y, btnW, btnH), "清空"))
                {
                    if (_sample.ClearTerrainMask()) _onTerrainChanged?.Invoke();
                }
                // 操作提示：左键 paint（add），右键 erase（清除当前 type 的 bit），清空按钮 = 全部 type 清掉
                y -= btnH + gap;
                GUI.Label(new Rect(pad, y, 480f, btnH),
                    "左键: 叠加当前 type   |   右键: 擦除当前 type   |   清空: 笔刷内所有 type 归零");
            }
        }

        public void OnUpdate()
        {
            var brush = _sample.Brush;
            if (brush == null || Camera.main == null) return;
            Transform t = brush.transform;
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

            if (Physics.Raycast(ray, out var hit, 1000f, _terrainMask))
            {
                var lw = _sample.Builder.localToWorld;
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

            float unit = 1f / BuildingConst.Unit;
            var p = t.position;
            p.x = Mathf.RoundToInt(p.x / unit) * unit;
            p.z = Mathf.RoundToInt(p.z / unit) * unit;
            t.position = p;

            // 记录最近一次按下的按键和时刻
            for (int btn = 0; btn <= 1; btn++)
                if (Input.GetMouseButtonDown(btn))
                {
                    _pressTime   = Time.time;
                    _pressButton = btn;
                }

            // 抬起时判断是否为短按（点击）；长按抬起不触发
            int clickBtn = -1;
            for (int btn = 0; btn <= 1; btn++)
            {
                if (Input.GetMouseButtonUp(btn) && _pressButton == btn
                    && _pressTime >= 0 && Time.time - _pressTime < ClickMaxDuration)
                {
                    clickBtn     = btn;
                    _pressTime   = -1f;
                    _pressButton = -1;
                    break;
                }
            }
            if (clickBtn < 0) return;

            bool dirty;
            if (brush.colorBrush)
            {
                // 左键叠加当前 type（Add 语义），右键擦除当前 type（Erase 语义）
                int type = _sample.TextureLayer;
                dirty = clickBtn == 0
                    ? _sample.PaintTerrainType(type)
                    : _sample.EraseTerrainType(type);
            }
            else
            {
                int delta = clickBtn == 0 ? 1 : -1;
                dirty = _sample.BrushMapHigh(delta);
            }
            if (dirty) _onTerrainChanged?.Invoke();
        }
    }
}
