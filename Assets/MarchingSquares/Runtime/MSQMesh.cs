//****************************************************************************
// File: MSQMesh.cs
// Author: Li Nan
// Date: 2023-09-01 12:00
// Version: 1.0
//****************************************************************************

using UnityEngine;

namespace MarchingSquares
{
    [CreateAssetMenu(fileName = "NewMSQMesh", menuName = "MarchingSquares/MSQMesh")]
    public class MSQMesh : ScriptableObject
    {
        public Mesh mesh;
        
    }
} 