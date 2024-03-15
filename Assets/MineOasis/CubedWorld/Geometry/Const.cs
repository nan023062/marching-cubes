//****************************************************************************
// File: Const.cs
// Author: Li Nan
// Date: 2024-03-08 12:00
// Version: 1.0
//****************************************************************************

using Unity.Mathematics;
using UnityEngine;

namespace MineOasis
{
    [System.Flags]
    public enum Side : byte
    {
        // six single
        Front = 0x01,
        Back = Front << 1,
        Left = Back << 1,
        Right = Left << 1,
        Bottom = Right << 1,
        Up = Bottom << 1,

        // three axis side
        FrontBack = Front | Back,
        LeftRight = Left | Right,
        BottomUp = Bottom | Up,

        All = FrontBack | LeftRight | BottomUp,
        Count = 6
    }

    [System.Flags]
    public enum Axis : byte
    {
        X = 0x01,
        Y = 0X02,
        Z = 0x04,

        XY = X | Y,
        XZ = X | Z,
        YZ = Y | Z,
        XYZ = X | Y | Z,
    }
    
    public static class BuildMath
    {
        public static readonly float Unit = 0.5F;
        public static readonly float HalfUnit = 0.25F;
        public static readonly float AlmostZero = 0.1F;

        public static float3 GetSidePos(this float3 cube, Side side)
        {
            float3 pos = cube;

            switch (side)
            {
                case Side.Front:
                    pos.z += HalfUnit;
                    break;
                
                case Side.Back:
                    pos.z -= HalfUnit;
                    break;
                
                case Side.Left:
                    pos.x -= HalfUnit;
                    break;
                
                case Side.Right:
                    pos.x += HalfUnit;
                    break;
                
                case Side.Bottom:
                    pos.y -= HalfUnit;
                    break;
                
                case Side.Up:
                    pos.y += HalfUnit;
                    break;
            }
            
            return pos;
        }
        
        public static quaternion GetSideQuaternion(this Side side)
        {
            switch (side)
            {
                case Side.Back:
                    return Quaternion.LookRotation(Vector3.back, Vector3.up);
                   
                case Side.Left:
                    return Quaternion.LookRotation(Vector3.left, Vector3.up);
                
                case Side.Right:
                    return Quaternion.LookRotation(Vector3.right, Vector3.up);
                    
                case Side.Bottom:
                    return Quaternion.LookRotation(Vector3.down, Vector3.forward);
                   
                case Side.Up:
                    return Quaternion.LookRotation(Vector3.up, Vector3.forward);
                
                default:
                    return Quaternion.identity;
            }
        }
        
        public static float3 GetCubeLocalPosition(this int3 xyz)
        {
            return new float3(xyz.x * Unit, xyz.y * Unit, xyz.z * Unit);
        }
        
        public static float3 GetCubeSideLocalPosition(this int3 xyz, Side side)
        {
            float3 pos = xyz.GetCubeLocalPosition();
            return pos.GetSidePos(side);
        }
    }
}