using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData
{
    /// <summary>本地存储键：历史最大通关关卡 id（图鉴解锁进度按它来算）</summary>
    public const string MaxLevelIdKey = "maxLevelId";
    public const string AudioVolumData = "AudioVolumData";
    public const int MaxLevelNum = 12;


    //事件

    public const string EventID_HandBookInfo = "EventID_HandBookInfo";
    public const string EventID_LeveLock = "EventID_LeveLock";
    
    //数据类型
    public struct HandBookInfoData
    {
        public Sprite icon;
        public string name;
        public string desc;
    }

    public static string GetLevelName(int index)
    {
        if (index < 0)
            index = 0;
        if (index >= LevelName.Count)
        {
            return "流浪汪";
        }

        return LevelName[index];
    }

    public static string GetLevelSortName(int index)
    {
        var levelName = GetLevelName(index);
        var names = levelName.Split('.');
        if (names.Length > 1)
        {
            return $"<color=#145200><b> {names[names.Length - 1]}</b></color>";
        }
        return $"<color=#145200><b> {levelName}</b></color>";
    }
    private static List<string> LevelName = new List<string>()
    {
        "柴犬.小焰",
        "博美.雪球",
        "金毛.暖阳",
        "哈士奇.星夜",
        "柯基.青芜",
        "泰迪.桃桃",
        "杰克罗素.小黄巾",
        "罗威纳.赤岩",
        "马尔济斯.紫缨",
        "雪纳瑞.灰砾",
        "边牧.彩斑",
        "松狮.圆墩",
    };
}