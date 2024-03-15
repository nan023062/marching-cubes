//****************************************************************************
// File: QuadElement.cs
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
    ///  A 2D quad for user input.
    /// </summary>
    public sealed class InputQuad : InputElement
    {
        private CubedWorld _cubedWorld;
        
        [Header("Cube坐标")] private int3 _xyz;
        [Header("Cube-Side")] private Side _side;
        
        public override CubedWorld world => _cubedWorld;
        
        public void Init(CubedWorld world, in int3 xyz, Side side)
        {
            _cubedWorld = world;
            _xyz = xyz;
            _side = side;
            Vector3 localPosition = xyz.GetCubeSideLocalPosition(side);
            Quaternion localRotation = side.GetSideQuaternion();
            Transform t = transform;
            t.SetParent(world.transform, false);
            t.SetLocalPositionAndRotation(localPosition, localRotation);
            t.localScale = new Vector3(BuildMath.Unit, BuildMath.Unit, BuildMath.AlmostZero);
        }
        
        protected override void OnSelected(PointerEventData eventData)
        {
        }

        protected override void OnDeselected(PointerEventData eventData)
        {
        }

        protected override void OnClicked(PointerEventData eventData)
        {
        }
    }
}