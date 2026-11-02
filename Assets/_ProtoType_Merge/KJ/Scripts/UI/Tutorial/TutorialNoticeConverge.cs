using UnityEngine;
using JC.Tutorial;

/// <summary>제한 안내가 나타날 때 한 번만 약하게 수렴하는 강조.</summary>
public sealed class TutorialNoticeConverge : MonoBehaviour
{
    [SerializeField, Tooltip("안내 패널 바깥에서 수렴할 노란색 글로우입니다. 입력을 가로채지 않습니다.")]
    private JcTutorialGraphic glow;
    private float elapsed;
    private void OnEnable() { elapsed=0; Render(0); }
    private void LateUpdate() { elapsed+=Time.unscaledDeltaTime; Render(elapsed); }
    public void Render(float time)
    {
        if(glow==null)return;
        float t=Mathf.Clamp01(time/.85f);
        glow.gameObject.SetActive(t<1);
        glow.rectTransform.sizeDelta=Vector2.one*Mathf.Lerp(30,4,Mathf.SmoothStep(0,1,t));
        glow.color=new Color(JcTutorialGraphic.MessageAccent.r,JcTutorialGraphic.MessageAccent.g,JcTutorialGraphic.MessageAccent.b,.28f*Mathf.Sin(t*Mathf.PI));
        glow.AnimateSweep(time);
    }
}
