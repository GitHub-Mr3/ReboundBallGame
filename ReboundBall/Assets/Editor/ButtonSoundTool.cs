#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;

public class ButtonSoundTool
{
    [MenuItem("Tools/给选中按钮添加点击音效")]
    private static void AddSoundToSelected()
    {
        var selected = Selection.gameObjects;
        int count = 0;

        foreach (var go in selected)
        {
            var btn = go.GetComponent<Button>();
            if (btn == null) continue;
            AttachSound(btn);
            count++;
        }

        Debug.Log($"[ButtonSoundTool] 已为 {count} 个按钮添加音效");
    }

    [MenuItem("Tools/给场景中所有按钮添加点击音效")]
    private static void AddSoundToAll()
    {
        // 场景中所有按钮，含未激活
        Button[] buttons = Object.FindObjectsOfType<Button>(true);
        int count = 0;
        foreach (var btn in buttons)
        {
            AttachSound(btn);
            count++;
        }

        Debug.Log($"[ButtonSoundTool] 已为 {count} 个按钮添加音效");
    }

    private static void AttachSound(Button btn)
    {
        // 避免重复
        if (btn.GetComponent<ButtonSoundHelper>() != null) return;
        btn.gameObject.AddComponent<ButtonSoundHelper>();
        // 标记场景已修改
        EditorUtility.SetDirty(btn.gameObject);
    }
}
#endif