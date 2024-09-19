using UnityEngine;
using UnityEngine.SceneManagement;

namespace XFramework.Unity
{
    public class CoreManager : MonoBehaviour, ICoreManager
    {
        private void Awake()
        {
            LoadDrivers();
            Global.RegisterManager<ICoreManager>(this);
        }

        private void OnApplicationQuit()
        {
            ShutdownFramework();
        }

        public void QuitGame()
        {
            XLog.Info("[XFramework.Unity] [CoreManager] Quit game...");
            ShutdownFramework();
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        }

        public void RestartGame()
        {
            ShutdownFramework();
            XLog.Info("[XFramework.Unity] [CoreManager] Restarting game...");
            SceneManager.LoadScene(0);
        }

        public void ShutdownFramework()
        {
            XLog.Info("[XFramework.Unity] [CoreManager] Shutdown XFramework...");
            Destroy(gameObject);
        }

        /// <summary>
        /// 加载驱动
        /// </summary>
        private void LoadDrivers()
        {
            var logDriver = new LogDriver();
            XLog.RegisterDriver(logDriver);
        }
    }
}