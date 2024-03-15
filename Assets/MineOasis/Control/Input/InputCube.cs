//****************************************************************************
// File: CubeElement.cs
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
    ///  A 3C cube for user input.
    /// </summary>
    public sealed class InputCube : InputElement
    {
        private CubedWorld _cubedWorld;
        
        [Header("Cube坐标")] private int3 _xyz;

        public override CubedWorld world => _cubedWorld;
        
        public void Init(CubedWorld world, in int3 xyz)
        {
            _cubedWorld = world;
            _xyz = xyz;
            Vector3 localPosition = xyz.GetCubeLocalPosition();
            Transform t = transform;
            t.SetParent(world.transform, false);
            t.SetLocalPositionAndRotation(localPosition, Quaternion.identity);
            t.localScale = new Vector3(BuildMath.Unit, BuildMath.Unit, BuildMath.Unit);
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