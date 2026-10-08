using System;
using System.Collections.Generic;

public class RedPointNode
{
    public string Name;
    public int PassCount;
    public int EndCount;
    public int RedPointCount;
    public Dictionary<string, RedPointNode> Children;
    public Dictionary<string, Action<string, int>> UpdateCB;

    public RedPointNode(string name)
    {
        Name = name;
        PassCount = 0;
        EndCount = 0;
        RedPointCount = 0;
        Children = new Dictionary<string, RedPointNode>();
        UpdateCB = new Dictionary<string, Action<string, int>>();
    }
}
