using UnityEngine;

/// <summary>유닛 효과음 이벤트. 스킬과 무관하게 유닛 자체가 내는 소리만 둔다.</summary>
public enum UnitSfxEvent
{
    Summon,
    Death,
    GuardSwap
}

/// <summary>
/// 스킬과 무관한 유닛 효과음 키(DHAudioClipCatalog SfxClips 정식 키). 유닛 프리팹 루트(BattleCharactor와 같은 GameObject)에 붙인다.
/// 스킬 소리는 SkillPresentationData(Timeline SFX 마커·Event SFX)에 둔다.
/// </summary>
[DisallowMultipleComponent]
public class UnitSfxProfile : MonoBehaviour
{
    [Tooltip("소환·등장 시 재생할 효과음 키.")]
    public string SummonSfxKey;
    [Tooltip("사망·파괴 시 재생할 효과음 키.")]
    public string DeathSfxKey;
    [Tooltip("대신 맞기 자리 교환 이동 시작 시 재생할 효과음 키(가디언 쪽에 입력).")]
    public string GuardSwapSfxKey;

    public string GetKey(UnitSfxEvent evt)
    {
        switch (evt)
        {
            case UnitSfxEvent.Summon: return SummonSfxKey;
            case UnitSfxEvent.Death: return DeathSfxKey;
            case UnitSfxEvent.GuardSwap: return GuardSwapSfxKey;
            default: return null;
        }
    }

    /// <summary>유닛의 UnitSfxProfile에서 이벤트 키를 찾는다. 프로필이나 키가 없으면 빈 문자열.</summary>
    public static string ResolveKey(Component unit, UnitSfxEvent evt)
    {
        if (unit == null) return string.Empty;
        UnitSfxProfile profile = unit.GetComponent<UnitSfxProfile>();
        return profile != null ? DHAudioClipCatalog.NormalizeKey(profile.GetKey(evt)) : string.Empty;
    }

    /// <summary>유닛의 이벤트 키를 재생한다. 프로필이나 키가 없으면 무음.</summary>
    public static void Play(Component unit, UnitSfxEvent evt)
    {
        PlayKey(ResolveKey(unit, evt));
    }

    /// <summary>미리 해석해 둔 키를 재생한다(유닛이 파괴된 뒤에도 쓸 수 있게). 빈 키는 무음.</summary>
    public static void PlayKey(string key)
    {
        if (!string.IsNullOrEmpty(key)) PresentationSfxPlayer.Play(key);
    }
}
