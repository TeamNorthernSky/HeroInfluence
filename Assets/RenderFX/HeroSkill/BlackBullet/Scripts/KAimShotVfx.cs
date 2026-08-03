using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 블랙불릿 「블랙 데빌 퍼펙트 에임 샷」(3010) VFX 오케스트레이터.
    /// 요소: 머즐 플래시 / 탄두 / 궤적(획) / 탄착 스타버스트.
    ///
    /// ★타이밍 구조 — 요소 4개 전부 독립 생명주기다.
    ///   시퀀스 시작부터의 전역 시계(_seqT) 하나만 두고, 각 요소는 자기 startDelay에 발동해
    ///   자기 lifetime 동안 살고 끝난다. 한 요소의 수명을 늘려도 다른 요소가 밀리지 않는다.
    ///   (페이즈 상태머신은 모든 요소가 독립화되면서 역할이 사라져 제거했다.
    ///    남은 시퀀스 파라미터는 launchDelay·travelTime 둘뿐이며, 이는 탄두 위치 계산용이다.)
    ///
    /// 저장 구조(저스티스 JusticeTrailPresetBinder와 동일):
    ///   프리팹도 값을 직렬화로 들고 있지만(2중 저장), **스폰 시점(Awake)에 프리셋을 다시 읽어**
    ///   덮어쓴다. 즉 런타임 권위는 프리셋이다. ASB가 Cue마다 프리팹을 새로 Instantiate하므로
    ///   이 재읽기 덕분에 플레이 중 프리셋 수정이 다음 발사에 버튼 조작 없이 반영된다.
    ///
    /// ★소켓 처리: 총구 소켓을 부모로 삼지 않는다. 위치만 읽어 쓴다.
    ///   총구 소켓(ShootPlace) lossyScale = 1.6, 무기 본 소켓 = 200배라 자식이 되면 크기가 깨진다.
    ///   TransformPoint 금지 — 오프셋에 스케일이 곱해진다. socket.position + socket.rotation * offset 만 사용.
    /// </summary>
    public class KAimShotVfx : VfxEffect
    {
        [Header("프리셋 (런타임 권위 — Awake에서 재읽기)")]
        [Tooltip("적용·캡처 대상 프리셋. 에디터가 이 참조로 스코프를 판정한다(대상 오배선 방지).")]
        [SerializeField] private KAimShotPreset masterPreset;

        [Header("요소 참조")]
        [SerializeField] private KStarBurstQuad muzzleQuad;
        [SerializeField] private KStarBurstQuad headQuad;
        [SerializeField] private KStarBurstQuad impactQuad;
        [SerializeField] private KBulletTrailStroke trailStroke;

        [Header("── 시퀀스 ──")]
        public float launchDelay = 0.06f;
        public float travelTime = 0.12f;

        [Header("── 머즐 플래시 ──")]
        public KElementLife muzzleLife = new KElementLife();

        [Header("── 탄두 ──")]
        public KElementLife bulletHead = new KElementLife();

        [Header("── 궤적 : 시간축 ──")]
        [Tooltip("크기 3값은 획 전체의 기준 굵기(시간에 따라 변함). 머리↔꼬리 차이는 아래 공간 테이퍼가 담당.")]
        public KElementLife trail = new KElementLife();
        [Tooltip("★그어진 획이 화면에 남는 시간(절대 초). 비행 중엔 궤적 길이, 탄착 후엔 잔존 시간을 결정한다.")]
        public float tailReachSeconds = 0.14f;

        [Header("── 궤적 : 공간 테이퍼 (머리↔꼬리) ──")]
        [Range(0.01f, 1f)] public float trailTailWidthRatio = 0.30f;
        [Range(0f, 1f)] public float trailTailBrightnessRatio = 0.22f;
        [Range(0.2f, 6f)] public float trailTaperCurve = 1.15f;

        [Header("── 탄착 ──")]
        public KElementLife impactBurst = new KElementLife();

        [Header("── 색 (요소별 독립) ──")]
        [Tooltip("축=시간")] public KColorSet muzzleColors = new KColorSet();
        [Tooltip("축=시간")] public KColorSet headColors = new KColorSet();
        [Tooltip("축=공간(탄두→총구)")] public KColorSet trailColors = new KColorSet();
        [Tooltip("축=시간")] public KColorSet impactColors = new KColorSet();

        [Header("── 발사 지점 폴백 ──")]
        public Vector3 casterFallbackOffset = new Vector3(0.35f, 1.0f, 0.15f);

        /// <summary>에디터 스코프 판정용 — 이 인스턴스가 해당 프리셋을 실제로 참조하는지.</summary>
        public bool UsesPreset(KAimShotPreset p) => masterPreset != null && masterPreset == p;

        /// <summary>시퀀스 전역 시계. 모든 요소의 기준.</summary>
        private float _seqT;
        private bool _muzzleDone, _headDone, _trailDone, _impactDone, _trailBegun;

        private Transform _muzzleSocket;
        private Transform _caster;
        private Transform _target;
        private Vector3 _shotOrigin;
        private Vector3 _impactPoint;
        private Vector3 _headPos;

        private void Awake()
        {
            PullFromPreset();
            HideImmediate();
        }

        /// <summary>
        /// 프리셋 → 자기 직렬화 값으로 재읽기. 저스티스 바인더(Awake => ApplyNow)와 같은 규약.
        /// 프리셋이 비어 있으면 프리팹에 굳어 있는 값을 그대로 쓴다.
        /// </summary>
        public void PullFromPreset()
        {
            if (masterPreset == null) return;
            var p = masterPreset;
            launchDelay = p.launchDelay;
            travelTime = p.travelTime;
            if (muzzleLife == null) muzzleLife = new KElementLife();
            muzzleLife.CopyFrom(p.muzzleFlash);
            if (bulletHead == null) bulletHead = new KElementLife();
            bulletHead.CopyFrom(p.bulletHead);
            if (trail == null) trail = new KElementLife();
            trail.CopyFrom(p.trail);
            tailReachSeconds = p.tailReachSeconds;
            trailTailWidthRatio = p.trailTailWidthRatio;
            trailTailBrightnessRatio = p.trailTailBrightnessRatio;
            trailTaperCurve = p.trailTaperCurve;
            if (impactBurst == null) impactBurst = new KElementLife();
            impactBurst.CopyFrom(p.impactBurst);
            if (muzzleColors == null) muzzleColors = new KColorSet();
            muzzleColors.CopyFrom(p.muzzleColors);
            if (headColors == null) headColors = new KColorSet();
            headColors.CopyFrom(p.headColors);
            if (trailColors == null) trailColors = new KColorSet();
            trailColors.CopyFrom(p.trailColors);
            if (impactColors == null) impactColors = new KColorSet();
            impactColors.CopyFrom(p.impactColors);
            casterFallbackOffset = p.casterFallbackOffset;
        }

        private void HideImmediate()
        {
            muzzleQuad?.SetVisible(false);
            headQuad?.SetVisible(false);
            impactQuad?.SetVisible(false);
            trailStroke?.HideImmediate();
            IsPlaying = false;
            _seqT = 0f;
            _muzzleDone = _headDone = _trailDone = _impactDone = false;
            _trailBegun = false;
        }

        /// <summary>총구 소켓을 명시 지정. Play 전에 호출한다. null이면 캐스터 루트 폴백.</summary>
        public void SetMuzzleSocket(Transform socket) => _muzzleSocket = socket;

        public override void Play(Transform origin, Transform target)
        {
            if (origin == null || target == null)
            {
                Debug.LogWarning("[KAimShotVfx] origin/target이 비어 있어 재생을 건너뜁니다.");
                return;
            }
            _caster = origin;
            _target = target;

            transform.SetParent(null, true);   // 소켓 자식 금지(스케일 오염 차단)
            transform.localScale = Vector3.one;

            _shotOrigin = ResolveMuzzlePosition();
            _impactPoint = ResolveImpactPoint();
            BeginSequence();
        }

        public override void Play() { /* 좌표 없이는 의미가 없다 — Play(origin,target)을 쓴다. */ }

        /// <summary>Transform 없이 월드 좌표로 직접 재생(ASB ctx 폴백 경로).</summary>
        public void PlayAt(Vector3 origin, Vector3 impactPoint)
        {
            transform.SetParent(null, true);
            transform.localScale = Vector3.one;
            _caster = null;
            _target = null;
            _shotOrigin = origin;
            _impactPoint = impactPoint + (impactBurst != null ? impactBurst.offset : Vector3.zero);
            BeginSequence();
        }

        private void BeginSequence()
        {
            _headPos = _shotOrigin;
            _seqT = 0f;
            _muzzleDone = _headDone = _trailDone = _impactDone = false;
            _trailBegun = false;
            IsPlaying = true;
            muzzleQuad?.SetVisible(false);
            headQuad?.SetVisible(false);
            impactQuad?.SetVisible(false);
            trailStroke?.HideImmediate();
        }

        public override void Stop() => HideImmediate();

        /// <summary>즉시 종료. 요소별 페이드가 없으므로 하네스 토글용으로만 쓴다.</summary>
        public void StopGraceful()
        {
            if (!IsPlaying) return;
            HideImmediate();
            RaiseFinished();
        }

        // ── 앵커 산출 ──
        private Vector3 ResolveMuzzlePosition()
        {
            Vector3 off = muzzleLife != null ? muzzleLife.offset : Vector3.zero;
            if (_muzzleSocket != null)
                return _muzzleSocket.position + _muzzleSocket.rotation * off;   // TransformPoint 금지
            return _caster != null ? _caster.TransformPoint(casterFallbackOffset) : transform.position;
        }

        /// <summary>총구→탄착 방향. 탄두·궤적 정렬에는 소켓 forward보다 이쪽이 정확하다.</summary>
        private Vector3 TravelDirection()
        {
            Vector3 d = _impactPoint - _shotOrigin;
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        private Vector3 MuzzleForward()
            => _muzzleSocket != null ? _muzzleSocket.forward : TravelDirection();

        /// <summary>타겟은 유닛 루트(lossyScale 1)라 position + offset으로 충분하다.</summary>
        private Vector3 ResolveImpactPoint()
        {
            Vector3 off = impactBurst != null ? impactBurst.offset : Vector3.zero;
            return _target != null ? _target.position + off : transform.position;
        }

        // ── 요소별 갱신 (전부 독립 시계) ──

        /// <summary>탄두 위치 — 주행 진행도에 묶인다. 궤적도 이 값을 머리 끝점으로 쓴다.</summary>
        private void UpdateHeadPosition()
        {
            float t = _seqT - launchDelay;
            if (t <= 0f) { _headPos = _shotOrigin; return; }
            float u = travelTime > 1e-5f ? Mathf.Clamp01(t / travelTime) : 1f;
            _headPos = Vector3.Lerp(_shotOrigin, _impactPoint, u);
        }

        private void UpdateMuzzle()
        {
            if (muzzleQuad == null || muzzleLife == null || _muzzleDone) return;
            float t = _seqT - muzzleLife.startDelay;
            if (t < 0f) { muzzleQuad.SetVisible(false); return; }

            if (KLifeEval.Sample(muzzleLife, t, out float size, out float fade))
            {
                muzzleQuad.SetVisible(true);
                muzzleQuad.SetWorldPosition(_shotOrigin);
                muzzleQuad.SetAxis(MuzzleForward());
                muzzleQuad.SetSize(size);
                muzzleQuad.SetFade(fade);
                // 색 그라데이션 축 = 시간(수명 진행도)
                float cu = Mathf.Clamp01(t / Mathf.Max(muzzleLife.lifetime, 1e-5f));
                muzzleQuad.SetColors(KColorEval.EdgeAt(muzzleColors, cu), KColorEval.Inner(muzzleColors));
            }
            else { muzzleQuad.SetVisible(false); _muzzleDone = true; }
        }

        private void UpdateHead()
        {
            if (headQuad == null || bulletHead == null || _headDone) return;
            float t = _seqT - bulletHead.startDelay;
            if (t < 0f) { headQuad.SetVisible(false); return; }

            if (KLifeEval.Sample(bulletHead, t, out float size, out float fade))
            {
                headQuad.SetVisible(true);
                headQuad.SetWorldPosition(_headPos + bulletHead.offset);
                headQuad.SetAxis(TravelDirection());
                headQuad.SetSize(size);
                headQuad.SetFade(fade);
                float cu = Mathf.Clamp01(t / Mathf.Max(bulletHead.lifetime, 1e-5f));
                headQuad.SetColors(KColorEval.EdgeAt(headColors, cu), KColorEval.Inner(headColors));
            }
            else { headQuad.SetVisible(false); _headDone = true; }
        }

        /// <summary>궤적 수명의 허용 창. 요소 시계(startDelay 기준) 단위.</summary>
        public void TrailLifetimeWindow(out float min, out float max)
        {
            float sd = trail != null ? trail.startDelay : 0f;
            min = Mathf.Max(0.01f, (launchDelay + travelTime) - sd);
            max = Mathf.Max(min, (launchDelay + travelTime + tailReachSeconds) - sd);
        }

        /// <summary>인스펙터 입력값을 허용 창으로 제한한 실효 수명.</summary>
        public float TrailEffectiveLifetime()
        {
            TrailLifetimeWindow(out float min, out float max);
            return Mathf.Clamp(trail != null ? trail.lifetime : min, min, max);
        }

        /// <summary>
        /// 궤적 — TrailRenderer가 탄두 위치 이력을 스스로 기록하므로 위치 추적·굵기·페이드만 준다.
        /// 탄두가 도착하면 정점 추가를 멈추고, 획은 tailReachSeconds에 걸쳐 스스로 수축해 사라진다.
        /// </summary>
        private void UpdateTrail()
        {
            if (trailStroke == null || trail == null || _trailDone) return;
            float t = _seqT - trail.startDelay;
            if (t < 0f) return;

            if (!_trailBegun) { trailStroke.Begin(_headPos); _trailBegun = true; }

            // 매 프레임 갱신 — 발사 시 1회만 주면 라이브 튜닝이 막힌다.
            trailStroke.SetTrailTime(tailReachSeconds);

            bool sampled = KLifeEval.Sample(trail, t, TrailEffectiveLifetime(), out float width, out float fade);
            bool emittingStopped = _seqT >= launchDelay + travelTime;

            if (sampled)
            {
                trailStroke.SetHeadPosition(_headPos);
                trailStroke.SetWidth(width);   // 시간축: 획 전체 기준 굵기
                // 공간축(머리↔꼬리) 테이퍼는 MPB로 — 공유 머티리얼을 건드리지 않는다.
                trailStroke.PushShaderParams(fade, trailTailWidthRatio, trailTailBrightnessRatio, trailTaperCurve);
                // 색 그라데이션 축 = 공간(획 길이). 셰이더가 head→mid→tail로 보간한다.
                KColorEval.EdgeStops(trailColors, out Color ch, out Color cm, out Color ct);
                trailStroke.PushColors(ch, cm, ct, KColorEval.Inner(trailColors));
                if (emittingStopped) trailStroke.StopEmitting();
            }
            else if (emittingStopped && trailStroke.HasPoints)
            {
                trailStroke.StopEmitting();
                trailStroke.SetFade(0f);
            }
            else { trailStroke.HideImmediate(); _trailDone = true; }
        }

        private void UpdateImpact()
        {
            if (impactQuad == null || impactBurst == null || _impactDone) return;
            float t = _seqT - impactBurst.startDelay;
            if (t < 0f) { impactQuad.SetVisible(false); return; }

            if (KLifeEval.Sample(impactBurst, t, out float size, out float fade))
            {
                impactQuad.SetVisible(true);
                impactQuad.SetWorldPosition(_impactPoint);   // 오프셋은 _impactPoint에 이미 포함
                impactQuad.SetAxis(TravelDirection());
                impactQuad.SetSize(size);
                impactQuad.SetFade(fade);
                float cu = Mathf.Clamp01(t / Mathf.Max(impactBurst.lifetime, 1e-5f));
                impactQuad.SetColors(KColorEval.EdgeAt(impactColors, cu), KColorEval.Inner(impactColors));
            }
            else { impactQuad.SetVisible(false); _impactDone = true; }
        }

        private static bool Pending(KElementLife l, bool done) => l != null && l.enabled && !done;

        private bool AllElementsDone()
            => !Pending(muzzleLife, _muzzleDone)
            && !Pending(bulletHead, _headDone)
            && !Pending(trail, _trailDone)
            && !Pending(impactBurst, _impactDone);

        private void Update()
        {
            if (!IsPlaying) return;

            _seqT += Time.deltaTime;

            UpdateHeadPosition();
            UpdateMuzzle();
            UpdateHead();
            UpdateTrail();
            UpdateImpact();

            if (AllElementsDone())
            {
                HideImmediate();
                RaiseFinished();
            }
        }
    }
}
