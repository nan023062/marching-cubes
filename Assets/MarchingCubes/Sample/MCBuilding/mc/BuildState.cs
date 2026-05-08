using UnityEngine;

namespace MarchingCubes.Sample
{
    public class BuildState : IBuildState
    {
        readonly MCBuilding _building;

        public BuildState(MCBuilding building) => _building = building;

        public void OnEnter() => _building.EnableInteraction(true);
        public void OnExit()  => _building.EnableInteraction(false);

        public void OnGUI()
        {
            int count = _building.ConfigCount;
            if (count <= 1) return;

            const float btnW = 140f, btnH = 28f, pad = 8f;
            float totalW = count * (btnW + pad) + pad;
            GUI.Box(new Rect(pad, pad, totalW, btnH + pad * 2), GUIContent.none);

            for (int i = 0; i < count; i++)
            {
                string label = _building.GetConfigName(i);
                Rect r = new Rect(pad + i * (btnW + pad), pad + pad * 0.5f, btnW, btnH);
                if (i == _building.CurrentConfigIndex)
                    GUI.Box(r, label);
                else if (GUI.Button(r, label))
                    _building.SwitchConfig(i);
            }
        }

        public void OnUpdate() { }
    }
}
