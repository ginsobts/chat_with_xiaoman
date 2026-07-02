using UnityEngine;

namespace VN
{
    /// <summary>
    /// 运行时自动启动，无需在场景里手动挂任何脚本。
    /// 打开工程直接 Play，或打包后运行即可。
    /// </summary>
    public static class Boot
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (Object.FindObjectOfType<GameManager>() != null) return;
            var go = new GameObject("GameManager");
            go.AddComponent<GameManager>();
            Object.DontDestroyOnLoad(go);
        }
    }
}
