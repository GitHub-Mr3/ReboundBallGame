using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 场景中的「旋转转盘组」容器。
/// WallGroup 列表在场景里已经配好（0=Wall_1 ... 4=Wall_5），
/// 具体每关启用哪几个、转多快、转到什么初始角度，全部由 LevelManager 按关卡表布置，
/// 不再在进入点位时随机乱刷。
/// </summary>
public class RandomWallGroup : MonoBehaviour
{
    public List<AutoRotate> WallGroup;

    private void Start()
    {
        // 场景开始：布置第 1 关
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.ApplyCurrentLevel();
        }
    }
}
