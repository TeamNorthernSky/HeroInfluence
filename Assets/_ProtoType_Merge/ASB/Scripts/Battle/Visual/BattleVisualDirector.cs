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

    /// <summary>투사체 등 연출 실행부가 skillIndex로 프리젠테이션 데이터를 직접 조회할 때 사용합니다.</summary>
    public SkillPresentationData GetPresentation(int skillIndex) => _catalog?.Get(skillIndex);

    public void PlayAttackEffect(BattleCharactor actor, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation?.AttackEffectPrefab == null)
        {
            return;
        }

        var profile = actor?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.AttackEffectSocket ?? actor?.transform;
        if (socket == null)
        {
            return;
        }

        Instantiate(presentation.AttackEffectPrefab, socket.position, socket.rotation);
    }

    public void PlayHitEffect(BattleCharactor target, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation?.HitEffectPrefab == null)
        {
            return;
        }

        var profile = target?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.HitEffectSocket ?? target?.transform;
        if (socket == null)
        {
            return;
        }

        Instantiate(presentation.HitEffectPrefab, socket.position, socket.rotation);
    }

    public void PlayAttackSfx(BattleCharactor actor, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation?.AttackSfxClip == null)
        {
            return;
        }

        var profile = actor?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.AttackEffectSocket ?? actor?.transform;
        if (socket == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(presentation.AttackSfxClip, socket.position, presentation.SfxVolume);
    }

    public void PlayHitSfx(BattleCharactor target, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation?.HitSfxClip == null)
        {
            return;
        }

        var profile = target?.GetComponent<UnitVisualProfile>();
        Transform socket = profile?.HitEffectSocket ?? target?.transform;
        if (socket == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(presentation.HitSfxClip, socket.position, presentation.SfxVolume);
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
