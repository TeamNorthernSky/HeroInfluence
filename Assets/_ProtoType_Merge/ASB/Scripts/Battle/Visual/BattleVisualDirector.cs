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

    public void PlayHitEffectAt(Transform targetTransform, int skillIndex)
    {
        if (targetTransform == null)
            return;

        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
            return;

        if (presentation.EnableHitEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.HitEffectId);
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, targetTransform.position, targetTransform.rotation);
                instance.GetComponent<JC.VFX.VfxEffect>()?.Play();
            }
        }

        PlaySound(presentation.HitSoundId, targetTransform, presentation.SfxVolume);
    }

    public void PlayHitEffect(BattleCharactor target, int skillIndex)
    {
        SkillPresentationData presentation = _catalog?.Get(skillIndex);
        if (presentation == null)
        {
            return;
        }

        // 소켓 해석: HitEffectSocket이 지정돼 있으면 사용, 아니면 대상 루트로 폴백한다.
        // Unity Object는 파괴 시 "fake null"이라 C#의 ?? 연산자가 폴백하지 못하므로,
        // 반드시 Unity의 != null 오버로드로 판정한다(미지정/파괴 소켓 → 대상 루트).
        var profile = target != null ? target.GetComponent<UnitVisualProfile>() : null;
        Transform socket = profile != null && profile.HitEffectSocket != null
            ? profile.HitEffectSocket
            : (target != null ? target.transform : null);

        if (presentation.EnableHitEffect)
        {
            GameObject prefab = ResolveEffectPrefab(presentation.HitEffectId);
            // 재료 프리팹은 프리젠터/이벤트가 담당 → director는 스폰하지 않음.
            if (prefab != null && socket != null)
            {
                GameObject instance = Instantiate(prefab, socket.position, socket.rotation);

                // HealOrbitVfx는 parameterless Play()로는 현재 루트 좌표를 쓰므로,
                // 힐 대상의 월드 좌표를 명시해 발밑에서 시작시킨다. 이 호출은 프리팹의
                // OnEnable 자동 재생 여부와 무관하게 런타임 인스턴스를 확실히 구동한다.
                JC.VFX.HealOrbitVfx healAura = instance.GetComponent<JC.VFX.HealOrbitVfx>();
                if (healAura != null)
                {
                    healAura.Play(target.transform.position);
                }
                else
                {
                    instance.GetComponent<JC.VFX.VfxEffect>()?.Play();
                }
            }
        }

        PlaySound(presentation.HitSoundId, socket, presentation.SfxVolume);
    }

    /// <summary>EffectRegistry에서 id로 프리팹을 조회합니다(시퀀서의 재료 판별용). 0이면 null.</summary>
    public GameObject GetRegisteredEffect(int id) => id != 0 ? _effectRegistry?.Get(id) : null;

    // EffectRegistry id로 프리팹 조회(0이면 null).
    private GameObject ResolveEffectPrefab(int effectId)
    {
        return effectId != 0 ? _effectRegistry?.Get(effectId) : null;
    }

    // SoundRegistry id로 재생(0이면 무시).
    private void PlaySound(int soundId, Transform at, float volume)
    {
        if (SoundManager.Instance == null || soundId == 0)
        {
            return;
        }

        Vector3 pos = at != null ? at.position : Vector3.zero;
        SoundManager.Instance.PlayById(soundId, pos, volume);
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
