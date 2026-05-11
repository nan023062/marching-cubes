using UnityEngine;

namespace MarchingSquares
{
    public class Brush : MonoBehaviour
    {
        [Range(1, 5), SerializeField, Header("刷子尺寸")]
        private int size = 1;

        [SerializeField, Header("开启纹理刷")]
        public bool colorBrush;

        private MeshFilter _filter;
        private Mesh       _mesh;
        private int        _cachedSize;

        public int Size => size;

        void Awake()
        {
            _filter = GetComponent<MeshFilter>();
            _mesh   = new Mesh { name = "Brush" };
            _filter.sharedMesh = _mesh;
            RebuildMesh();
        }

        void OnValidate() => RebuildMesh();

        void RebuildMesh()
        {
            if (_filter == null || size == _cachedSize) return;
            _cachedSize = size;

            const int segments = 64;
            float     radius   = size * 0.5f;
            const float yOff   = 0.03f;

            var verts = new Vector3[segments + 1];
            var tris  = new int[segments * 3];

            verts[segments] = new Vector3(0, yOff, 0); // 圆心

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                verts[i] = new Vector3(Mathf.Cos(angle) * radius, yOff, Mathf.Sin(angle) * radius);

                int ti = i * 3;
                tris[ti]     = segments;
                tris[ti + 1] = i;
                tris[ti + 2] = (i + 1) % segments;
            }

            _mesh.Clear();
            _mesh.vertices  = verts;
            _mesh.triangles = tris;
            _mesh.RecalculateNormals();
            _mesh.RecalculateBounds();
            _filter.sharedMesh = _mesh;
        }
    }
}
