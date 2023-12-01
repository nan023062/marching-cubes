using UnityEngine;

namespace MarchingCubes
{
    public class PolygonTableDrawer : MonoBehaviour
    {
        public Mode mode = Mode.DrawOneCube;
        public bool V0,V1,V2,V3,V4,V5,V6,V7;
        private readonly OneCube[] _cubes = new OneCube[CubeTable.CubeKind];
        
        public enum Mode
        {
            DrawOneCube, Draw256Cube
        }
        
        private void Awake()
        {
            for (int i = 0; i < CubeTable.CubeKind; i++)
            {
                Vector3 position = new Vector3(i * 1.2f, 0f, 0f);
                _cubes[i] = new OneCube(i, position);
            }
        }

        public void OnDrawGizmos()
        {
            switch (mode)
            {
                case Mode.DrawOneCube:
                {
                    int cubeIndex = V0 ? 1 : 0;
                    if (V1) cubeIndex |= 1 << 1;
                    if (V2) cubeIndex |= 1 << 2;
                    if (V3) cubeIndex |= 1 << 3;
                    if (V4) cubeIndex |= 1 << 4;
                    if (V5) cubeIndex |= 1 << 5;
                    if (V6) cubeIndex |= 1 << 6;
                    if (V7) cubeIndex |= 1 << 7;
                    _cubes[cubeIndex]?.Draw();
                    break;
                }
                case Mode.Draw256Cube:
                {
                    for (int i = 0; i < CubeTable.CubeKind; i++)
                    {
                        _cubes[i]?.Draw();
                    }
                    break;
                }
            }
        }
        

        class OneCube : IMarchingCubeReceiver
        {
            public readonly int index;
            private readonly CubeMesh mesh;
            private readonly Matrix4x4 _localToWorld;
            
            float IMarchingCubeReceiver.GetIsoLevel() => 0.5f;
            
            public bool IsoPass(float iso) => iso > 0.5f;
            
            void IMarchingCubeReceiver.OnRebuildCompleted()
            {
            }
            
            public OneCube(int index, Vector3 position)
            {
                this.index = index;
                _localToWorld = Matrix4x4.TRS(position, Quaternion.identity, Vector3.one);
                mesh = new CubeMesh(1, 1, 1, this);
                for (int v = 0; v < CubeTable.VertexCount; v++)
                {
                    if ((index & (1 << v)) > 0)
                    {
                        ref readonly var p = ref CubeTable.Vertices[v];
                        mesh.SetPointISO(p.x, p.y, p.z, 1);
                    }
                }
                mesh.Rebuild();
            }
            
            public void Draw()
            {
                Gizmos.matrix = _localToWorld;
                if (index > 0)
                {
                    Gizmos.color = Color.green;
                    Gizmos.DrawMesh(mesh.mesh);
                    for (int i = 0; i < CubeTable.VertexCount; i++)
                    {
                        if ((index & 1 << i) > 0)
                        {
                            Gizmos.color = Color.blue;
                            ref var p = ref CubeTable.Vertices[i];
                            Gizmos.DrawSphere(new Vector3(p.x,p.y,p.z), 0.03f);
                        }
                    }
                }
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(Vector3.one * 0.5f, Vector3.one);
                Gizmos.matrix = Matrix4x4.identity;
            }
        }
    }
}