#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MarchingSquares
{
    /// <summary>
    /// MQ tile prefab 调试组件，兼容地形坡面（Terrain）和悬崖墙面（Cliff）两种类型。
    ///
    /// Terrain：caseIndex = 角点高差 bit mask（0-18），可视化四角高低状态。
    /// Cliff  ：caseIndex = 边墙 bit mask（0-15），可视化哪些边有垂直墙面。
    /// </summary>
    [ExecuteAlways]
    [ExecuteInEditMode]
    public class TilePrefab : MonoBehaviour
    {
        [Header("Tile")]
        public TileType tileType  = TileType.Terrain;
        public int      caseIndex;   // Terrain: 0-18  /  Cliff: 0-15
        public int      baseHeight;  // runtime 由 MqTerrainBuilder 设置

#if UNITY_EDITOR
        private GUIStyle _labelStyle;
        private GUIStyle _cornerStyle;

        private void OnEnable()
        {
            _labelStyle = new GUIStyle
            {
                normal    = { textColor = tileType == TileType.Cliff ? Color.cyan : Color.yellow },
                fontSize  = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            _cornerStyle = new GUIStyle
            {
                normal    = { textColor = tileType == TileType.Cliff ? Color.cyan : Color.cyan },
                fontSize  = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
        }
#endif

        // ── 悬崖：各边底部顶点对（格子 XZ 中心为原点，Y=0）─────────────────
        private static readonly (Vector3 a, Vector3 b)[] CliffEdgeBottom =
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

            if (tileType == TileType.Terrain)
                DrawTerrainGizmos();
            else
                DrawCliffGizmos();

            Gizmos.matrix = prev;
            Gizmos.color  = Color.white;

#if UNITY_EDITOR
            string bits = System.Convert.ToString(caseIndex, 2).PadLeft(4, '0');
            string prefix = tileType == TileType.Cliff ? "cliff" : "ci";
            Vector3 labelPos = tileType == TileType.Cliff
                ? matrix.MultiplyPoint(new Vector3(0, 1.15f, 0))
                : matrix.MultiplyPoint(new Vector3(0.5f, 0.6f, 0.5f));
            Handles.Label(labelPos, $"{prefix}={caseIndex}\n{bits}", _labelStyle);
#endif
        }

        // ── 地形坡面 Gizmos（角点高低 + 边框）───────────────────────────────

        private void DrawTerrainGizmos()
        {
            var corners = new Vector3[4];
            for (int i = 0; i < 4; i++)
            {
                var (x, z) = TileTable.Corners[i];
                float y    = ((caseIndex & (1 << i)) != 0) ? 1f : 0f;
                corners[i] = new Vector3(x, y, z);
            }

            Gizmos.color = Color.yellow;
            int[] edgeA = { 0, 1, 2, 3 };
            int[] edgeB = { 1, 2, 3, 0 };
            for (int e = 0; e < 4; e++)
                Gizmos.DrawLine(corners[edgeA[e]], corners[edgeB[e]]);

            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(0.5f, 0f, 0.5f), new Vector3(1f, 0f, 1f));

            for (int i = 0; i < 4; i++)
            {
                bool high  = (caseIndex & (1 << i)) != 0;
                Gizmos.color = high ? new Color(1f, 0.4f, 0.1f) : new Color(0.4f, 0.4f, 0.4f);
                Gizmos.DrawSphere(corners[i], 0.06f);
            }

#if UNITY_EDITOR
            var matrix   = transform.localToWorldMatrix;
            string[] names = { "V0\nBL", "V1\nBR", "V2\nTR", "V3\nTL" };
            for (int i = 0; i < 4; i++)
            {
                bool    high = (caseIndex & (1 << i)) != 0;
                Vector3 wp   = matrix.MultiplyPoint(corners[i] + new Vector3(0, 0.12f, 0));
                Handles.Label(wp, $"{names[i]}\n{(high ? 'H' : 'L')}", _cornerStyle);
            }
#endif
        }

        // ── 悬崖墙面 Gizmos（各激活边的竖向矩形轮廓）────────────────────────

        private void DrawCliffGizmos()
        {
            for (int i = 0; i < 4; i++)
            {
                if ((caseIndex & (1 << i)) == 0) continue;

                Gizmos.color = Color.cyan;
                var (a, b) = CliffEdgeBottom[i];
                var at = a + Vector3.up;
                var bt = b + Vector3.up;

                Gizmos.DrawLine(a, b);
                Gizmos.DrawLine(at, bt);
                Gizmos.DrawLine(a, at);
                Gizmos.DrawLine(b, bt);
            }

            Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.4f);
            Gizmos.DrawWireCube(new Vector3(0, 0.5f, 0), new Vector3(1, 1, 1));
        }
    }
}
