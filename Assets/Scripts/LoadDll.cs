using HybridCLR;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using YooAsset;

/// <summary>
/// 脚本工作流程：
/// 1.下载资源，用yooAsset资源框架进行下载
///    1.资源文件，ab包
///    2.热更新dll
/// 2.给AOT DLL补充元素据，通过RuntimeApi.LoadMetadataForAOTAssembly
/// 3.通过实例化prefab，运行热更代码
/// </summary>
public class LoadDll : MonoBehaviour
{
    /// <summary>
    /// 资源系统运行模式
    /// </summary>
    public EPlayMode PlayMode = EPlayMode.HostPlayMode;

    private ResourcePackage _defaultPackage;

    void Start()
    {
        StartCoroutine(InitYooAssets(StartGame));
    }

    #region YooAsset初始化

    IEnumerator InitYooAssets(Action onDownloadComplete)
    {
        // 1.初始化资源系统
        YooAssets.Initialize();
        string packageName = "DefaultPackage";
        var package = YooAssets.TryGetPackage(packageName) ?? YooAssets.CreatePackage(packageName);
        YooAssets.SetDefaultPackage(package);
        InitializationOperation initializationOperation = null; 
        //===================================适应新版本YooAsset插件的修改===================================
        if (PlayMode == EPlayMode.EditorSimulateMode)
        {   
            
            // 编辑器模拟模式
            var createParameters = new OfflinePlayModeParameters();
            initializationOperation = package.InitializeAsync(createParameters);
        }
        else if (PlayMode == EPlayMode.HostPlayMode)
        { 
            //联机运行模式
            string defaultHostServer = GetHostServerURL();
            string fallbackHostServer = GetHostServerURL();
            IRemoteServices remoteServices = new RemoteServices(defaultHostServer, fallbackHostServer);
            var createParameters = new HostPlayModeParameters { BuildinFileSystemParameters = FileSystemParameters.CreateDefaultBuildinFileSystemParameters(), CacheFileSystemParameters = FileSystemParameters.CreateDefaultCacheFileSystemParameters(remoteServices) };
            initializationOperation = package.InitializeAsync(createParameters);
        }

        yield return initializationOperation;
        //===================================适应新版本YooAsset插件的修改===================================
            
        if (initializationOperation.Status == EOperationStatus.Succeed)
        {
            Debug.Log("资源包初始化成功！");
        }
        else
        {
            Debug.LogError($"资源包初始化失败：{initializationOperation.Error}");
        }

        //2.获取资源版本
        var operation = package.RequestPackageVersionAsync();
        yield return operation;

        if (operation.Status != EOperationStatus.Succeed)
        {
            //更新失败
            Debug.LogError(operation.Error);
            yield break;
        }

        string packageVersion = operation.PackageVersion;
        Debug.Log($"Updated package Version : {packageVersion}");

        //3.更新补丁清单
        // 更新成功后自动保存版本号，作为下次初始化的版本。
        // 也可以通过operation.SavePackageVersion()方法保存。
        var operation2 = package.UpdatePackageManifestAsync(packageVersion);
        yield return operation2;

        if (operation2.Status != EOperationStatus.Succeed)
        {
            //更新失败
            Debug.LogError(operation2.Error);
            yield break;
        }

        //4.下载补丁包
        yield return Download();

        //判断是否下载成功
        var assets = new List<string> { "HotUpdate.dll" }.Concat(AOTMetaAssemblyFiles);
        foreach (var asset in assets)
        {
            var handle = package.LoadAssetAsync<TextAsset>(asset);
            yield return handle;
            var assetObj = handle.AssetObject as TextAsset;
            s_assetDatas[asset] = assetObj;
            Debug.Log($"dll:{asset}   {assetObj == null}");
        }

        _defaultPackage = package;
        onDownloadComplete();
    }
    
    private string GetHostServerURL()
    {
        //模拟下载地址，8084为Nginx里面设置的端口号，项目名，平台名
        return "http://127.0.0.1:8084/TestProject/PC";
    }

    /// <summary>
    /// 远端资源地址查询服务类
    /// </summary>
    private class RemoteServices : IRemoteServices
    {
        private readonly string _defaultHostServer;
        private readonly string _fallbackHostServer;

        public RemoteServices(string defaultHostServer, string fallbackHostServer)
        {
            _defaultHostServer = defaultHostServer;
            _fallbackHostServer = fallbackHostServer;
        }

        string IRemoteServices.GetRemoteMainURL(string fileName)
        {
            return $"{_defaultHostServer}/{fileName}";
        }

        string IRemoteServices.GetRemoteFallbackURL(string fileName)
        {
            return $"{_fallbackHostServer}/{fileName}";
        }
    }
    
//===================================适应新版本YooAsset插件的修改===================================
//     /// <summary>
//     /// 资源文件查询服务类
//     /// </summary>
//     internal class GameQueryServices : IBuildinQueryServices
//     {
//         public bool Query(string packageName, string fileName, string fileCRC)
//         {
// #if UNITY_IPHONE
//             throw new Exception("Ios平台需要内置资源");
//             return false;
// #else
//             return false;
// #endif
//         }
//     }
//===================================适应新版本YooAsset插件的修改===================================

    #endregion

    #region 下载热更资源

    IEnumerator Download()
    {
        int downloadingMaxNum = 10;
        int failedTryAgain = 3;
        var package = YooAssets.GetPackage("DefaultPackage");
        var downloader = package.CreateResourceDownloader(downloadingMaxNum, failedTryAgain);

        //没有需要下载的资源
        if (downloader.TotalDownloadCount == 0)
        {
            yield break;
        }

        //需要下载的文件总数和总大小
        int totalDownloadCount = downloader.TotalDownloadCount;
        long totalDownloadBytes = downloader.TotalDownloadBytes;

        //===================================适应新版本YooAsset插件的修改===================================
        //注册回调方法
        downloader.DownloadErrorCallback = OnDownloadErrorFunction;
        downloader.DownloadUpdateCallback = OnDownloadProgressUpdateFunction;
        downloader.DownloadFinishCallback = OnDownloadOverFunction;
        downloader.DownloadFileBeginCallback = OnStartDownloadFileFunction;
        //===================================适应新版本YooAsset插件的修改===================================

        //开启下载
        downloader.BeginDownload();
        yield return downloader;

        //检测下载结果
        if (downloader.Status == EOperationStatus.Succeed)
        {
            //下载成功
            Debug.Log("更新完成");
        }
        else
        {
            //下载失败
            Debug.Log("更新失败");
        }
    }

    //===================================适应新版本YooAsset插件的修改===================================
    /// <summary>
    /// 开始下载
    /// </summary>
    private void OnStartDownloadFileFunction(DownloadFileData downloadFileData)
    {
        Debug.Log($"开始下载：文件名：{downloadFileData.FileName}，文件大小：{downloadFileData.FileSize}");
    }

    /// <summary>
    /// 下载完成
    /// </summary>
    private void OnDownloadOverFunction(DownloaderFinishData downloaderFinishData)
    {
        Debug.Log("下载" + (downloaderFinishData.Succeed ? "成功" : "失败"));
    }

    /// <summary>
    /// 更新中
    /// </summary>
    private void OnDownloadProgressUpdateFunction(DownloadUpdateData downloadUpdateData)
    {
        Debug.Log($"文件总数：{downloadUpdateData.TotalDownloadCount}，已下载文件数：{downloadUpdateData.CurrentDownloadCount}，下载总大小：{downloadUpdateData.TotalDownloadBytes}，已下载大小{downloadUpdateData.CurrentDownloadBytes}");
    }

    /// <summary>
    /// 下载出错
    /// </summary>
    /// <param name="errorData"></param>
    private void OnDownloadErrorFunction(DownloadErrorData errorData)
    {
        Debug.Log($"下载出错：包名:{errorData.PackageName} 文件名：{errorData.FileName}，错误信息：{errorData.ErrorInfo}");
    }
    //===================================适应新版本YooAsset插件的修改===================================

    #endregion

    #region 补充元数据

    //补充元数据dll的列表
    //通过RuntimeApi.LoadMetadataForAOTAssembly()函数来补充AOT泛型的原始元数据
    private static List<string> AOTMetaAssemblyFiles { get; } = new() { "mscorlib.dll", "System.dll", "System.Core.dll", };
    private static Dictionary<string, TextAsset> s_assetDatas = new Dictionary<string, TextAsset>();
    private static Assembly _hotUpdateAss;
    
    public static byte[] ReadBytesFromStreamingAssets(string dllName)
    {
        if (s_assetDatas.ContainsKey(dllName))
        {
            return s_assetDatas[dllName].bytes;
        }

        return Array.Empty<byte>();
    }

    

    /// <summary>
    /// 为aot assembly加载原始metadata， 这个代码放aot或者热更新都行。
    /// 一旦加载后，如果AOT泛型函数对应native实现不存在，则自动替换为解释模式执行
    /// </summary>
    private static void LoadMetadataForAOTAssemblies()
    {
        /// 注意，补充元数据是给AOT dll补充元数据，而不是给热更新dll补充元数据。
        /// 热更新dll不缺元数据，不需要补充，如果调用LoadMetadataForAOTAssembly会返回错误
        HomologousImageMode mode = HomologousImageMode.SuperSet;
        foreach (var aotDllName in AOTMetaAssemblyFiles)
        {
            byte[] dllBytes = ReadBytesFromStreamingAssets(aotDllName);
            // 加载assembly对应的dll，会自动为它hook。一旦aot泛型函数的native函数不存在，用解释器版本代码
            LoadImageErrorCode err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, mode);
            Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. mode:{mode} ret:{err}");
        }
    }

    #endregion

    #region 运行测试

    void StartGame()
    {
        // 加载AOT dll的元数据
        LoadMetadataForAOTAssemblies();
        // 加载热更dll
#if !UNITY_EDITOR
        _hotUpdateAss = Assembly.Load(ReadBytesFromStreamingAssets("HotUpdate.dll"));
#else
        _hotUpdateAss = System.AppDomain.CurrentDomain.GetAssemblies().First(a => a.GetName().Name == "HotUpdate");
#endif
        Debug.Log("运行热更代码");
        StartCoroutine(Run_InstantiateComponentByAsset());
    }

    IEnumerator Run_InstantiateComponentByAsset()
    {
        // 通过实例化assetbundle中的资源，还原资源上的热更新脚本
        var package = YooAssets.GetPackage("DefaultPackage");
        var handle = package.LoadAssetAsync<GameObject>("Cube");
        yield return handle;
        handle.Completed += Handle_Completed;
    }

    private void Handle_Completed(AssetHandle obj)
    {
        Debug.Log("准备实例化");
        GameObject go = obj.InstantiateSync();
        Debug.Log($"Prefab name is {go.name}");
    }

    #endregion
}

