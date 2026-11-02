using TMPro;
using UnityEngine;

// 씬 원문은 보존하고 튜토리얼로 지정한 라벨에만 대본을 연결합니다.
[DefaultExecutionOrder(32000), DisallowMultipleComponent]
public sealed class JcTutorialTextBinding : MonoBehaviour
{
    string id, original, applied;
    TMP_Text label;
    public string SourceText => original;
    public string ResolvedText => JcTutorialText.Get(id, original);
    public void Initialize(string key, TMP_Text target)
    {
        id = key; label = target; original = target.text; Apply();
    }
    void OnEnable() => Apply();
    void LateUpdate() => Apply();
    public void Apply()
    {
        if (label == null || id == null) return;
        // 스킬 안내 등 같은 라벨의 일시적인 동적 내용은 해당 표시 코드가 담당합니다.
        if (label.text != original && label.text != applied) return;
        var value = JcTutorialText.Get(id, original);
        if (label.text != value) label.text = value;
        applied = value;
    }
}
