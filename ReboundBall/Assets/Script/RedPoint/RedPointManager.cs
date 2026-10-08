using System.Collections.Generic;
using Mr3;
using UnityEngine;
using UnityEngine.UI;

public class RedPointManager : SingletonBase<RedPointManager>
{

    RedPointTree redTree;
    //保存节点名称和对应的路径值
    Dictionary<string, string> nodeNames = new Dictionary<string, string>();
    public RedPointManager()
    {
        redTree = RedPointTree.Instance;
        CreateTree();
        RegisterRedPintCallBack();
    }
    void RegisterRedPintCallBack()
    {
        for (int i = 0; i < NodeNames.NodeList.Count; i++)
        {
            SetRedPointChangeCallBack(NodeNames.NodeList[i]);
        }
    }

    private void CreateTree()
    {
        for (int i = 0; i < NodeNames.NodeList.Count; i++)
        {
            redTree.InsertNode(NodeNames.NodeList[i]);
            var nodelistName = NodeNames.NodeList[i].Split('|');
            var key = nodelistName[nodelistName.Length - 1];
            nodeNames[key] = NodeNames.NodeList[i];
        }
    }

    public string GetNodePath(string name)
    {
        if (nodeNames.TryGetValue(name, out string value))
        {
            return value;
        }
        return string.Empty;
    }
    /// <summary>
    /// 设置红点修改回调，只需要注册一次
    /// </summary>
    /// <param name="name"></param>
    public void SetRedPointChangeCallBack(string name)
    {
        redTree.SetCallBack(name, RedPointNumChange);
    }

    /// <summary>
    /// 红点数量改变
    /// </summary>
    /// <param name="name"></param>
    /// <param name="num"></param>
    public void ChangeRedPointNum(string name, int num)
    {
        redTree.ChangeRedPointCount(name, num);
    }
    /// <summary>
    /// 设置红点数量
    /// </summary>
    /// <param name="name"></param>
    /// <param name="count"></param>
    public void SetRedPointCount(string name, int count)
    {
        redTree.SetRedPointCount(name, count);
    }
    /// <summary>
    /// 获取红点数量
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public int GetRedPointCount(string name)
    {
        return redTree.GetRedPointCount(name);
    }
    /// <summary>
    /// 红点数量发生改变
    /// </summary>
    /// <param name="str"></param>
    /// <param name="redCount"></param>
    void RedPointNumChange(string str, int redCount)
    {

        if (!nodeNames.ContainsKey(str))
        {
            Debug.LogError($"节点数据错误 str:{str}   _redCount:{redCount} ");
            return;
        }
        //TODO 抛事件出去，自己系统的红点自己系统模块处理
        RedChangeData redChangeData = new RedChangeData();
        redChangeData.name = str;
        redChangeData.redCount = redCount;
        EventMgr.Instance.Emit(GameData.Event_RedPointChange, redChangeData);
       // EventCenter.Instance.EventTrigger<RedChangeData>("REDNPOINT_CHANGE", redChangeData);
    }

}
/// <summary>
/// 红点改变事件发送的数据
/// </summary>
struct RedChangeData
{
    public string name;
    public int redCount;
}

/// <summary>
///          请注意！！！！ 请注意！！！！请注意！！！！  
///  常量名称要与最后一个节点的名称一致，因为当红点统计之后抛出的事件是最后一个节点，参考下面的结构写红点的树形结构 
/// </summary>
public class NodeNames
{


    #region  签到
    public const string Signin = "Signin";

    #endregion
    #region 商店
    public const string Shop = "Shop";
    public const string FreeShop = "Shop|Free";

    #endregion


    public static List<string> NodeList = new List<string>() {
        Signin,Shop,FreeShop
    };

}
