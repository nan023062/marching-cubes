using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace MineOasis
{
    public sealed class CubedWorld : MonoBehaviour
    {
        [SerializeField, Header("Input Quad")] private InputQuad _inputQuad;
        [SerializeField, Header("Input Cube")] private InputCube _inputCube;
        [SerializeField, Header("Cubed Zone")] private BuildZone _cubedZone;
        
        private GameObjectPool<InputQuad> _quadPool;
        private GameObjectPool<InputCube> _cubePool;
        private GameObjectPool<BuildZone> _zonePool;

        private readonly List<BuildZone> _zones = new List<BuildZone>();
        private BuildZone _selectedZone;
        private int3 _cells;

        private readonly Dictionary<int, InputQuad> _quads = new Dictionary<int, InputQuad>();
        private readonly Dictionary<int, InputCube> _cubes = new Dictionary<int, InputCube>();

        public Matrix4x4 localToWorldMatrix { private set; get; }
        public Matrix4x4 worldToLocalMatrix { private set; get; }

        public void BuildStart(Vector3 position, Quaternion quaternion)
        {
            _cells = new int3(32, 32, 32);
            _quadPool = new GameObjectPool<InputQuad>(_inputQuad, 4);
            _cubePool = new GameObjectPool<InputCube>(_inputCube, 4);
            _zonePool = new GameObjectPool<BuildZone>(_cubedZone, 4);
            
            Transform t = transform;
            localToWorldMatrix = Matrix4x4.TRS(position, quaternion, Vector3.one);
            worldToLocalMatrix = localToWorldMatrix.inverse;
            t.SetPositionAndRotation(position, quaternion);
            t.localScale = Vector3.one;
        }
        
        public void Input_Place(in Block block)
        {

        }
        
        public void Input_Cancel()
        {

        }

        public void BuildFinish()
        {
            _quadPool.Dispose();
            _quadPool = null;

            _cubePool.Dispose();
            _cubePool = null;

            _zonePool.Dispose();
            _zonePool = null;
        }
    }
}