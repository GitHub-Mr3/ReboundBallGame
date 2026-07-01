using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 继承MonoBehaviour的单例模式的基类
/// 作用：继承了这个类的类就继承了MonoBehaviour，并且自带单例模式
/// </summary>
public class SingleMonoBase<T> : MonoBehaviour where T : MonoBehaviour
{
    //构造方法私有化，防止外部new对象
    protected SingleMonoBase() { }
    //记录单例对象是否存在 ，用于防止 在OnDestroy中访问单例对象报错
    public static bool IsExisted { get; private set; } = false;

    private static T instance;
    public static T Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<T>();
                if (instance == null)
                {
                    GameObject obj = new GameObject(typeof(T).Name);
                    instance = obj.AddComponent<T>();
                    DontDestroyOnLoad(obj);
                    IsExisted = true;
                }
            }
            return instance;
        }
    }
    protected virtual void OnDestroy()
    {
        IsExisted = false;
    }
}
