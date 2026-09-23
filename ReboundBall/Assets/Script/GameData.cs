using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameData
{
    public const string MaxScoreKey = "maxScore";
    public const string AudioVolumData = "AudioVolumData";
    
    
    //事件
    public const string EventID_HandBookInfo = "EventID_HandBookInfo";
    
    //数据类型
    public struct  HandBookInfoData
    {
        public Sprite icon;
        public string name;
        public string desc;
    }
}