using ASB.Work.Battle.Core;
using UnityEngine;

/// <summary>
/// 전투 연출(이펙트·팝업) 생성 책임.
/// BattleManager의 [SerializeField] 필드로 주입합니다.
/// </summary>
public class BattleVisualDirector : MonoBehaviour
{
    [SerializeField] private SkillPresentationCatalog _catalog;
    [SerializeField] private DamagePopupPresenter _popupPresenter;
    [SerializeField] private EffectRegistry _effectRegistry;

    public SkillPresentationData GetPresentation(int skillIndex)
    {
        return _catalog != null ? _catalog.Get(skillIndex) : null;
    }

    public void PlayAttackEffect(BattleCharactor actor, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
        {
            return;
        }

        // Schema=1(PhaseCue)은 시전자 이펙트/사운드를 UnitEffectPresenter/Cue가 담당 →
        // director는 스폰하지 않는다(이중 스폰 방지). Schema=0만 이 레거시 경로 사용.
        if (presentation.IsPhaseCue)
        {
            return;
        }

        var profile = actor?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.AttackEffectSocket ?? actor?.transform;

        if (presentation.EnableAttackEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.AttackEffectId, presentation.AttackEffectPrefab);
            if (prefab != null && socket != null)
            {
                Instantiate(prefab, socket.position, socket.rotation);
            }
        }

        PlaySound(presentation.AttackSoundId, presentation.AttackSfxClip, socket, presentation.SfxVolume);
    }

    public void PlayHitEffect(BattleCharactor target, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
        {
            return;
        }

        var profile = target?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.HitEffectSocket ?? target?.transform;

        if (presentation.EnableHitEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.HitEffectId, presentation.HitEffectPrefab);
            // 재료 프리팹은 프리젠터/이벤트가 담당 → director는 스폰하지 않음.
            if (prefab != null && prefab.GetComponent<ISkillEffectBehaviour>() == null && socket != null)
            {
                Instantiate(prefab, socket.position, socket.rotation);
            }
        }

        PlaySound(presentation.HitSoundId, presentation.HitSfxClip, socket, presentation.SfxVolume);
    }

    /// <summary>EffectRegistry에서 id로 프리팹을 조회합니다(시퀀서의 재료 판별용). 0이면 null.</summary>
    public GameObject GetRegisteredEffect(int id) => id != 0 ? _effectRegistry?.Get(id) : null;

    // 레지스트리 id 우선, 없으면 레거시 프리팹 폴백 (마이그레이션 브리지).
    private GameObject ResolveEffectPrefab(int effectId, GameObject legacyPrefab)
    {
        GameObject fromRegistry = effectId != 0 ? _effectRegistry?.Get(effectId) : null;
        return fromRegistry != null ? fromRegistry : legacyPrefab;
    }

    // SoundRegistry id 우선, 없으면 레거시 AudioClip 폴백. 전투 로직은 건드리지 않음.
    private void PlaySound(int soundId, AudioClip legacyClip, Transform at, float volume)
    {
        if (SoundManager.Instance == null)
        {
            return;
        }

        Vector3 pos = at != null ? at.position : Vector3.zero;
        if (soundId != 0)
        {
            SoundManager.Instance.PlayById(soundId, pos, volume);
            return;
        }

        if (legacyClip != null)
        {
            SoundManager.Instance.PlayClip(legacyClip, pos, volume);
        }
    }

    public void ShowDamagePopup(BattleHitResult result)
    {
        if (result == null || result.Target == null)
        {
            return;
        }

        _popupPresenter?.Show(result);
    }
}
