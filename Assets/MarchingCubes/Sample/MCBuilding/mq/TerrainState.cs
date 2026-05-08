using UnityEngine;
using MarchingSquares;

namespace MarchingCubes.Sample
{
    public class TerrainState : IBuildState
    {
        readonly MarchingQuad25Sample _sample;
        readonly int                  _terrainMask;
        readonly System.Action        _onTerrainChanged;

        static readonly string[] LayerNames = { "泥", "草", "岩", "雪", "腐" };

        Vector3 _lastAppliedPos = new Vector3(float.NaN, 0f, float.NaN);

        public TerrainState(MarchingQuad25Sample sample, System.Action onTerrainChanged)
        {
            _sample           = sample;
            _terrainMask      = 1 << LayerMask.NameToLayer("MarchingQuads");
            _onTerrainChanged = onTerrainChanged;
        }

        public void OnEnter() => _sample.SetBrushVisible(true);
        public void OnExit()  => _sample.SetBrushVisible(false);

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
                for (int i = 0; i < LayerNames.Length; i++)
                {
                    bool sel = (_sample.TextureLayer == i);
                    if (GUI.Button(new Rect(pad + i * (btnW * 0.7f + gap), y, btnW * 0.7f, btnH),
                            sel ? $"[{LayerNames[i]}]" : LayerNames[i]))
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

            float unit = 1f / BuildingConst.Unit;
            var p = t.position;
            p.x = Mathf.RoundToInt(p.x / unit) * unit;
            p.z = Mathf.RoundToInt(p.z / unit) * unit;
            t.position = p;

            if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1))
            {
                _lastAppliedPos = new Vector3(float.NaN, 0f, float.NaN);
                return;
            }
            if (p == _lastAppliedPos) return;
            _lastAppliedPos = p;

            bool dirty;
            if (brush.colorBrush)
            {
                int type = Input.GetMouseButton(0) ? _sample.TextureLayer : 0;
                dirty = _sample.Terrain.PaintTerrainType(brush, type);
            }
            else
            {
                int delta = Input.GetMouseButton(0) ? 1 : -1;
                dirty = _sample.Terrain.BrushMapHigh(brush, delta);
            }
            if (dirty)
            {
                _sample.RefreshMeshes();
                _onTerrainChanged?.Invoke();
            }
        }
    }
}
