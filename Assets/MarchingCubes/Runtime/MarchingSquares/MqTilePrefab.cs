#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 地形 tile prefab 调试组件（类比 CubedMeshPrefab）。
    /// 挂在 MqMeshConfig 生成的 case prefab 根节点上，在 Editor 中可视化四角状态。
    ///
    /// 角点布局（unit quad，Y=高度）：
    ///   V3(TL) ─── V2(TR)
    ///     │               │
    ///   V0(BL) ─── V1(BR)
    /// </summary>
    [ExecuteAlways]
    [ExecuteInEditMode]
    public class MqTilePrefab : MonoBehaviour
    {
        [Header("Quad Case")]
        public int caseIndex;   // 0–15，bit_i=1 表示 Vi 高于 base
        public int baseHeight;  // 格点 base 高度（runtime 由 MqTerrainBuilder 设置）

#if UNITY_EDITOR
        private GUIStyle _labelStyle;
        private GUIStyle _cornerStyle;

        private void OnEnable()
        {
            _labelStyle = new GUIStyle
            {
                normal    = { textColor = Color.yellow },
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _cornerStyle = new GUIStyle
            {
                normal    = { textColor = Color.cyan },
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }
#endif

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (_labelStyle == null) return;
#endif
            var matrix = transform.localToWorldMatrix;
            var prev   = Gizmos.matrix;
            Gizmos.matrix = matrix;

            // 四角在 local space 的位置（Y 根据 bit 决定）
            var corners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var (x, z) = MqTable.Corners[i];
                float y    = ((caseIndex & (1 << i)) != 0) ? 1f : 0f;
                corners[i] = new Vector3(x, y, z);
            }

            // 线框：四条边 + 高差连线
            Gizmos.color = Color.yellow;
            int[] edgeA = { 0, 1, 2, 3 };
            int[] edgeB = { 1, 2, 3, 0 };
            for (int e = 0; e < 4; e++)
                Gizmos.DrawLine(corners[edgeA[e]], corners[edgeB[e]]);

            // 底面参考框（base 高度 Y=0）
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0f, 1f));

            // 角点球：橙=高，灰=低
            for (int i = 0; i < 4; i++)
            {
                bool high  = (caseIndex & (1 << i)) != 0;
                Gizmos.color = high ? new Color(1f, 0.4f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
                Gizmos.DrawSphere(corners[i], 0.06f);
            }

            Gizmos.matrix = prev;
            Gizmos.color  = Color.white;

#if UNITY_EDITOR
            // Case index + bit pattern 标签（quad 中心上方）
            Vector3 worldCenter = matrix.MultiplyPoint(new Vector3(0.5f, 0.6f, 0.5f));
            string  bits        = System.Convert.ToString(caseIndex, 2).PadLeft(4, '0');
            Handles.Label(worldCenter, $"ci={caseIndex}\n{bits}", _labelStyle);

            // V0~V3 角点标签
            string[] names = { "V0\nBL", "V1\nBR", "V2\nTR", "V3\nTL" };
            for (int i = 0; i < 4; i++)
            {
                bool    high  = (caseIndex & (1 << i)) != 0;
                Vector3 wp    = matrix.MultiplyPoint(corners[i] + new Vector3(0, 0.12f, 0));
                Handles.Label(wp, $"{names[i]}\n{(high ? 'H' : 'L')}", _cornerStyle);
            }
#endif
        }
    }
}
