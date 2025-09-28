# Unity Resource Management Systems: In-Depth Technical Comparison

## Table of Contents
1. [Overview](#overview)
2. [Resources System](#resources-system)
3. [AssetBundle System](#assetbundle-system)
4. [Addressable Asset System](#addressable-asset-system)
5. [Performance Comparison](#performance-comparison)
6. [Migration Strategies](#migration-strategies)
7. [Best Practices and Recommendations](#best-practices-and-recommendations)

## Overview

Unity provides three main systems for managing assets at runtime: Resources, AssetBundles, and Addressables. Each system has evolved to address limitations of its predecessors while maintaining different use cases and complexity levels.

### Evolution Timeline
- **Resources System** (Unity 1.0+): Original built-in system
- **AssetBundles** (Unity 2.5+): Advanced content delivery system
- **Addressables** (Unity 2018.2+): Modern unified asset management

## Resources System

### Architecture

The Resources system is Unity's oldest asset management approach, storing assets in special "Resources" folders that get compiled into the build.

```csharp
// Basic Resources loading
public class ResourcesExample : MonoBehaviour
{
    // Synchronous loading
    void LoadTextureSync()
    {
        // Path relative to any Resources folder
        Texture2D texture = Resources.Load<Texture2D>("Textures/PlayerAvatar");
        
        if (texture != null)
        {
            GetComponent<Renderer>().material.mainTexture = texture;
        }
    }
    
    // Asynchronous loading
    IEnumerator LoadTextureAsync()
    {
        ResourceRequest request = Resources.LoadAsync<Texture2D>("Textures/PlayerAvatar");
        
        while (!request.isDone)
        {
            float progress = request.progress;
            Debug.Log($"Loading progress: {progress * 100}%");
            yield return null;
        }
        
        Texture2D texture = request.asset as Texture2D;
        if (texture != null)
        {
            GetComponent<Renderer>().material.mainTexture = texture;
        }
    }
    
    // Loading all assets of type
    void LoadAllSprites()
    {
        Sprite[] allSprites = Resources.LoadAll<Sprite>("Sprites/Characters");
        foreach (var sprite in allSprites)
        {
            Debug.Log($"Loaded sprite: {sprite.name}");
        }
    }
    
    // Memory management
    void UnloadUnusedAssets()
    {
        // Unload a specific asset
        Texture2D texture = Resources.Load<Texture2D>("Textures/LargeTexture");
        // Use the texture...
        
        // Explicitly unload
        Resources.UnloadAsset(texture);
        
        // Or unload all unused assets
        Resources.UnloadUnusedAssets();
    }
}
```

### Internal Implementation Details

```csharp
// How Resources system works internally (conceptual)
public class ResourcesInternals
{
    // Resources are indexed at build time
    private static Dictionary<string, int> resourcePathToId = new Dictionary<string, int>();
    private static Dictionary<int, UnityEngine.Object> loadedResources = new Dictionary<int, UnityEngine.Object>();
    
    // Build-time processing
    [UnityEditor.InitializeOnLoadMethod]
    static void BuildResourcesIndex()
    {
        // Unity scans all Resources folders
        string[] resourceFolders = Directory.GetDirectories(
            Application.dataPath, 
            "Resources", 
            SearchOption.AllDirectories
        );
        
        foreach (string folder in resourceFolders)
        {
            // Index all assets in Resources folders
            string[] assets = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
            foreach (string assetPath in assets)
            {
                if (!assetPath.EndsWith(".meta"))
                {
                    string relativePath = GetRelativeResourcesPath(assetPath);
                    int assetId = GenerateAssetId(relativePath);
                    resourcePathToId[relativePath] = assetId;
                }
            }
        }
    }
    
    // Runtime loading simulation
    public static T Load<T>(string path) where T : UnityEngine.Object
    {
        if (resourcePathToId.TryGetValue(path, out int assetId))
        {
            if (!loadedResources.ContainsKey(assetId))
            {
                // Load from built-in archive
                loadedResources[assetId] = LoadFromArchive(assetId);
            }
            return loadedResources[assetId] as T;
        }
        return null;
    }
}
```

### Limitations and Issues

```csharp
public class ResourcesLimitations
{
    // Problem 1: All Resources assets are included in build
    void BuildSizeIssue()
    {
        // These assets are ALWAYS included, even if never used
        var unusedAsset1 = "Resources/Debug/TestTexture.png";  // Still in build
        var unusedAsset2 = "Resources/Temp/LargeModel.fbx";    // Still in build
        
        // No way to exclude at runtime or build variants
    }
    
    // Problem 2: No asset dependencies tracking
    void DependencyIssue()
    {
        // Loading a prefab doesn't provide dependency info
        GameObject prefab = Resources.Load<GameObject>("Prefabs/Enemy");
        
        // All referenced materials, textures, etc. are loaded automatically
        // but you can't know what they are or manage them individually
    }
    
    // Problem 3: Poor memory management
    void MemoryIssue()
    {
        // No reference counting
        var texture1 = Resources.Load<Texture2D>("Textures/Large");
        var texture2 = Resources.Load<Texture2D>("Textures/Large"); // Loaded again?
        
        // Manual unloading required
        Resources.UnloadAsset(texture1);
        // texture2 might still reference the same asset - undefined behavior
    }
    
    // Problem 4: No streaming or dynamic loading
    void StreamingIssue()
    {
        // Cannot load from web
        // Cannot load DLC content
        // Cannot have platform-specific assets
        // Everything must be in the initial build
    }
}
```

## AssetBundle System

### Architecture and Core Concepts

AssetBundles are Unity's file format for storing assets separately from the main build, enabling dynamic content delivery.

```csharp
// AssetBundle creation (Editor only)
using UnityEditor;
using System.Collections.Generic;

public class AssetBundleBuilder
{
    [MenuItem("Assets/Build AssetBundles")]
    static void BuildAllAssetBundles()
    {
        string assetBundleDirectory = "Assets/StreamingAssets/AssetBundles";
        
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        
        // Build with different options
        BuildAssetBundleOptions options = 
            BuildAssetBundleOptions.ChunkBasedCompression | // LZ4 compression
            BuildAssetBundleOptions.DeterministicAssetBundle | // Consistent builds
            BuildAssetBundleOptions.StrictMode; // Fail on errors
        
        // Build for current platform
        BuildPipeline.BuildAssetBundles(
            assetBundleDirectory,
            options,
            EditorUserBuildSettings.activeBuildTarget
        );
        
        // Build with manifest
        AssetBundleBuild[] buildMap = new AssetBundleBuild[2];
        
        buildMap[0].assetBundleName = "characters";
        buildMap[0].assetNames = new string[] {
            "Assets/Prefabs/Player.prefab",
            "Assets/Prefabs/Enemy.prefab"
        };
        
        buildMap[1].assetBundleName = "environments";
        buildMap[1].assetNames = new string[] {
            "Assets/Prefabs/Forest.prefab",
            "Assets/Prefabs/Desert.prefab"
        };
        
        BuildPipeline.BuildAssetBundles(
            assetBundleDirectory,
            buildMap,
            options,
            BuildTarget.StandaloneWindows
        );
    }
}
```

### Loading AssetBundles

```csharp
public class AssetBundleLoader : MonoBehaviour
{
    private Dictionary<string, AssetBundle> loadedBundles = new Dictionary<string, AssetBundle>();
    private Dictionary<string, int> bundleRefCount = new Dictionary<string, int>();
    
    // Load from local file
    IEnumerator LoadLocalBundle(string bundleName)
    {
        string path = Path.Combine(Application.streamingAssetsPath, "AssetBundles", bundleName);
        
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
        yield return request;
        
        AssetBundle bundle = request.assetBundle;
        if (bundle != null)
        {
            loadedBundles[bundleName] = bundle;
            bundleRefCount[bundleName] = 1;
            
            // Load specific asset
            AssetBundleRequest assetRequest = bundle.LoadAssetAsync<GameObject>("Player");
            yield return assetRequest;
            
            GameObject prefab = assetRequest.asset as GameObject;
            Instantiate(prefab);
        }
    }
    
    // Load from web with caching
    IEnumerator LoadWebBundle(string url, uint version)
    {
        using (UnityWebRequest request = UnityWebRequestAssetBundle.GetAssetBundle(url, version))
        {
            yield return request.SendWebRequest();
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                AssetBundle bundle = DownloadHandlerAssetBundle.GetContent(request);
                
                // Cache management
                if (Caching.ready)
                {
                    var cacheInfo = new CachedAssetBundle(bundle.name, Hash128.Parse(version.ToString()));
                    if (Caching.IsVersionCached(cacheInfo))
                    {
                        Debug.Log("Bundle loaded from cache");
                    }
                }
                
                loadedBundles[bundle.name] = bundle;
            }
        }
    }
    
    // Advanced loading with dependencies
    class AssetBundleDependencyManager
    {
        private AssetBundleManifest manifest;
        private Dictionary<string, AssetBundle> bundles = new Dictionary<string, AssetBundle>();
        
        public IEnumerator Initialize(string manifestPath)
        {
            AssetBundle manifestBundle = AssetBundle.LoadFromFile(manifestPath);
            manifest = manifestBundle.LoadAsset<AssetBundleManifest>("AssetBundleManifest");
            manifestBundle.Unload(false);
            yield break;
        }
        
        public IEnumerator LoadBundleWithDependencies(string bundleName)
        {
            // Load dependencies first
            string[] dependencies = manifest.GetAllDependencies(bundleName);
            foreach (string dep in dependencies)
            {
                if (!bundles.ContainsKey(dep))
                {
                    yield return LoadBundleInternal(dep);
                }
            }
            
            // Load the requested bundle
            if (!bundles.ContainsKey(bundleName))
            {
                yield return LoadBundleInternal(bundleName);
            }
        }
        
        private IEnumerator LoadBundleInternal(string bundleName)
        {
            string path = GetBundlePath(bundleName);
            AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(path);
            yield return request;
            
            if (request.assetBundle != null)
            {
                bundles[bundleName] = request.assetBundle;
            }
        }
    }
}
```

### Memory Management and Optimization

```csharp
public class AssetBundleMemoryManager
{
    private class BundleInfo
    {
        public AssetBundle bundle;
        public int referenceCount;
        public float lastAccessTime;
        public long memorySize;
        public HashSet<string> loadedAssets = new HashSet<string>();
    }
    
    private Dictionary<string, BundleInfo> bundleCache = new Dictionary<string, BundleInfo>();
    private long maxCacheSize = 100 * 1024 * 1024; // 100 MB
    private long currentCacheSize = 0;
    
    // Load with reference counting
    public IEnumerator LoadAsset<T>(string bundleName, string assetName) where T : UnityEngine.Object
    {
        BundleInfo info;
        
        if (!bundleCache.TryGetValue(bundleName, out info))
        {
            // Load bundle
            string path = GetBundlePath(bundleName);
            AssetBundle bundle = AssetBundle.LoadFromFile(path);
            
            info = new BundleInfo
            {
                bundle = bundle,
                referenceCount = 0,
                lastAccessTime = Time.realtimeSinceStartup,
                memorySize = EstimateBundleSize(bundle)
            };
            
            bundleCache[bundleName] = info;
            currentCacheSize += info.memorySize;
            
            // Check cache size
            if (currentCacheSize > maxCacheSize)
            {
                yield return EvictLeastRecentlyUsed();
            }
        }
        
        info.referenceCount++;
        info.lastAccessTime = Time.realtimeSinceStartup;
        
        // Load asset
        if (!info.loadedAssets.Contains(assetName))
        {
            AssetBundleRequest request = info.bundle.LoadAssetAsync<T>(assetName);
            yield return request;
            info.loadedAssets.Add(assetName);
        }
    }
    
    // Unload with reference counting
    public void ReleaseAsset(string bundleName)
    {
        if (bundleCache.TryGetValue(bundleName, out BundleInfo info))
        {
            info.referenceCount--;
            
            if (info.referenceCount <= 0)
            {
                // Optionally unload immediately or mark for cleanup
                UnloadBundle(bundleName, false);
            }
        }
    }
    
    // LRU eviction
    private IEnumerator EvictLeastRecentlyUsed()
    {
        var sortedBundles = bundleCache
            .Where(kvp => kvp.Value.referenceCount == 0)
            .OrderBy(kvp => kvp.Value.lastAccessTime)
            .ToList();
        
        foreach (var kvp in sortedBundles)
        {
            if (currentCacheSize <= maxCacheSize * 0.8f) // Keep 20% buffer
                break;
                
            UnloadBundle(kvp.Key, true);
            yield return Resources.UnloadUnusedAssets();
        }
    }
    
    private void UnloadBundle(string bundleName, bool unloadAllObjects)
    {
        if (bundleCache.TryGetValue(bundleName, out BundleInfo info))
        {
            currentCacheSize -= info.memorySize;
            info.bundle.Unload(unloadAllObjects);
            bundleCache.Remove(bundleName);
        }
    }
}
```

### Platform-Specific Variants

```csharp
public class PlatformSpecificBundles
{
    // Build variants for different platforms
    public static void BuildPlatformBundles()
    {
        var builds = new List<AssetBundleBuild>();
        
        // Texture variants
        var textureBuild = new AssetBundleBuild
        {
            assetBundleName = "textures",
            assetBundleVariant = GetTextureVariant(),
            assetNames = GetTextureAssets()
        };
        builds.Add(textureBuild);
        
        // Shader variants
        var shaderBuild = new AssetBundleBuild
        {
            assetBundleName = "shaders",
            assetBundleVariant = GetShaderVariant(),
            assetNames = GetShaderAssets()
        };
        builds.Add(shaderBuild);
        
        BuildPipeline.BuildAssetBundles(
            "AssetBundles",
            builds.ToArray(),
            BuildAssetBundleOptions.None,
            EditorUserBuildSettings.activeBuildTarget
        );
    }
    
    private static string GetTextureVariant()
    {
        #if UNITY_ANDROID || UNITY_IOS
            return "mobile";
        #elif UNITY_PS4 || UNITY_XBOXONE
            return "console";
        #else
            return "pc";
        #endif
    }
    
    // Runtime loading with variants
    public IEnumerator LoadPlatformSpecificBundle(string bundleName)
    {
        string variant = GetCurrentPlatformVariant();
        string fullBundleName = $"{bundleName}.{variant}";
        
        // Try platform-specific first
        string path = Path.Combine(Application.streamingAssetsPath, fullBundleName);
        
        if (!File.Exists(path))
        {
            // Fall back to default
            path = Path.Combine(Application.streamingAssetsPath, bundleName);
        }
        
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        yield return bundle;
    }
}
```

## Addressable Asset System

### Architecture and Core Concepts

Addressables unify Resources and AssetBundles into a single, flexible system with powerful runtime and editor features.

```csharp
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

public class AddressablesExample : MonoBehaviour
{
    // Basic loading
    async void LoadAssetByAddress()
    {
        // Load by address (key)
        AsyncOperationHandle<GameObject> handle = 
            Addressables.LoadAssetAsync<GameObject>("Player");
        
        await handle.Task;
        
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            GameObject player = handle.Result;
            Instantiate(player);
        }
        
        // IMPORTANT: Always release handles
        Addressables.Release(handle);
    }
    
    // Loading with labels
    async void LoadAssetsByLabel()
    {
        // Load all assets with a specific label
        AsyncOperationHandle<IList<GameObject>> handle = 
            Addressables.LoadAssetsAsync<GameObject>(
                "enemies", 
                null // callback for each loaded asset
            );
        
        IList<GameObject> enemies = await handle.Task;
        
        foreach (var enemy in enemies)
        {
            Instantiate(enemy);
        }
        
        Addressables.Release(handle);
    }
    
    // Direct reference loading
    [SerializeField] private AssetReference assetReference;
    [SerializeField] private AssetReferenceGameObject gameObjectReference;
    [SerializeField] private AssetReferenceTexture2D textureReference;
    
    async void LoadDirectReferences()
    {
        // Type-safe loading
        AsyncOperationHandle<GameObject> goHandle = 
            gameObjectReference.LoadAssetAsync();
        
        GameObject go = await goHandle.Task;
        
        // Or instantiate directly
        AsyncOperationHandle<GameObject> instanceHandle = 
            gameObjectReference.InstantiateAsync();
        
        GameObject instance = await instanceHandle.Task;
    }
}
```

### Advanced Addressables Features

```csharp
public class AdvancedAddressables : MonoBehaviour
{
    // Resource location management
    async void LoadByLocation()
    {
        // Get locations first
        var locHandle = Addressables.LoadResourceLocationsAsync("player");
        IList<IResourceLocation> locations = await locHandle.Task;
        
        if (locations.Count > 0)
        {
            // Load from specific location
            var assetHandle = Addressables.LoadAssetAsync<GameObject>(locations[0]);
            GameObject asset = await assetHandle.Task;
            
            // Get metadata
            Debug.Log($"Location: {locations[0].PrimaryKey}");
            Debug.Log($"Provider: {locations[0].ProviderId}");
            Debug.Log($"Dependencies: {locations[0].Dependencies.Count}");
        }
        
        Addressables.Release(locHandle);
    }
    
    // Scene management
    async void LoadSceneAddressable()
    {
        var handle = Addressables.LoadSceneAsync(
            "GameplayScene", 
            LoadSceneMode.Additive
        );
        
        SceneInstance sceneInstance = await handle.Task;
        
        // Later: unload scene
        await Addressables.UnloadSceneAsync(handle);
    }
    
    // Download management
    async void PredownloadContent()
    {
        // Get download size
        var sizeHandle = Addressables.GetDownloadSizeAsync("DLCContent");
        long downloadSize = await sizeHandle.Task;
        
        Debug.Log($"Need to download: {downloadSize / (1024f * 1024f)} MB");
        
        if (downloadSize > 0)
        {
            // Download dependencies
            var downloadHandle = Addressables.DownloadDependenciesAsync("DLCContent");
            
            while (!downloadHandle.IsDone)
            {
                float percent = downloadHandle.GetDownloadStatus().Percent;
                Debug.Log($"Download progress: {percent * 100}%");
                await Task.Yield();
            }
        }
        
        Addressables.Release(sizeHandle);
    }
    
    // Memory management with reference counting
    class AddressableMemoryManager
    {
        private Dictionary<string, AsyncOperationHandle> handles = 
            new Dictionary<string, AsyncOperationHandle>();
        private Dictionary<string, int> refCounts = 
            new Dictionary<string, int>();
        
        public async Task<T> LoadAssetAsync<T>(string address) where T : UnityEngine.Object
        {
            if (!handles.ContainsKey(address))
            {
                var handle = Addressables.LoadAssetAsync<T>(address);
                handles[address] = handle;
                refCounts[address] = 0;
                await handle.Task;
            }
            
            refCounts[address]++;
            return (T)handles[address].Result;
        }
        
        public void ReleaseAsset(string address)
        {
            if (refCounts.ContainsKey(address))
            {
                refCounts[address]--;
                
                if (refCounts[address] <= 0)
                {
                    Addressables.Release(handles[address]);
                    handles.Remove(address);
                    refCounts.Remove(address);
                }
            }
        }
    }
}
```

### Addressables Configuration and Build

```csharp
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Build;

public class AddressablesBuildConfiguration
{
    // Custom build script
    public class CustomBuildScript : BuildScriptBase
    {
        public override string Name => "Custom Build Script";
        
        protected override TResult BuildDataImplementation<TResult>(
            AddressablesDataBuilderInput context)
        {
            // Custom pre-build logic
            PrepareCustomData();
            
            // Call base implementation
            var result = base.BuildDataImplementation<TResult>(context);
            
            // Custom post-build logic
            ProcessBuildResult(result);
            
            return result;
        }
        
        private void PrepareCustomData()
        {
            // Custom data preparation
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            
            // Modify groups programmatically
            foreach (var group in settings.groups)
            {
                if (group.name.Contains("Remote"))
                {
                    // Set remote load path
                    var schema = group.GetSchema<BundledAssetGroupSchema>();
                    if (schema != null)
                    {
                        schema.LoadPath.SetVariableByName(settings, "Remote.LoadPath");
                    }
                }
            }
        }
    }
    
    // Runtime path configuration
    public class CustomResourceLocator
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeAddressablesPaths()
        {
            Addressables.InternalIdTransformFunc = TransformInternalId;
            Addressables.WebRequestOverride = CustomWebRequest;
        }
        
        static string TransformInternalId(IResourceLocation location)
        {
            // Custom path transformation
            if (location.InternalId.StartsWith("http"))
            {
                // Replace with CDN URL
                return location.InternalId.Replace(
                    "http://localhost", 
                    "https://cdn.mygame.com"
                );
            }
            return location.InternalId;
        }
        
        static void CustomWebRequest(UnityWebRequest request)
        {
            // Add custom headers
            request.SetRequestHeader("X-Game-Version", Application.version);
            request.SetRequestHeader("X-Platform", Application.platform.ToString());
        }
    }
    
    // Profile variables
    public static void SetupProfiles()
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        
        // Create custom profile
        string profileId = settings.profileSettings.AddProfile("Production", settings.activeProfileId);
        
        // Set variables
        settings.profileSettings.SetValue(profileId, "Remote.LoadPath", 
            "https://cdn.mygame.com/[BuildTarget]");
        settings.profileSettings.SetValue(profileId, "Remote.BuildPath", 
            "ServerData/[BuildTarget]");
        
        // Set active profile
        settings.activeProfileId = profileId;
    }
}
```

### Addressables Catalog and Content Update

```csharp
public class AddressablesCatalogManager
{
    // Content catalog update
    public async Task<bool> CheckForCatalogUpdates()
    {
        var handle = Addressables.CheckForCatalogUpdates();
        List<string> catalogs = await handle.Task;
        
        if (catalogs.Count > 0)
        {
            Debug.Log($"Found {catalogs.Count} catalog updates");
            
            // Update catalogs
            var updateHandle = Addressables.UpdateCatalogs(catalogs);
            await updateHandle.Task;
            
            Addressables.Release(updateHandle);
            return true;
        }
        
        Addressables.Release(handle);
        return false;
    }
    
    // Content versioning
    public class ContentVersionManager
    {
        private const string VERSION_KEY = "ContentVersion";
        
        public async Task<bool> NeedsUpdate()
        {
            // Load remote version
            var versionHandle = Addressables.LoadAssetAsync<TextAsset>("version.txt");
            TextAsset versionAsset = await versionHandle.Task;
            string remoteVersion = versionAsset.text;
            
            // Compare with local version
            string localVersion = PlayerPrefs.GetString(VERSION_KEY, "0.0.0");
            
            bool needsUpdate = CompareVersions(remoteVersion, localVersion) > 0;
            
            if (needsUpdate)
            {
                PlayerPrefs.SetString(VERSION_KEY, remoteVersion);
            }
            
            Addressables.Release(versionHandle);
            return needsUpdate;
        }
        
        private int CompareVersions(string v1, string v2)
        {
            var parts1 = v1.Split('.').Select(int.Parse).ToArray();
            var parts2 = v2.Split('.').Select(int.Parse).ToArray();
            
            for (int i = 0; i < Math.Min(parts1.Length, parts2.Length); i++)
            {
                if (parts1[i] > parts2[i]) return 1;
                if (parts1[i] < parts2[i]) return -1;
            }
            
            return parts1.Length.CompareTo(parts2.Length);
        }
    }
}
```

## Performance Comparison

### Memory Usage Analysis

```csharp
public class PerformanceComparison
{
    // Resources memory profile
    public class ResourcesMemoryProfile
    {
        public void AnalyzeMemory()
        {
            // All resources loaded at once
            long beforeMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            
            // Load multiple textures
            var textures = new List<Texture2D>();
            for (int i = 0; i < 10; i++)
            {
                textures.Add(Resources.Load<Texture2D>($"Textures/Texture_{i}"));
            }
            
            long afterMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long memoryUsed = afterMemory - beforeMemory;
            
            Debug.Log($"Resources - Memory used: {memoryUsed / (1024f * 1024f)} MB");
            
            // No automatic unloading
            // Memory stays allocated until explicit unload
        }
    }
    
    // AssetBundle memory profile
    public class AssetBundleMemoryProfile
    {
        public IEnumerator AnalyzeMemory()
        {
            long beforeMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            
            // Load bundle
            AssetBundle bundle = AssetBundle.LoadFromFile("path/to/bundle");
            
            // Load specific assets only
            var textures = new List<Texture2D>();
            for (int i = 0; i < 10; i++)
            {
                var request = bundle.LoadAssetAsync<Texture2D>($"Texture_{i}");
                yield return request;
                textures.Add(request.asset as Texture2D);
            }
            
            long afterMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long memoryUsed = afterMemory - beforeMemory;
            
            Debug.Log($"AssetBundle - Memory used: {memoryUsed / (1024f * 1024f)} MB");
            
            // Can unload bundle but keep assets
            bundle.Unload(false);
        }
    }
    
    // Addressables memory profile
    public class AddressablesMemoryProfile
    {
        public async Task AnalyzeMemory()
        {
            long beforeMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            
            var handles = new List<AsyncOperationHandle<Texture2D>>();
            
            // Load with automatic dependency management
            for (int i = 0; i < 10; i++)
            {
                var handle = Addressables.LoadAssetAsync<Texture2D>($"Texture_{i}");
                handles.Add(handle);
                await handle.Task;
            }
            
            long afterMemory = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long memoryUsed = afterMemory - beforeMemory;
            
            Debug.Log($"Addressables - Memory used: {memoryUsed / (1024f * 1024f)} MB");
            
            // Reference counting ensures proper cleanup
            foreach (var handle in handles)