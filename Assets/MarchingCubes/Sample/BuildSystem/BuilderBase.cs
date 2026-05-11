using UnityEngine;

namespace MarchingCubes.Sample
{
    public abstract class BuilderBase
    {
        public Matrix4x4 localToWorld { get; protected set; }
    }
}
