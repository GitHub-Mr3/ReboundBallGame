using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommonUtil : SingletonBase<CommonUtil>
{
    private Transform uiRoot;

    public Transform GetUIRoot()
    {
        if (uiRoot == null)
        {
            GameObject rootObj = GameObject.Find("GameMain");

            // 游戏场景里没有 GameMain，退回场景中的 Canvas，避免在那边弹提示时直接空引用
            if (rootObj == null)
            {
                // CommonUtil 不是 MonoBehaviour（继承自 SingletonBase），必须写全 UnityEngine.Object 才能调到这个静态方法
                Canvas canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
                rootObj = canvas != null ? canvas.gameObject : null;
            }

            if (rootObj != null) uiRoot = rootObj.transform;
        }
        return uiRoot;
    }

    /// <summary>弹出提示：文案交给 CommonTips，它会自动上飘并关闭</summary>
    public void ShowTips(string str)
    {
        Transform root = GetUIRoot();
        if (root == null)
        {
            DebugMgr.Instance.LogWarning("[CommonUtil] 找不到 UI 根节点，提示无法显示：" + str);
            return;
        }

        CommonTips uiRes = ResLoader.CreateUIOnce<CommonTips>(GameData.CommonTips, root);
        if (uiRes != null) uiRes.Refresh(str);
    }
}