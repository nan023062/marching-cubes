namespace MarchingCubes.Sample
{
    public interface IBuildState
    {
        void OnEnter();
        void OnExit();
        void OnUpdate();
        void DrawGUI();
    }
}
