using System.Collections;
using UnityEngine;

/// <summary>
/// 준비 단계 차징 연출: 스폰되면 배속-시간 동안 크기를 키운다(비행/피해 없음).
/// held 이펙트(ISkillEffectHandle)로 등록되어, 발사 시점에 AoEApplyDamageAction이 이 인스턴스를 넘겨받아 발사한다.
/// 전투 타입(피해/게이트)은 절대 참조하지 않는다.
/// </summary>
[DisallowMultipleComponent]
public class ProjectileChargeGrow : MonoBehaviour, ISkillEffectBehaviour, ISkillEffectHandle
{
    [Tooltip("시작 배율(기준 스케일 대비).")]
    [SerializeField] private float _startScale = 0.2f;
    [Tooltip("최종 배율(기준 스케일 대비).")]
    [SerializeField] private float _endScale = 1.0f;
    [Tooltip("확대에 걸리는 배속-시간(초).")]
    [SerializeField] private float _growSeconds = 0.5f;

    private float _battleSpeed = 1f;
    private Vector3 _baseScale;

    public void Play(SkillEffectContext ctx)
    {
        _battleSpeed = ctx != null ? Mathf.Max(0.01f, ctx.PlaybackSpeed) : 1f;
        _baseScale = transform.localScale;
        transform.localScale = _baseScale * _startScale;
        StartCoroutine(GrowRoutine());
    }

    private IEnumerator GrowRoutine()
    {
        float t = 0f;
        while (t < _growSeconds)
        {
            t += Time.deltaTime * _battleSpeed;
            float u = Mathf.Clamp01(t / _growSeconds);
            transform.localScale = _baseScale * Mathf.Lerp(_startScale, _endScale, u);
            yield return null;
        }
        transform.localScale = _baseScale * _endScale;
    }

    // 발사 단계는 AoEApplyDamageAction이 인스턴스를 직접 넘겨받아 처리하므로 Signal은 사용하지 않는다.
    public bool Signal(SkillEffectContext ctx) => true;

    // 발사 전 취소(액션 종료 등)로 Stop이 오면 정리. 발사에 넘겨진 뒤에는 등록 해제되어 여기로 오지 않는다.
    public void Stop()
    {
        Destroy(gameObject);
    }
}
