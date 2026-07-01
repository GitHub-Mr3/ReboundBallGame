using System;
using System.Collections;
using System.Collections.Generic;
using Mr3;
using UnityEngine;
using Random = UnityEngine.Random;

public class RandomWallGroup : MonoBehaviour
{
    public List<AutoRotate> WallGroup;

    private void Start()
    {
        InitRandom();
        AddEvent();
    }

    private void OnDestroy()
    {
        OnRemoveEvent();
    }

    void AddEvent()
    {
        EventMgr.Instance.AddListener("OnRandomWall", OnRandomWall);
        EventMgr.Instance.AddListener("RestartGame",OnRestartGame);
    }

    void OnRemoveEvent()
    {
        EventMgr.Instance.RemoveListener("OnRandomWall", OnRandomWall);
        EventMgr.Instance.RemoveListener("RestartGame",OnRestartGame);
        
    }

    void OnRestartGame(string str, object obj)
    {
        InitRandom();
    }

    void OnRandomWall(string str, object obj)
    {
        bool isWin = (bool)obj;
        if (isWin)
        {
            RandomShow();
        }
      
        
    }

    void InitRandom()
    {
        for (int i = 0; i < WallGroup.Count; i++)
        {
            WallGroup[i].gameObject.SetActive(false);
        }

        WallGroup[0].gameObject.SetActive(true);
    }

    void RandomShow()
    {
        int rangeNum = Random.Range(1, 6);
        var a = GetRandomValues(WallGroup, rangeNum);

        for (int i = 0; i < WallGroup.Count; i++)
        {
            WallGroup[i].gameObject.SetActive(false);
        }

        for (int i = 0; i < a.Count; i++)
        {
            a[i].gameObject.SetActive(true);
        }
    }

    public static List<T> GetRandomValues<T>(List<T> list, int count)
    {
        List<T> result = new List<T>();

        if (list == null || list.Count == 0)
        {
            return result;
        }

        if (count >= list.Count)
        {
            // 如果请求数量大于等于列表长度，返回所有元素
            return new List<T>(list);
        }

        // 复制列表并打乱
        List<T> tempList = new List<T>(list);
        for (int i = 0; i < count; i++)
        {
            int randomIndex = Random.Range(0, tempList.Count);
            result.Add(tempList[randomIndex]);
            tempList.RemoveAt(randomIndex);
        }

        return result;
    }
}