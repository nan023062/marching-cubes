//****************************************************************************
// File: TestSystemGroup.cs
// Author: Li Nan
// Date: 2023-12-12 12:00
// Version: 1.0
//****************************************************************************

using Unity.Entities;
using Unity.Scenes;

namespace MineOasis
{
    public partial class MineOasisSystemGroup : ComponentSystemGroup
    {
        
    }
    
    [UpdateInGroup( typeof( MineOasisSystemGroup ) )]
    public partial class CubeRotateSystemGroup : ComponentSystemGroup
    {
        
    }
}