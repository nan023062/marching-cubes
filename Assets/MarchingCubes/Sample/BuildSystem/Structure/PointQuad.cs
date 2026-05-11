//****************************************************************************
// File: CubeBoard.cs
// Author: Li Nan
// Date: 2023-09-03 12:00
// Version: 1.0
//****************************************************************************

using System;
using UnityEngine;

namespace MarchingCubes.Sample
{
    public class PointQuad : PointElement
    {
        public int cx, cz;   // cell 索引（不是格点索引）
    }
}