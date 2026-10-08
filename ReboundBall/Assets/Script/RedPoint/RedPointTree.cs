using System;
using UnityEngine;

public class RedPointTree : SingletonBase<RedPointTree>
{
    RedPointNode Root;
    public RedPointTree()
    {
        Root = new RedPointNode("Root");
    }

    /// <summary>
    /// 增加节点
    /// </summary>
    /// <param name="name"></param>
    public void InsertNode(string name)
    {
        if (string.IsNullOrEmpty(name))
            return;

        if (SearchNode(name) != null)
        {
            Debug.Log("你已经插入了此节点" + name);
            return;
        }

        var node = Root;
        node.PassCount += 1;

        string[] pathList = name.Split('|');

        for (int i = 0; i < pathList.Length; i++)
        {
            string path = pathList[i];
            if (!node.Children.ContainsKey(path))
            {
                node.Children.Add(path, new RedPointNode(path));
            }
            node = node.Children[path];
            node.PassCount += 1;
        }

        node.EndCount += 1;
    }

    /// <summary>
    /// 查找节点
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public RedPointNode SearchNode(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var node = Root;
        string[] pathList = name.Split('|');
        for (int i = 0; i < pathList.Length; i++)
        {
            string path = pathList[i];
            if (!node.Children.ContainsKey(path))
            {
                return null;
            }
            node = node.Children[path];
        }

        if (node.EndCount > 0)
        {
            return node;
        }
        return null;
    }

    /// <summary>
    /// 删除节点
    /// </summary>
    /// <param name="name"></param>
    public void DeleteNode(string name)
    {
        if (SearchNode(name) == null)
        {
            return;
        }

        RedPointNode node = Root;
        node.PassCount -= 1;
        string[] pathList = name.Split('|');
        for (int i = 0; i < pathList.Length; i++)
        {
            string path = pathList[i];
            RedPointNode childNode = node.Children[path];
            childNode.PassCount -= 1;
            if (childNode.PassCount == 0)
            {
                node.Children.Remove(path);
            }
            node = childNode;
        }
        node.EndCount -= 1;
    }

    /// <summary>
    /// 修改红点数据
    /// </summary>
    /// <param name="name"></param>
    /// <param name="delta"></param>
    public void ChangeRedPointCount(string name, int delta)
    {
        RedPointNode targetNode = SearchNode(name);
        if (targetNode == null)
        {
            return;
        }

        if (delta < 0 && targetNode.RedPointCount + delta < 0)
        {
            delta = -targetNode.RedPointCount;
        }

        RedPointNode node = Root;
        string[] pathList = name.Split('|');

        for (int i = 0; i < pathList.Length; i++)
        {
            string path = pathList[i];
            RedPointNode childNode = node.Children[path];
            childNode.RedPointCount += delta;
            node = childNode;
            foreach (Action<string, int> action in childNode.UpdateCB.Values)
            {
                action(node.Name, node.RedPointCount);
            }
        }
    }

    /// <summary>
    /// 直接设置红点数量
    /// </summary>
    /// <param name="name"></param>
    /// <param name="count"></param>
    public void SetRedPointCount(string name, int count)
    {
        RedPointNode targetNode = SearchNode(name);
        if (targetNode == null)
        {
            return;
        }

        // 确保红点数量不为负数
        if (count < 0)
        {
            count = 0;
        }

        // 设置目标节点的红点数量
        int lastCount = targetNode.RedPointCount;
        targetNode.RedPointCount = count;
        var delta = count - lastCount;

        RedPointNode node = Root;
        string[] pathList = name.Split('|');

        for (int i = 0; i < pathList.Length; i++)
        {
            string path = pathList[i];
            RedPointNode childNode = node.Children[path];
            if (childNode.Name != targetNode.Name)
            {
                childNode.RedPointCount += delta;
            }

            node = childNode;
            foreach (Action<string, int> action in childNode.UpdateCB.Values)
            {
                action(node.Name, node.RedPointCount);
            }
        }
    }

    /// <summary>
    /// 设置修改回调
    /// </summary>
    /// <param name="name"></param>
    /// <param name="cb"></param>
    public void SetCallBack(string name, Action<string, int> cb)
    {
        RedPointNode node = SearchNode(name);
        if (node == null)
        {
            return;
        }
        node.UpdateCB.Add(name, cb);
    }

    /// <summary>
    /// 获取红点数
    /// </summary>
    /// <param name="name"></param>
    /// <returns></returns>
    public int GetRedPointCount(string name)
    {
        RedPointNode node = SearchNode(name);
        if (node == null)
        {
            return 0;
        }

        return node.RedPointCount;
    }
}


