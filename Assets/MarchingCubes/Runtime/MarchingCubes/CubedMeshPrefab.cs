//****************************************************************************
// File: CubedMesh.cs
// Author: Li Nan
// Date: 2024-03-10 12:00
// Version: 1.0
//****************************************************************************

#if UNITY_EDITOR
using UnityEditor;
#endif

using UnityEngine;

namespace MarchingCubes
{
    [ExecuteAlways]
    [ExecuteInEditMode]
    public class CubedMeshPrefab : MonoBehaviour
    {
        public static readonly float Unit = 0.5f;
        
        [Header( "顶点组合" )]
        public CubeVertexMask mask;

        [Header("Gizmos")]
        public bool showGizmos = false;
        
        private GUIStyle _vertexStyle;
        private GUIStyle _titleStyle;
        
        private void OnEnable()
        {
#if UNITY_EDITOR
            _titleStyle = new GUIStyle();
            _titleStyle.normal.textColor = Color.red;
            _titleStyle.fontSize = 28;
            _titleStyle.fontStyle = FontStyle.Bold;
            _titleStyle.alignment = TextAnchor.MiddleCenter;
            
            _vertexStyle = new GUIStyle();
            _vertexStyle.normal.textColor = Color.blue;
            _vertexStyle.fontSize = 20;
            _vertexStyle.fontStyle = FontStyle.Bold;
            _vertexStyle.alignment = TextAnchor.MiddleCenter;
#endif 
        }

        private void OnDrawGizmos()
        {
            if (!showGizmos) return;
            if(_vertexStyle == null) return;
            
            Color color = Gizmos.color;
            Transform t = transform;
            Matrix4x4 matrix = t.localToWorldMatrix;
            Gizmos.matrix = matrix;
            
            // draw cube wire
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(new Vector3(0.5f,0.5f,0.5f), Vector3.one);
            
            // draw vertex sphere
            Gizmos.color = Color.red;
            for (int v = 0; v < CubeTable.VertexCount; v++)
            {
                if (mask.HasFlag((CubeVertexMask)(1 << v)))
                {
                    ref readonly var p = ref CubeTable.Vertices[v];
                    Vector3 pos = new Vector3(p.x, p.y, p.z);
                    Gizmos.DrawSphere(pos, 0.03f);
                }
            }
            
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = color;
            
#if UNITY_EDITOR
            // 显示索引 和 edge mask 信息
            Vector3 center = matrix.MultiplyPoint(Vector3.one * 0.5f);
            string maskBit = System.Convert.ToString((int)mask, 2);
            Handles.Label(center, $"cube: {(int)mask}\n{maskBit}", _vertexStyle);
            
            // 下面使用文本显示顶点序号
            for (int v = 0; v < CubeTable.VertexCount; v++)
            {
                ref readonly var p = ref CubeTable.Vertices[v];
                Vector3 pos = new Vector3(p.x, p.y, p.z);
                Vector3 worldPos = matrix.MultiplyPoint(pos);
                Handles.Label(worldPos, v.ToString(), _vertexStyle);
            }
#endif
        }
        
        //0个顶点 C0 = 1
        //1个顶点 C8 = 8
        //2个顶点 C8^2 = (8 * 7) / (2 * 1) = 28
        //3个顶点 C8^3 = (8 * 7 * 6) / (3 * 2 * 1) = 56
        //4个顶点 C8^4 = (8 * 7 * 6 * 5) / (4 * 3 * 2 * 1) = 70
        //5个顶点 C8^5 = (8 * 7 * 6 * 5 * 4) / (5 * 4 * 3 * 2 * 1) = 56
        //6个顶点 C8^6 = (8 * 7 * 6 * 5 * 4 * 3) / (6 * 5 * 4 * 3 * 2 * 1) = 28
        //7个顶点 C8^7 = (8 * 7 * 6 * 5 * 4 * 3 * 2) / (7 * 6 * 5 * 4 * 3 * 2 * 1) = 8
        //8个顶点 C8^8 = 1
    }
}