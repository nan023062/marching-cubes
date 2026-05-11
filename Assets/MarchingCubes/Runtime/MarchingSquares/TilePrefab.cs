#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ tile prefab 调试组件，仅持有 caseIndex / baseHeight 数据供 Inspector 查看。
    /// caseIndex 为 base-3 编码 case_idx ∈ [0, 80]，TileTable.GetMeshCase 产出。
    /// 网格可视化（点阵 grid）由 TerrainBuilder.DrawGizmos 在 Terrain 层统一绘制。
    /// </summary>
    [ExecuteAlways]
    public class TilePrefab : MonoBehaviour
    {
        [Header("Tile")]
        public int caseIndex;   // 0~80（base-3 编码 r0+r1*3+r2*9+r3*27，65 有效 + 16 死槽）
        public int baseHeight;  // runtime 由 TerrainBuilder 设置

        [Header("Gizmos")]
        public bool showGizmos = false;

        private GUIStyle _labelStyle;

        private void OnEnable()
        {
#if UNITY_EDITOR
            _labelStyle = new GUIStyle();
            _labelStyle.normal.textColor = Color.blue;
            _labelStyle.fontSize         = 20;
            _labelStyle.fontStyle        = FontStyle.Bold;
            _labelStyle.alignment        = TextAnchor.MiddleCenter;
#endif
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if (_labelStyle == null) return;

            Color     prevColor  = Gizmos.color;
            Matrix4x4 prevMatrix = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;

            // unit quad 边框（黄色）
            Gizmos.color = Color.yellow;
            var corners = new Vector3[]
            {
                new Vector3(0, 0, 0),
                new Vector3(1, 0, 0),
                new Vector3(1, 0, 1),
                new Vector3(0, 0, 1),
            };
            for (int i = 0; i < 4; i++)
                Gizmos.DrawLine(corners[i], corners[(i + 1) % 4]);

            // 四角小球（红色）
            Gizmos.color = Color.red;
            foreach (var c in corners)
                Gizmos.DrawSphere(c, 0.04f);

            Gizmos.matrix = prevMatrix;
            Gizmos.color  = prevColor;

#if UNITY_EDITOR
            Matrix4x4 m      = transform.localToWorldMatrix;
            Vector3   center = m.MultiplyPoint(new Vector3(0.5f, 0f, 0.5f));
            string    bit3   = $"r = {caseIndex % 3},{(caseIndex / 3) % 3},{(caseIndex / 9) % 3},{caseIndex / 27}";
            Handles.Label(center, $"case: {caseIndex}\nbaseH: {baseHeight}\n{bit3}", _labelStyle);

            for (int i = 0; i < TileTable.CornerCount; i++)
            {
                var (cx, cz) = TileTable.Corners[i];
                Vector3 wp   = m.MultiplyPoint(new Vector3(cx, 0f, cz));
                Handles.Label(wp, $"V{i}", _labelStyle);
            }
#endif
        }
    }
}
