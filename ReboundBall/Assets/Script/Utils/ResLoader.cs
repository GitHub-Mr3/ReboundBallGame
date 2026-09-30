using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 预制体加载工具：所有预制体统一放在 Resources/Prefab 下面，业务代码只需要给名字。
///
/// 用法：
///     // 只加载，不实例化（同一个预制体只 Load 一次，后面走缓存）
///     GameObject prefab = ResLoader.Load("PetHouseView");
///
///     // 加载 + 实例化：挂到 parent 下（不传 parent 就直接实例化到场景根）
///     GameObject view = ResLoader.Create("PetHouseView", transform);
///     PetHouseView scr = ResLoader.Create<PetHouseView>("PetHouseView", transform);
///
///     // UI 弹窗 / 列表项：自动把 localPosition / anchoredPosition / localScale 归零，避免位置跑偏
///     GameObject ui = ResLoader.CreateUI("PetHouseView", transform);
///     PetHouseView uiScr = ResLoader.CreateUI<PetHouseView>("PetHouseView", transform);
///
///     // 异步版（Resources.LoadAsync，不卡当前帧；回调在主线程）
///     ResLoader.CreateAsync("PetHouseView", transform, go => { /* 用 go */ });
///     ResLoader.CreateAsync<PetHouseView>("PetHouseView", transform, scr => { /* 用 scr */ });
///
///     // 弹窗推荐用这个：只创建一次，重复调用只是再显示一次同一个实例，不会堆副本
///     ResLoader.CreateUIOnce("PetHouseView", uiRoot);
///     PetHouseView scr = ResLoader.CreateUIOnce<PetHouseView>("PetHouseView", uiRoot);
///     ResLoader.Close("PetHouseView");   // 关闭 = SetActive(false)，下次 CreateUIOnce 直接复用
///     ResLoader.IsOpen("PetHouseView");  // 是否显示中
///     ResLoader.DestroyUI("PetHouseView"); // 确实要销毁时才用
///
/// 支持子目录，名字里写相对路径即可：ResLoader.Create("UI/PetHouseView", transform);
/// 也兼容带前缀的写法：ResLoader.Create("Prefab/PetHouseView", transform);
/// 找不到的预制体会打 Error 并返回 null，调用处记得判空。
/// </summary>
public static class ResLoader
{
    private const string LogTag = "[ResLoader]";

    /// <summary>预制体根目录（相对 Resources）</summary>
    private const string RootPath = "Prefab";

    /// <summary>已加载过的预制体：key 是完整 Resources 路径</summary>
    private static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>(StringComparer.Ordinal);

    /// <summary>正在异步加载的路径，避免同一预制体被重复发起 LoadAsync</summary>
    private static readonly HashSet<string> loading = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>加载完成后要通知的回调（同一预制体被多处同时请求时用）</summary>
    private static readonly Dictionary<string, List<Action<GameObject>>> waiters =
        new Dictionary<string, List<Action<GameObject>>>(StringComparer.Ordinal);

    /// <summary>CreateUIOnce 创建出来的弹窗实例：key 是预制体完整路径，用来复用而不是重复 new</summary>
    private static readonly Dictionary<string, GameObject> instances =
        new Dictionary<string, GameObject>(StringComparer.Ordinal);

    // ==================== 同步 ====================

    /// <summary>按名字加载预制体资源本体（不实例化）</summary>
    public static GameObject Load(string prefabName)
    {
        string path = BuildPath(prefabName);
        if (string.IsNullOrEmpty(path)) return null;

        // 缓存里的资源被外部 Destroy 过（UnityObject 重载了 ==），这里会自动重新加载
        GameObject cached;
        if (cache.TryGetValue(path, out cached) && cached != null) return cached;

        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            DebugMgr.Instance.LogError($"{LogTag} 找不到预制体: Resources/{path}.prefab");
            return null;
        }

        cache[path] = prefab;
        return prefab;
    }

    /// <summary>按名字加载预制体并返回它身上的组件</summary>
    public static T Load<T>(string prefabName) where T : Component
    {
        GameObject prefab = Load(prefabName);
        return prefab != null ? GetComponent<T>(prefab) : null;
    }

    /// <summary>加载并实例化：parent 传 null 时生成到场景根</summary>
    public static GameObject Create(string prefabName, Transform parent = null)
    {
        GameObject prefab = Load(prefabName);
        return prefab != null ? InstantiateCore(prefab, parent) : null;
    }

    /// <summary>加载并实例化，直接返回实例上的组件</summary>
    public static T Create<T>(string prefabName, Transform parent = null) where T : Component
    {
        GameObject instance = Create(prefabName, parent);
        return instance != null ? GetComponent<T>(instance) : null;
    }

    /// <summary>UI 版：实例化后把位置 / 缩放归零，适合弹窗、列表项这类需要贴着父节点排布的对象</summary>
    public static GameObject CreateUI(string prefabName, Transform parent)
    {
        GameObject instance = Create(prefabName, parent);
        if (instance == null) return null;

        ResetTransform(instance.transform);
        return instance;
    }

    /// <summary>UI 版：实例化归零后返回组件</summary>
    public static T CreateUI<T>(string prefabName, Transform parent) where T : Component
    {
        GameObject instance = CreateUI(prefabName, parent);
        return instance != null ? GetComponent<T>(instance) : null;
    }

    // ==================== 单例弹窗：只创建一次，开关用 SetActive ====================

    /// <summary>
    /// 打开弹窗：第一次会加载并实例化，之后再调用只是把同一个实例 SetActive(true) 复用。
    /// 关闭请用 Close / SetActive(false)，不要 Destroy，否则这里检测不到、下次会再新建一个。
    /// 实例被外部 Destroy 或场景卸载后（UnityObject 的假 null）会自动重建，不需要手动处理。
    /// </summary>
    public static GameObject CreateUIOnce(string prefabName, Transform parent)
    {
        string key = BuildPath(prefabName);
        if (string.IsNullOrEmpty(key)) return null;

        GameObject instance;
        if (instances.TryGetValue(key, out instance) && instance != null)
        {
            MoveToParent(instance, parent);
            ResetTransform(instance.transform);
            instance.SetActive(true);
            return instance;
        }

        instance = CreateUI(prefabName, parent);
        if (instance != null) instances[key] = instance;
        return instance;
    }

    /// <summary>同上，打开后直接返回实例上的组件</summary>
    public static T CreateUIOnce<T>(string prefabName, Transform parent) where T : Component
    {
        GameObject instance = CreateUIOnce(prefabName, parent);
        return instance != null ? GetComponent<T>(instance) : null;
    }

    /// <summary>关闭弹窗（等价于 SetActive(false)），下次用 CreateUIOnce 打开会复用同一个实例</summary>
    public static void Close(string prefabName)
    {
        string key = BuildPath(prefabName);
        if (string.IsNullOrEmpty(key)) return;

        GameObject instance;
        if (instances.TryGetValue(key, out instance) && instance != null) instance.SetActive(false);
    }

    /// <summary>关闭指定实例（等价于 SetActive(false)）</summary>
    public static void Close(GameObject instance)
    {
        if (instance != null) instance.SetActive(false);
    }

    /// <summary>取已打开过的实例（没有或已被销毁返回 null）</summary>
    public static GameObject Get(string prefabName)
    {
        string key = BuildPath(prefabName);
        if (string.IsNullOrEmpty(key)) return null;

        GameObject instance;
        instances.TryGetValue(key, out instance);
        return instance != null ? instance : null;
    }

    /// <summary>弹窗当前是否显示中</summary>
    public static bool IsOpen(string prefabName)
    {
        GameObject instance = Get(prefabName);
        return instance != null && instance.activeSelf;
    }

    /// <summary>确实要销毁时才调用：销毁实例并从复用表里移除，下次 CreateUIOnce 会重新创建</summary>
    public static void DestroyUI(string prefabName)
    {
        string key = BuildPath(prefabName);
        if (string.IsNullOrEmpty(key)) return;

        GameObject instance;
        if (instances.TryGetValue(key, out instance))
        {
            instances.Remove(key);
            if (instance != null) UnityEngine.Object.Destroy(instance);
        }
    }

    /// <summary>清空弹窗复用表；destroy=true 时连实例一起销毁</summary>
    public static void ClearInstances(bool destroy = false)
    {
        if (destroy)
        {
            List<GameObject> all = new List<GameObject>(instances.Values);
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null) UnityEngine.Object.Destroy(all[i]);
            }
        }

        instances.Clear();
    }

    // ==================== 异步 ====================

    /// <summary>异步加载 + 实例化，回调在主线程，实例可能为 null（预制体不存在时）</summary>
    public static void CreateAsync(string prefabName, Transform parent, Action<GameObject> onDone)
    {
        CommonMonoUtils.Instance.StartCoroutine(CoCreateAsync(prefabName, parent, onDone));
    }

    /// <summary>异步加载 + 实例化，回调里直接拿到组件</summary>
    public static void CreateAsync<T>(string prefabName, Transform parent, Action<T> onDone) where T : Component
    {
        CreateAsync(prefabName, parent, instance =>
        {
            onDone?.Invoke(instance != null ? GetComponent<T>(instance) : null);
        });
    }

    /// <summary>异步加载 + 实例化并归零 UI 变换</summary>
    public static void CreateUIAsync(string prefabName, Transform parent, Action<GameObject> onDone)
    {
        CreateAsync(prefabName, parent, instance =>
        {
            if (instance != null) ResetTransform(instance.transform);
            onDone?.Invoke(instance);
        });
    }

    /// <summary>异步加载 + 实例化并归零 UI 变换，回调里直接拿到组件</summary>
    public static void CreateUIAsync<T>(string prefabName, Transform parent, Action<T> onDone) where T : Component
    {
        CreateUIAsync(prefabName, parent, instance =>
        {
            onDone?.Invoke(instance != null ? GetComponent<T>(instance) : null);
        });
    }

    // ==================== 缓存管理 ====================

    /// <summary>
    /// 丢掉单个预制体的缓存引用（只是不再复用，资源本身还在内存里）。
    /// 注意：Unity 的 Resources.UnloadAsset 不支持 GameObject 预制体，
    /// 真要释放内存请调用 ClearCache(true)，由 Resources.UnloadUnusedAssets 统一回收。
    /// </summary>
    public static void Release(string prefabName)
    {
        string path = BuildPath(prefabName);
        if (string.IsNullOrEmpty(path)) return;

        cache.Remove(path);
    }

    /// <summary>清空全部预制体缓存；unloadUnusedAssets=true 时调用 Resources.UnloadUnusedAssets 释放内存</summary>
    public static void ClearCache(bool unloadUnusedAssets = false)
    {
        cache.Clear();
        waiters.Clear();
        loading.Clear();

        if (unloadUnusedAssets) Resources.UnloadUnusedAssets();
    }

    // ==================== 内部实现 ====================

    /// <summary>把「预制体名字」拼成 Resources 相对路径，已经带 Prefab/ 前缀或传空都能容错</summary>
    private static string BuildPath(string prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
        {
            DebugMgr.Instance.LogError($"{LogTag} 预制体名字为空");
            return "";
        }

        string name = prefabName.Trim();
        if (name.StartsWith(RootPath + "/", StringComparison.Ordinal)) return name;

        // 名字带后缀 / 反斜杠这类常见误传，直接提示，避免查半天路径拼错
        if (name.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            DebugMgr.Instance.LogWarning($"{LogTag} 不用传 .prefab 后缀，已自动去掉: {name}");
            name = name.Substring(0, name.Length - ".prefab".Length);
        }

        return RootPath + "/" + name.Replace("\\", "/");
    }

    private static GameObject InstantiateCore(GameObject prefab, Transform parent)
    {
        GameObject instance = parent != null
            ? UnityEngine.Object.Instantiate(prefab, parent, false)
            : UnityEngine.Object.Instantiate(prefab);

        instance.name = prefab.name; // 去掉 Unity 自动加的 "(Clone)"
        return instance;
    }

    private static void MoveToParent(GameObject instance, Transform parent)
    {
        if (parent != null && instance.transform.parent != parent)
        {
            instance.transform.SetParent(parent, false);
        }
    }

    private static void ResetTransform(Transform target)
    {
        target.localPosition = Vector3.zero;
        target.localRotation = Quaternion.identity;
        target.localScale = Vector3.one;

        RectTransform rect = target as RectTransform;
        if (rect != null)
        {
            rect.anchoredPosition = Vector2.zero;
            rect.SetAsLastSibling(); // UI 里保证渲染在最上层
        }
    }

    private static T GetComponent<T>(GameObject go) where T : Component
    {
        T component = go.GetComponent<T>();
        if (component == null)
        {
            DebugMgr.Instance.LogError($"{LogTag} 预制体 {go.name} 上没有 {typeof(T).Name} 组件");
        }

        return component;
    }

    private static IEnumerator CoCreateAsync(string prefabName, Transform parent, Action<GameObject> onDone)
    {
        string path = BuildPath(prefabName);
        if (string.IsNullOrEmpty(path))
        {
            onDone?.Invoke(null);
            yield break;
        }

        GameObject prefab;
        if (cache.TryGetValue(path, out prefab) && prefab != null)
        {
            onDone?.Invoke(InstantiateCore(prefab, parent));
            yield break;
        }

        // 同一预制体正在加载：挂到等待队列里，加载完一起回调，避免重复 LoadAsync
        if (loading.Contains(path))
        {
            AddWaiter(path, prefab => onDone?.Invoke(prefab != null ? InstantiateCore(prefab, parent) : null));
            yield break;
        }

        loading.Add(path);
        ResourceRequest request = Resources.LoadAsync<GameObject>(path);
        yield return request;

        prefab = request.asset as GameObject;
        if (prefab == null)
        {
            DebugMgr.Instance.LogError($"{LogTag} 找不到预制体: Resources/{path}.prefab");
            loading.Remove(path);
            FireWaiters(path, null);
            onDone?.Invoke(null);
            yield break;
        }

        cache[path] = prefab;
        loading.Remove(path);

        GameObject instance = InstantiateCore(prefab, parent);
        FireWaiters(path, prefab);
        onDone?.Invoke(instance);
    }

    private static void AddWaiter(string path, Action<GameObject> waiter)
    {
        List<Action<GameObject>> list;
        if (!waiters.TryGetValue(path, out list))
        {
            list = new List<Action<GameObject>>();
            waiters[path] = list;
        }

        list.Add(waiter);
    }

    private static void FireWaiters(string path, GameObject prefab)
    {
        List<Action<GameObject>> list;
        if (!waiters.TryGetValue(path, out list)) return;

        waiters.Remove(path);
        for (int i = 0; i < list.Count; i++) list[i]?.Invoke(prefab);
    }
}
