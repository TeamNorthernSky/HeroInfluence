using System.Collections;
using UnityEngine;

/// <summary>유닛 효과음 이벤트. 스킬과 무관하게 유닛 자체가 내는 소리만 둔다.</summary>
public enum UnitSfxEvent
{
    Summon,
    Death,
    GuardSwap,
    GuardBlock,
    ImmuneHit
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
    [Tooltip("사망 효과음을 쓰러지기 시작한 순간보다 늦출 시간(전투 배속 기준 초). 0이면 바로 재생.")]
    [Min(0f)] public float DeathSfxDelaySeconds;
    [Tooltip("대신 맞기 자리 교환 이동 시작 시 재생할 효과음 키(가디언 쪽에 입력).")]
    public string GuardSwapSfxKey;
    [Tooltip("대신 맞기로 재지정된 피해를 받는 순간 재생할 효과음 키(가디언 쪽에 입력).")]
    public string GuardBlockSfxKey;
    [Tooltip("피해 무효 상태(예: 율리아, 증폭기 생존 중)에서 공격을 받는 순간 재생할 효과음 키.")]
    public string ImmuneHitSfxKey;

    public string GetKey(UnitSfxEvent evt)
    {
        switch (evt)
        {
            case UnitSfxEvent.Summon: return SummonSfxKey;
            case UnitSfxEvent.Death: return DeathSfxKey;
            case UnitSfxEvent.GuardSwap: return GuardSwapSfxKey;
            case UnitSfxEvent.GuardBlock: return GuardBlockSfxKey;
            case UnitSfxEvent.ImmuneHit: return ImmuneHitSfxKey;
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

    /// <summary>
    /// 사망 효과음을 DeathSfxDelaySeconds만큼 늦춰 재생한다. 프로필이나 키가 없으면 무음.
    /// BattleCharactor.Die가 자기 코루틴을 모두 멈추므로 대기는 이 컴포넌트가 맡는다.
    /// </summary>
    public static void PlayDeath(Component unit)
    {
        UnitSfxProfile profile = unit != null ? unit.GetComponent<UnitSfxProfile>() : null;
        if (profile == null) return;
        string key = DHAudioClipCatalog.NormalizeKey(profile.DeathSfxKey);
        if (string.IsNullOrEmpty(key)) return;
        if (profile.DeathSfxDelaySeconds <= 0f || !profile.isActiveAndEnabled)
        {
            PlayKey(key);
            return;
        }
        profile.StartCoroutine(PlayAfterBattleSeconds(key, profile.DeathSfxDelaySeconds));
    }

    private static IEnumerator PlayAfterBattleSeconds(string key, float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += Time.deltaTime * BattleRuntimeSettings.BattleSpeed;
            yield return null;
        }
        PlayKey(key);
    }
}
