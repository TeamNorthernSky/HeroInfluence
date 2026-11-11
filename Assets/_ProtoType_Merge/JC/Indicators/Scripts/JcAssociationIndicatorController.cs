using System;
using UnityEngine;

// JC | 2026-11-10 | JC_Indicator 하위의 조절기. 임시 초안과 저장 프로필을 분리하여 플레이 종료 시 원본 덮어쓰기를 막습니다.
[DisallowMultipleComponent]
public sealed class JcAssociationIndicatorController : MonoBehaviour
{
    [SerializeField] private JcAssociationIndicatorProfile profile;
    [NonSerialized] private JcAssociationIndicatorProfile draft;
    public JcAssociationIndicatorProfile Source => profile;
    public JcAssociationIndicatorProfile Draft
    {
        get
        {
            if (!draft && profile)
            {
                draft = Instantiate(profile);
                draft.name = profile.name + " (임시 조절값)";
                // JC | 2026-11-10 | HideAndDontSave의 NotEditable은 PropertyField까지 잠그므로 제거합니다.
                // 초안의 숨김/저장 제외/명시적 수명 관리는 유지하고 인스펙터 편집만 허용합니다.
                draft.hideFlags = HideFlags.HideAndDontSave & ~HideFlags.NotEditable;
            }
            // JC | 2026-11-10 | 이미 생성된 초안도 값을 버리지 않고 편집 금지 플래그만 해제합니다.
            if (draft && (draft.hideFlags & HideFlags.NotEditable) != 0)
                draft.hideFlags &= ~HideFlags.NotEditable;
            return draft;
        }
    }
    public void ReloadProfile()
    {
        if (!profile) return;
        if (draft) JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(profile), draft);
        else { var initialized = Draft; }
        if (draft) draft.Sanitize();
    }
    private void OnDestroy()
    {
        if (!draft) return;
        if (Application.isPlaying) Destroy(draft);
        else DestroyImmediate(draft);
    }
}
