using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通用对象池 - 纯代码单例，无需挂载
/// 用法：
///   var obj = PoolManager.Instance.Spawn(prefab, position, parent);
///   PoolManager.Instance.Despawn(obj);
/// </summary>
public class PoolMgr:SingletonBase<PoolMgr>
{


    private GameObject rootObject;
    private Dictionary<GameObject, Stack<GameObject>> poolDict
        = new Dictionary<GameObject, Stack<GameObject>>();

    // 记录每个实例对应的源 prefab（用于回收）
    private Dictionary<GameObject, GameObject> instanceToPrefab
        = new Dictionary<GameObject, GameObject>();

    private PoolMgr()
    {
        rootObject = new GameObject("[PoolManager]");
        Object.DontDestroyOnLoad(rootObject);
    }

    /// <summary>
    /// 从池中取出对象
    /// </summary>
    public GameObject Spawn(GameObject prefab, Vector3 position = default, Transform parent = null)
    {
        if (prefab == null)
        {
            Debug.LogError("[PoolManager] prefab 为 null");
            return null;
        }

        if (!poolDict.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            poolDict[prefab] = stack;
        }

        GameObject obj;
        if (stack.Count > 0)
        {
            obj = stack.Pop();
            obj.transform.SetParent(parent, false);
            obj.transform.position = position;
            obj.SetActive(true);
        }
        else
        {
            obj = Object.Instantiate(prefab, position, Quaternion.identity, parent);
            instanceToPrefab[obj] = prefab;
        }

        return obj;
    }

    /// <summary>
    /// 回收对象到池
    /// </summary>
    public void Despawn(GameObject obj)
    {
        if (obj == null) return;

        if (!instanceToPrefab.TryGetValue(obj, out var prefab))
        {
            // 不是池里创建的，直接销毁
            Object.Destroy(obj);
            return;
        }

        obj.SetActive(false);
        obj.transform.SetParent(rootObject.transform, false);

        if (!poolDict.TryGetValue(prefab, out var stack))
        {
            stack = new Stack<GameObject>();
            poolDict[prefab] = stack;
        }
        stack.Push(obj);
    }

    /// <summary>
    /// 清空池（切场景时可选调用）
    /// </summary>
    public void ClearAll()
    {
        poolDict.Clear();
        instanceToPrefab.Clear();
    }
}