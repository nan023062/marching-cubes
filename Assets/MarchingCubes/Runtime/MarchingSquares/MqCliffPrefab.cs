#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ 悬崖 tile prefab 调试组件（类比 MqTilePrefab）。
    /// 挂在 MqMeshConfig 生成的 cliff prefab 根节点上，
    /// 在 Editor 中可视化哪些边有悬崖墙面。
    ///
    /// 悬崖 Mesh 以格子 XZ 中心为原点，Y∈[0,1]。
    /// 边约定：E0=南(-Z) E1=东(+X) E2=北(+Z) E3=西(-X)
    /// </summary>
    [ExecuteAlways]
    [ExecuteInEditMode]
    public class MqCliffPrefab : MonoBehaviour
    {
        [Header("Cliff Case")]
        public int cliffCase;   // 0–15，bit_i=1 表示 Ei 边有墙面
        public int baseHeight;  // 格点 base-1 高度（runtime 由 MqTerrainBuilder 设置）

#if UNITY_EDITOR
        private GUIStyle _labelStyle;

        private void OnEnable()
        {
            _labelStyle = new GUIStyle
            {
                normal    = { textColor = Color.cyan },
                fontSize  = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }
#endif

        // 各边在格子中心坐标系中的两端点（Y=0 底边，Y=1 顶边）
        private static readonly (Vector3 a, Vector3 b)[] EdgeBottom =
        {
            (new Vector3(-0.5f, 0, -0.5f), new Vector3( 0.5f, 0, -0.5f)),  // E0 南
            (new Vector3( 0.5f, 0, -0.5f), new Vector3( 0.5f, 0,  0.5f)),  // E1 东
            (new Vector3( 0.5f, 0,  0.5f), new Vector3(-0.5f, 0,  0.5f)),  // E2 北
            (new Vector3(-0.5f, 0,  0.5f), new Vector3(-0.5f, 0, -0.5f)),  // E3 西
        };

        private void OnDrawGizmos()
        {
#if UNITY_EDITOR
            if (_labelStyle == null) return;
#endif
            var matrix = transform.localToWorldMatrix;
            var prev   = Gizmos.matrix;
            Gizmos.matrix = matrix;

            // 各激活边：绘制底边线 + 顶边线 + 两条竖线
            for (int i = 0; i < 4; i++)
            {
                if ((cliffCase & (1 << i)) == 0) continue;

                Gizmos.color = Color.cyan;
                var (a, b) = EdgeBottom[i];
                var at = a + Vector3.up; var bt = b + Vector3.up;

                Gizmos.DrawLine(a, b);   // 底边
                Gizmos.DrawLine(at, bt); // 顶边
                Gizmos.DrawLine(a, at);  // 左竖线
                Gizmos.DrawLine(b, bt);  // 右竖线
            }

            // 单位格子底面轮廓（浅色参考）
            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1));

            Gizmos.matrix = prev;
            Gizmos.color  = Color.white;

#if UNITY_EDITOR
            Vector3 worldCenter = matrix.MultiplyPoint(new Vector3(0, 1.1f, 0));
            string  bits        = System.Convert.ToString(cliffCase, 2).PadLeft(4, '0');
            Handles.Label(worldCenter, $"cliff={cliffCase}\n{bits}", _labelStyle);
#endif
        }
    }
}
