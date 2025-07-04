using YooAsset;
using UnityEngine;
using System.Collections;
using XFramework.Utils;
using System;
using Cysharp.Threading.Tasks;
using UnityEngine.SceneManagement;
using System.Collections.Generic;

namespace XFramework
{
    /// <summary>
    /// 资源加载管理器，依赖于 YooAsset
    /// </summary>
    /// <remarks>目前只支持单个默认资源包，后续可以扩展支持多个资源包</remarks>
    [DisallowMultipleComponent]
    [AddComponentMenu("XFramework/Asset Manager")]
    public sealed class AssetManager : XFrameworkComponent
    {
        internal enum BuildMode
        {
            Editor,  // 编辑器模式，编辑器下模拟运行游戏，只在编辑器下有效
            Offline, // 单机运行模式，不需要热更新资源的游戏
            Online,  // 联机模式，需要热更新资源的游戏
            WebGL,   // 针对 WebGL 的特殊模式
        }

        [Header("资源构建模式")]
        [SerializeField]
        BuildMode _buildMode;

        [Header("主要 Package 名称")]
        [SerializeField]
        private string _mainPackageName = "DefaultPackage";

        [Header("资源下载配置")]
        [SerializeField]
        private string _defaultHostServer = "http://<Server>/CDN/<Platform>/<Version>";

        [SerializeField]
        private string _fallbackHostServer = "http://<Server>/CDN/<Platform>/<Version>";

        [SerializeField]
        private int _maxConcurrentDownloadCount = 10;

        [SerializeField]
        private int _failedDownloadRetryCount = 3;

        private ResourcePackage _package;
        private string _packageVersion;

        // 资源下载回调
        private Action<DownloaderFinishData> _onDownloadFinish;
        private Action<DownloadErrorData> _onDownloadError;
        private Action<DownloadUpdateData> _onDownloadUpdate;
        private Action<DownloadFileData> _onDownloadFileBegin;

        private readonly Dictionary<string, AssetHandle> _assetHandleDict = new();

        internal override int Priority
        {
            get => Global.PriorityValue.AssetManager;
        }

        internal override void Init()
        {
            base.Init();

            YooAssets.Initialize();
            // 获取资源包对象，如果资源包不存在，则创建资源包
            // 注意：需要先在 Collector 创建同名 Package
            _package = YooAssets.TryGetPackage(_mainPackageName);
            if (_package == null)
            {
                _package = YooAssets.CreatePackage(_mainPackageName);
            }
            // 设置默认资源包，之后可以直接使用 YooAssets.XXX 接口来加载该资源包内容
            YooAssets.SetDefaultPackage(_package);

            foreach (AssetHandle handle in _assetHandleDict.Values)
            {
                handle.Release();
            }
            _assetHandleDict.Clear();
        }

        internal override void Clear()
        {
            base.Clear();

            _package = null;
            _packageVersion = null;

            _onDownloadFinish = null;
            _onDownloadError = null;
            _onDownloadUpdate = null;
            _onDownloadFileBegin = null;
        }

        #region 资源管理接口

        public void InitPackageAsync(Action callback = null)
        {
            // 初始化资源包
            StartCoroutine(InitPackageInternal(() =>
            {
                StartCoroutine(RequestPackageVersion(() =>
                {
                    StartCoroutine(UpdatePackageManifest(() =>
                    {
                        StartCoroutine(UpdatePackageFiles(callback));
                    }));
                }));
            }));
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        /// <typeparam name="T">资源类型</typeparam>
        /// <param name="address">资源地址</param>
        /// <returns>资源句柄</returns>
        public async UniTask<AssetHandle> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            AssetHandle handle = _package.LoadAssetAsync<T>(address);
            await handle.Task.AsUniTask();
            Log.Debug($"[XFramework] [AssetManager] Load asset ({address}) succeed.");
            return handle;
        }

        /// <summary>
        /// 加载资源
        /// </summary>
        /// <param name="address">资源地址</param>
        /// <param name="callback">回调</param>
        public void LoadAssetAsync<T>(string address, Action<AssetHandle> callback) where T : UnityEngine.Object
        {
            AssetHandle handle = _package.LoadAssetAsync<T>(address);
            handle.Completed += (resultHandle) =>
            {
                Log.Debug($"[XFramework] [AssetManager] Load asset ({address}) succeed.");
                callback?.Invoke(resultHandle);
            };
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        internal async UniTask<SceneHandle> LoadSceneAsync(string address, LoadSceneMode mode = LoadSceneMode.Single)
        {
            SceneHandle handle = _package.LoadSceneAsync(address, mode);
            await handle.Task.AsUniTask();
            Log.Debug($"[XFramework] [AssetManager] Load scene ({handle.SceneName}) succeed.");
            return handle;
        }

        /// <summary>
        /// 加载场景
        /// </summary>
        internal void LoadSceneAsync(string address, LoadSceneMode mode, Action<SceneHandle> callback)
        {
            SceneHandle handle = _package.LoadSceneAsync(address, mode);
            handle.Completed += (resultHandle) =>
            {
                Log.Debug($"[XFramework] [AssetManager] Load scene ({handle.SceneName}) succeed.");
                callback?.Invoke(resultHandle);
            };
        }

        /// <summary>
        /// 尝试卸载指定资源
        /// </summary>
        public void TryUnloadUnusedAsset(string address)
        {
            _package.TryUnloadUnusedAsset(address);
        }

        /// <summary>
        /// 卸载所有未使用的资源
        /// </summary>
        public async UniTask UnloadUnusedAssetsAsync()
        {
            var operation = _package.UnloadUnusedAssetsAsync();
            await operation.Task.AsUniTask();
        }

        /// <summary>
        /// 强制卸载所有资源
        /// </summary>
        public async UniTask ForceUnloadAllAssetsAsync()
        {
            var operation = _package.UnloadAllAssetsAsync();
            await operation.Task.AsUniTask();
            _assetHandleDict.Clear();
        }

        #endregion

        #region 初始化和销毁

        /// <summary>
        /// 初始化资源包
        /// </summary>
        private IEnumerator InitPackageInternal(Action callback = null)
        {
            InitializationOperation operation = null;
            switch (_buildMode)
            {
                case BuildMode.Editor:
                    var simulateBuildResult = EditorSimulateModeHelper.SimulateBuild(_mainPackageName);
                    var initParametersEditor = new EditorSimulateModeParameters()
                    {
                        EditorFileSystemParameters = FileSystemParameters.CreateDefaultEditorFileSystemParameters(simulateBuildResult.PackageRootDirectory)
                    };
                    operation = _package.InitializeAsync(initParametersEditor);
                    break;
                case BuildMode.Offline:
                    var initParametersOffline = new OfflinePlayModeParameters
                    {
                        BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters()
                    };
                    operation = _package.InitializeAsync(initParametersOffline);
                    break;
                case BuildMode.Online:
                    IRemoteServices remoteServicesOnline = new RemoteServices(_defaultHostServer, _fallbackHostServer);
                    var initParametersOnline = new HostPlayModeParameters
                    {
                        BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(),
                        CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServicesOnline)
                    };
                    operation = _package.InitializeAsync(initParametersOnline);
                    break;
                case BuildMode.WebGL:
                    IRemoteServices remoteServicesWebGL = new RemoteServices(_defaultHostServer, _fallbackHostServer);
                    var initParametersWebGL = new WebPlayModeParameters
                    {
                        WebServerFileSystemParameters = FileSystemParameters.CreateDefaultWebServerFileSystemParameters(),
                        WebRemoteFileSystemParameters = FileSystemParameters.CreateDefaultWebRemoteFileSystemParameters(remoteServicesWebGL)
                    };
                    operation = _package.InitializeAsync(initParametersWebGL);
                    break;
                default:
                    Log.Error($"[XFramework] [AssetManager] Invalid package mode: {_buildMode}");
                    break;
            }
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Initialize package succeed. ({_buildMode})");
                callback?.Invoke();
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Initialize package failed. ({_buildMode}) {operation.Error}");
            }
        }

        /// <summary>
        /// 销毁资源包
        /// </summary>
        private IEnumerator DestroyPackageInternal()
        {
            if (_package == null)
            {
                yield break;
            }
            string packageName = _package.PackageName;
            DestroyOperation destroyOperation = _package.DestroyAsync();
            yield return destroyOperation;

            if (YooAssets.RemovePackage(_package))
            {
                Log.Debug($"[XFramework] [AssetManager] Destroy package ({packageName}) succeed.");
            }
        }

        #endregion

        #region 资源更新

        /// <summary>
        /// 获取资源版本
        /// </summary>
        private IEnumerator RequestPackageVersion(Action callback)
        {
            var operation = _package.RequestPackageVersionAsync();
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                _packageVersion = operation.PackageVersion;
                Log.Debug($"[XFramework] [AssetManager] Request package version succeed. {_packageVersion}");
                callback?.Invoke();
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Request package version failed. {operation.Error}");
            }
        }

        /// <summary>
        /// 根据版本号更新资源清单
        /// </summary>
        private IEnumerator UpdatePackageManifest(Action callback)
        {
            var operation = _package.UpdatePackageManifestAsync(_packageVersion);
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Update package manifest succeed. Latest version: {_packageVersion}");
                callback?.Invoke();
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Update package manifest failed. (Latest version: {_packageVersion}) {operation.Error}");
            }
        }

        /// <summary>
        /// 根据资源清单更新资源文件（下载到缓存资源）
        /// </summary>
        private IEnumerator UpdatePackageFiles(Action callback)
        {
            var downloader = _package.CreateResourceDownloader(_maxConcurrentDownloadCount, _failedDownloadRetryCount);

            if (downloader.TotalDownloadCount == 0)
            {
                Log.Debug("[XFramework] [AssetManager] No package files need to update.");
                callback?.Invoke();
                yield break;
            }

            int totalDownloadCount = downloader.TotalDownloadCount;
            long totalDownloadBytes = downloader.TotalDownloadBytes;

            downloader.DownloadFinishCallback = (finishData) =>
            {
                _onDownloadFinish?.Invoke(finishData);
            };
            downloader.DownloadErrorCallback = (errorData) =>
            {
                _onDownloadError?.Invoke(errorData);
            };
            downloader.DownloadUpdateCallback = (updateData) =>
            {
                _onDownloadUpdate?.Invoke(updateData);
            };
            downloader.DownloadFileBeginCallback = (fileData) =>
            {
                _onDownloadFileBegin?.Invoke(fileData);
            };

            downloader.BeginDownload();
            yield return downloader;

            if (downloader.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Update package files succeed. Total download count: {totalDownloadCount}, Total download bytes: {totalDownloadBytes}");
                callback?.Invoke();
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Update package files failed. {downloader.Error}");
            }
        }

        #endregion

        #region 资源移除

        /// <summary>
        /// 清理所有缓存资源文件
        /// </summary>
        private IEnumerator ClearAllCacheBundleFiles()
        {
            var operation = _package.ClearCacheFilesAsync(EFileClearMode.ClearAllBundleFiles);
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Clear all cache bundle files succeed.");
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Clear all cache bundle files failed. {operation.Error}");
            }
        }

        /// <summary>
        /// 清理未使用的缓存资源文件
        /// </summary>
        private IEnumerator ClearUnusedCacheBundleFiles()
        {
            var operation = _package.ClearCacheFilesAsync(EFileClearMode.ClearUnusedBundleFiles);
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Clear unused cache bundle files succeed.");
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Clear unused cache bundle files failed. {operation.Error}");
            }
        }

        /// <summary>
        /// 清理所有缓存清单文件
        /// </summary>
        private IEnumerator ClearAllCacheManifestFiles()
        {
            var operation = _package.ClearCacheFilesAsync(EFileClearMode.ClearAllManifestFiles);
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Clear all cache manifest files succeed.");
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Clear all cache manifest files failed. {operation.Error}");
            }
        }

        /// <summary>
        /// 清理未使用的缓存清单文件
        /// </summary>
        private IEnumerator ClearUnusedCacheManifestFiles()
        {
            var operation = _package.ClearCacheFilesAsync(EFileClearMode.ClearUnusedManifestFiles);
            yield return operation;

            if (operation.Status == EOperationStatus.Succeed)
            {
                Log.Debug($"[XFramework] [AssetManager] Clear unused cache manifest files succeed.");
            }
            else
            {
                Log.Error($"[XFramework] [AssetManager] Clear unused cache manifest files failed. {operation.Error}");
            }
        }

        #endregion

        public class RemoteServices : IRemoteServices
        {
            public RemoteServices(string defaultHostServer, string fallbackHostServer)
            {
                DefaultHostServer = defaultHostServer;
                FallbackHostServer = fallbackHostServer;
            }

            public string DefaultHostServer { get; private set; }
            public string FallbackHostServer { get; private set; }

            public string GetRemoteFallbackURL(string fileName)
            {
                return $"{FallbackHostServer}/{fileName}";
            }

            public string GetRemoteMainURL(string fileName)
            {
                return $"{DefaultHostServer}/{fileName}";
            }
        }
    }
}