//****************************************************************************
// File: Sample.cs
// Author: Li Nan
// Date: 2023-08-30 12:00
// Version: 1.0
//****************************************************************************
using UnityEngine;

namespace MarchingSquares  
{
    [RequireComponent(typeof(MeshCollider))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class MarchingQuad25Sample : MonoBehaviour, ITextureLoader
    {
        [SerializeField]
        private int width = 50;
        [SerializeField]
        private int height = 10;
        [SerializeField]
        private int pow = 8;

        [SerializeField,Header("纹理")]
        private int textureLayer = 0;
        
        private MSQTerrain _terrain;
        private MeshCollider _meshCollider;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        [SerializeField] private Brush bush;
        [SerializeField] private MSQTexture[] _textureLayers;
        
        public void Awake()
        {
            _meshCollider = GetComponent<MeshCollider>();
            _meshRenderer = GetComponent<MeshRenderer>();
            _meshFilter = GetComponent<MeshFilter>();
            Transform t = transform;
            _terrain = new MSQTerrain(width, width, height, 1f / pow, t.position, this);
            t.localScale = _terrain.localToWorld.lossyScale;
            _meshFilter.sharedMesh = _terrain.mesh;
            _meshCollider.sharedMesh = _terrain.mesh;
        }

        private void Update()
        {
            Vector3 pos = Input.mousePosition;
            Transform t = bush.transform;
            Ray ray = Camera.main.ScreenPointToRay(pos);
            int layerMask = 1 << LayerMask.NameToLayer("MarchingQuads");
            if (Physics.Raycast(ray, out var hit, 1000, layerMask))
            {
                var h = hit.collider;
                t.position = hit.point;
                t.localScale = _terrain.localToWorld.lossyScale;
                t.rotation = _terrain.localToWorld.rotation;
            }
            else
            {
                Vector3 position = t.position;
                float northDis = Vector3.Project(position - ray.origin, Vector3.up).magnitude;
                float cos = Vector3.Dot(Vector3.down, ray.direction);
                float distance = northDis / cos;
                t.position = ray.origin + ray.direction * distance;
            }

            if (Input.GetMouseButtonUp(1))
            {
                if (bush.colorBrush)
                {
                    if (_terrain.PaintTexture(bush, textureLayer, false))
                    {
                        _meshFilter.sharedMesh = _terrain.mesh;
                        _meshCollider.sharedMesh = _terrain.mesh;
                    }
                }
                else
                {
                    if(_terrain.BrushMapHigh(bush, -1))
                    {
                        _meshFilter.sharedMesh = _terrain.mesh;
                        _meshCollider.sharedMesh = _terrain.mesh;
                    }
                }
            }
            else if (Input.GetMouseButtonUp(0))
            {
                if (bush.colorBrush)
                {
                    if (_terrain.PaintTexture(bush, textureLayer, true))
                    {
                        _meshFilter.sharedMesh = _terrain.mesh;
                        _meshCollider.sharedMesh = _terrain.mesh;
                    }
                }
                else
                {
                    if(_terrain.BrushMapHigh(bush, 1))
                    {
                        _meshFilter.sharedMesh = _terrain.mesh;
                        _meshCollider.sharedMesh = _terrain.mesh;
                    }
                }
            }
        }

        public void OnDrawGizmos()
        {
            if (null != _terrain)
            {
                Color color = Gizmos.color;
            
                Gizmos.color = Color.black;
                Gizmos.matrix = _terrain.localToWorld;
                
                _terrain.OnDrawGizmos();
                
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = color;
            }
        }

        public void OnDestroy()
        {
            Destroy(_meshCollider.gameObject);
            _meshCollider = null;
            _terrain = null;
        }
        
        public MSQTexture GetTexture(int layer)
        {
            return _textureLayers[layer];
        }
    }
}