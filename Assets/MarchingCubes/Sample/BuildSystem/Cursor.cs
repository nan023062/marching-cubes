using UnityEngine;

namespace MarchingCubes.Sample
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class Cursor : MonoBehaviour
    {
        private CursorStyle  _style = CursorStyle.Cube;

        [Range(1, 5), SerializeField] protected int _size = 1;
        public int Size => _size;

        private MeshFilter   _filter;
        private MeshRenderer _renderer;
        
        public CursorStyle Style
        {
            get => _style;
            set
            {
                _style = value;
                Vector3 scale = Vector3.one;
                
                switch (_style)
                {
                    case CursorStyle.QuadX:
                    {
                        scale.x = 0;
                        break;
                    }
                    case CursorStyle.QuadY:
                    {
                        scale.y = 0;
                        break;
                    }
                    case CursorStyle.QuadZ:
                    {
                        scale.z = 0;
                        break;
                    }
                }
                
                transform.localScale = scale * Size;
                transform.localRotation = Quaternion.identity;
            }
        }


        private void Awake()
        {
            _filter   = GetComponent<MeshFilter>();
            _renderer = GetComponent<MeshRenderer>();
            Style = CursorStyle.Cube;
        }

        public void SetMaterial(Material m) => _renderer.sharedMaterial = m;
    }

    /// <summary>
    /// 4种光标样式：立方体、3个轴向的面（X/Y/Z）。不同模式下使用不同样式以增强交互反馈。  
    /// </summary>
    public enum CursorStyle
    {
        Cube,
        QuadX,
        QuadY,
        QuadZ,
    }
}
