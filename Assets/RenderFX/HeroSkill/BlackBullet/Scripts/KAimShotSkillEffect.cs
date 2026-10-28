using System.Linq;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// ASB 연출 파이프라인 ↔ KAimShotVfx 브릿지.
    ///
    /// ASB의 UnitEffectPresenter는 Cue에 걸린 프리팹을 Instantiate한 뒤
    /// instance.GetComponent&lt;ISkillEffectBehaviour&gt;()?.Play(effectContext) 를 호출한다.
    /// KAimShotVfx는 JC 계보의 VfxEffect라 그 계약을 모르므로, 이 컴포넌트가 ctx를 번역해 넘긴다.
    ///
    /// 소켓은 ctx.SocketTransform으로 들어온다(Cue의 Anchor=CasterSocket, Socket=WeaponVfxRight로
    /// 지정하면 Gun.001/ShootPlace가 해석돼 온다). 위치만 쓰고 부모로 삼지 않는다 —
    /// 총구 소켓 lossyScale이 1.6배, 무기 본 소켓은 200배라 자식이 되면 크기가 깨진다.
    ///
    /// 탄착: KAimShotVfx.Impacted 순간 SkillImpactSignalBus에 착탄 신호를 낸다. 스킬 연출 데이터의
    /// ImpactDeliveryMode가 CustomEffectImpact면 피해가 이 신호까지 기다린다(그 외 스킬은 신호를 무시).
    /// 탄착 전에 이펙트가 꺼지면 그 자리에서 신호를 내 피해가 타임아웃까지 밀리지 않게 한다.
    ///
    /// ASB 파일은 수정하지 않는다. 같은 인터페이스만 구현해 JC 재료를 얹는 방식.
    /// </summary>
    [RequireComponent(typeof(KAimShotVfx))]
    public class KAimShotSkillEffect : MonoBehaviour, ISkillEffectBehaviour
    {
        [Tooltip("연출이 끝나면 인스턴스를 파괴한다. ASB EffectManager가 수명을 관리하지 않는 경우 대비.")]
        [SerializeField] private bool destroyOnFinish = true;

        private KAimShotVfx _vfx;
        private bool _subscribed;
        private SkillEffectContext _context;
        private bool _impactPublished;

        private void Awake() => Resolve();

        private void Resolve()
        {
            if (_vfx == null) _vfx = GetComponent<KAimShotVfx>();
            if (_vfx != null && !_subscribed)
            {
                if (destroyOnFinish) _vfx.OnFinished += OnVfxFinished;
                _vfx.Impacted += OnVfxImpacted;
                _subscribed = true;
            }
        }

        private void OnVfxFinished(VfxEffect _)
        {
            if (destroyOnFinish) Destroy(gameObject);
        }

        private void OnVfxImpacted(Vector3 impactPoint) => PublishImpact(impactPoint, true);

        private void OnDisable()
        {
            // 탄착 전에 꺼졌다(인터럽트·정리 등): 커밋된 공격의 피해가 타임아웃까지 밀리지 않게 즉시 알린다.
            // 실제 탄착이 아니므로 착탄음(Event SFX)은 내지 않는다.
            if (_context != null && !_impactPublished)
                PublishImpact(_context.TargetPosition, false);
        }

        private void PublishImpact(Vector3 position, bool playSfx)
        {
            if (_context == null || _impactPublished) return;
            _impactPublished = true;
            if (playSfx) PresentationSfxEvents.PlayEffectImpact(_context.Presentation, 0);
            var targets = _context.Targets?.Where(u => u != null).Select(u => u.transform).ToArray()
                          ?? System.Array.Empty<Transform>();
            SkillImpactSignalBus.PublishImpact(new ImpactKey(_context.ActionInstanceId), position, targets);
        }

        public void Play(SkillEffectContext ctx)
        {
            Resolve();
            if (_vfx == null)
            {
                Debug.LogWarning("[KAimShotSkillEffect] KAimShotVfx를 찾지 못했습니다.");
                return;
            }

            // 전투 Cue로 재생될 때만 착탄 신호를 낸다(ActionInstanceId 0 = 프리뷰/디버그 재생).
            _context = ctx != null && ctx.ActionInstanceId > 0 ? ctx : null;
            _impactPublished = false;

            // 전투 배속을 따른다(발사 시점 배속 고정 — JcVfxPresentationAdapter와 같은 규약). 탄착 시각도 함께 당겨진다.
            _vfx.PlaybackSpeed = Mathf.Max(0.01f, ctx?.PlaybackSpeed ?? 1f);

            // 총구 소켓: 위치만 참조한다(부모 결합 없음).
            _vfx.SetMuzzleSocket(ctx != null ? ctx.SocketTransform : null);

            Transform caster = ctx != null && ctx.Caster != null ? ctx.Caster.transform : null;
            Transform target = ctx != null && ctx.PrimaryTarget != null ? ctx.PrimaryTarget.transform : null;

            if (caster != null && target != null)
            {
                _vfx.Play(caster, target);
                return;
            }

            // 폴백: Transform이 없으면 컨텍스트의 월드 좌표로 재생.
            Vector3 origin = ctx != null && ctx.SocketTransform != null
                ? ctx.SocketTransform.position
                : (ctx != null ? ctx.SpawnPosition : transform.position);
            Vector3 impact = ctx != null ? ctx.TargetPosition : origin + transform.forward * 5f;
            if (impact == Vector3.zero && target == null && ctx != null) impact = ctx.SpawnPosition + Vector3.forward * 5f;

            _vfx.PlayAt(origin, impact);
        }
    }
}
