using UnityEngine;

namespace MarchingCubes.Sample
{
    /// <summary>
    /// 建造系统状态接口：定义进入、退出、更新、GUI 四个生命周期函数。
    /// </summary>
    public abstract class BuildController : MonoBehaviour, IBuildState
    {
        public abstract void OnEnter();

        public abstract void OnExit();
        
        public abstract void OnUpdate();
        
        public abstract void OnGUI();
    }
}