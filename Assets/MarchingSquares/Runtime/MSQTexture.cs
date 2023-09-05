//****************************************************************************
// File: MQTexture.cs
// Author: Li Nan
// Date: 2023-09-01 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingSquares
{
    [CreateAssetMenu(fileName = "NewMSQTexture", menuName = "MarchingSquares/MSQTexture")]
    public class MSQTexture : ScriptableObject
    {
        public static readonly Vector2 offset = new (0.25f,0.25f);
        
        public Texture2D texture;
        public Coord[] coord;
    }
    
    [Serializable]
    public struct Coord
    {
        public int x, y;
    }
}