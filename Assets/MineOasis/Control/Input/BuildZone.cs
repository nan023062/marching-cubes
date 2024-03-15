//****************************************************************************
// File: BuildingZone.cs
// Author: Li Nan
// Date: 2024-03-08 12:00
// Version: 1.0
//****************************************************************************

using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MineOasis
{
    /// <summary>
    /// 摆放区域
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BuildZone : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CubedWorld _world;
        private Zone _zone;
        private Matrix4x4 _localToWorldMatrix;
        private Matrix4x4 _worldToLocalMatrix;
        
        public CubedWorld world => _world;

        public void Init(CubedWorld world, Zone zone)
        {
            _world = world;
            _zone = zone;
            Matrix4x4 localMatrix = Matrix4x4.TRS(_zone.position, _zone.rotation, Vector3.one);
            _localToWorldMatrix = world.localToWorldMatrix * localMatrix;
            _worldToLocalMatrix = _localToWorldMatrix.inverse;

            Transform t = transform;
            t.SetParent(world.transform, false);
            t.SetLocalPositionAndRotation(_zone.position, _zone.rotation);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
        }

        public void OnPointerExit(PointerEventData eventData)
        {
        }
    
        public float3 GetCubePosition(in int3 xyz)
        {
            Vector3 localPosition = xyz.GetCubeLocalPosition();
            return _localToWorldMatrix.MultiplyPoint(localPosition);
        }

        public float3 GetCubeSidePosition(in int3 xyz, Side side)
        {
            Vector3 localPosition = xyz.GetCubeSideLocalPosition(side);
            return _localToWorldMatrix.MultiplyPoint(localPosition);
        }

        public int3 WorldPositionToXyz(in float3 position)
        {
            float3 localPosition = _worldToLocalMatrix.MultiplyPoint(position);
            int x = (int)math.round(localPosition.x / BuildMath.Unit);
            int y = (int)math.round(localPosition.y / BuildMath.Unit);
            int z = (int)math.round(localPosition.z / BuildMath.Unit);
            return new int3(x, y, z);
        }

        public int3 LocalPositionToXyz(in Vector3 position)
        {
            int x = (int)math.round(position.x / BuildMath.Unit);
            int y = (int)math.round(position.y / BuildMath.Unit);
            int z = (int)math.round(position.z / BuildMath.Unit);
            return new int3(x, y, z);
        }
    }
}