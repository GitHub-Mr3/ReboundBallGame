using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮音效辅助组件，由编辑器工具自动挂载
/// </summary>
[RequireComponent(typeof(Button))]
public class ButtonSoundHelper : MonoBehaviour
{

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() =>
            AudioMgr.Instance.PlaySFX(GameData.UiClickSFX));
    }
}