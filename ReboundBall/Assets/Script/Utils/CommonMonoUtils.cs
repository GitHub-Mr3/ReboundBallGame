using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CommonMonoUtils : SingleMonoBase<CommonMonoUtils>
{
    public void SetPopEffect(Transform panelRt)
    {
        StartCoroutine(PopIn(panelRt));
    }

    IEnumerator PopIn(Transform panelRt)
    {
        const float dur = 0.18f;
        for (float a = 0; a < dur; a += Time.deltaTime)
        {
            if (panelRt == null) yield break;
            float k = Mathf.Clamp01(a / dur);
            float s = Mathf.Lerp(0.86f, 1f, k) + 0.05f * Mathf.Sin(k * Mathf.PI); // ÇáÎ¢»Øµ¯
            panelRt.localScale = new UnityEngine.Vector3(s, s, 1f);
            yield return null;
        }

        if (panelRt != null) panelRt.localScale = UnityEngine.Vector3.one;
    }

}
