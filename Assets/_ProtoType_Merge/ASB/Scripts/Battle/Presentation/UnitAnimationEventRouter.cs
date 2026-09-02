using UnityEngine;

/// <summary>
/// 유닛 로컬 애니메이션 이벤트 라우터. 클립 이벤트를 같은 유닛의 담당 컴포넌트로 분배.
/// 중앙 싱글톤 아님. CharactorAnimationController=hit timing, 이펙트=EffectPresenter, 사운드=SoundPresenter.
/// </summary>
[DisallowMultipleComponent]
public class UnitAnimationEventRouter : MonoBehaviour
{
    private CharactorAnimationController _anim;
    private UnitEffectPresenter _effect;
    private UnitSoundPresenter _sound;

    private void Awake() => Resolve();

    private void Resolve()
    {
        if (_anim == null) _anim = GetComponent<CharactorAnimationController>();
        if (_effect == null) _effect = GetComponent<UnitEffectPresenter>();
        if (_sound == null) _sound = GetComponent<UnitSoundPresenter>();
    }

    /// <summary>전투 타이밍 신호. 연출과 분리.</summary>
    public void OnHit()
    {
        Resolve();
        _anim?.AniEvent_OnHit();
    }

    /// <summary>연출 Cue: 같은 cue의 이펙트와 사운드를 각 프리젠터로 분배.</summary>
    public void PresentationCue(string cueName)
    {
        Resolve();
        _effect?.PresentationCue(cueName);
        _sound?.PresentationCue(cueName);
    }

    /// <summary>연출 Cue를 CueId로 분배(Path A Timeline Signal 경로). 이름 충돌 없이 특정된다.</summary>
    public void PresentationCueById(string cueId)
    {
        Resolve();
        _effect?.PresentationCueById(cueId);
        _sound?.PresentationCueById(cueId);
    }

    /// <summary>연출 Cue를 CueName으로 분배(Path A Timeline Signal 경로). Timeline이 시점을 소유하므로 State 게이트를 우회한다.</summary>
    public void PresentationCueByName(string cueName)
    {
        Resolve();
        _effect?.PresentationCue(cueName, bypassStateGate: true);
        _sound?.PresentationCue(cueName, bypassStateGate: true);
    }
}
