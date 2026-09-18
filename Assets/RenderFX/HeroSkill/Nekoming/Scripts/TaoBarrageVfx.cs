using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★네코밍 직업 스킬 「쓰러지면 안돼」(Taosenaiyo)의 광역 공격 부품 (큐 이름 <c>tao_barrage</c>).
    /// 260807 본 연출 1차 — 예시 이미지 7요소 분해(사용자)의 기술 구현:
    ///   ① 손앞 수직 마법진(TaoMagicCircle 재사용 — ★부활 오라와 자산 축 분리, 향후 별도 텍스처 교환 예정)
    ///   ② 발사 원점 코어 = 구형 프레넬(TaoBarrageCore) + 스타버스트 — 시트 밑동이 코어를 관통해 경계 없음
    ///   ③ 방사 스트릭 = 월드 고정 경사 시트(TaoBarrageSheet 셰이더, 원점 수렴) 부채 배치
    ///   ⑤⑥ 볼류메트릭·베일 = 시트 겹층 + 노이즈 베일 시트(같은 셰이더의 베일 지배 재질)
    ///   ④ 유성형 스타버스트 = ParticleSystem Trails(헤드=스타 빌보드, 꼬리=트레일 — stretch 금지)
    ///   ⑦ 피격 = 광 기둥 플레이스홀더 유지(교체 예정), 순서 규칙은 T9 소유(기본 동시)
    ///
    /// ★원점(Origin) = 시전자 기준 월드 상공 오프셋(T9 소유) — 화면 고정 금지(빌보드 2차 불일치 동류).
    ///   코어·시트·낙하 스타·피격 순서가 전부 이 원점 하나에서 파생된다.
    /// 시각물은 절차 조립(프리팹은 재질·프리셋 참조만) — 룩 색은 변종별 재질이 정본, 기하·타이밍은 T9.
    /// 다중 대상 규약(260804): 판정은 스킬 — SetTargets 로 받은 위치 목록만 쓴다. 빈 목록이면 Play 대상 폴백.
    /// </summary>
    public class TaoBarrageVfx : VfxEffect
    {
        [Header("프리셋")]
        [Tooltip("배리지 프리셋(T9, B/A). 기하·타이밍·밀도·피격 순서.")]
        [SerializeField] private TaoBarragePreset preset;
        [Tooltip("손앞 마법진 프리셋(T10, B/A) — ★부활 오라의 T1과 분리(비동일 제약).")]
        [SerializeField] private TaoMagicCirclePreset circlePreset;

        [Header("재질 (변종별 — 색·룩의 정본)")]
        [Tooltip("스트릭 시트(TaoBarrageSheet 셰이더, 스트릭 지배).")]
        [SerializeField] private Material sheetMaterial;
        [Tooltip("노이즈 베일 시트(같은 셰이더, 베일 지배 — 넓게 감싸는 눈속임 층).")]
        [SerializeField] private Material veilMaterial;
        [Tooltip("원점 코어(구형 프레넬 — TaoCometHead 계열 사본).")]
        [SerializeField] private Material coreMaterial;
        [Tooltip("낙하 스타 헤드(스타 스프라이트 가산).")]
        [SerializeField] private Material starHeadMaterial;
        [Tooltip("낙하 스타 꼬리(트레일 가산).")]
        [SerializeField] private Material starTrailMaterial;
        [Tooltip("손앞 마법진 재질(★부활 오라 재질과 분리 사본).")]
        [SerializeField] private Material circleMaterial;
        [Tooltip("피격 광 기둥(플레이스홀더 — 교체 예정).")]
        [SerializeField] private Material pillarMaterial;

        private static readonly int FadeMulID = Shader.PropertyToID("_FadeMul");

        private readonly List<VfxTarget> _targets = new List<VfxTarget>();
        private readonly List<GameObject> _pillars = new List<GameObject>();
        private readonly List<Renderer> _fadeRenderers = new List<Renderer>();

        // 절차 조립물(1회 생성 후 재사용)
        private Transform _beamRoot;
        private Transform _core;
        private ParticleSystem _coreStars;
        private ParticleSystem _fallStars;
        private TaoMagicCircle _circle;
        private MaterialPropertyBlock _mpb;

        private enum Phase { Idle, Build, Sustain, Out }
        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private float _fade;
        private float _hitTimer;
        private int _hitFired;                       // 순차 모드에서 몇 번째까지 발동했나
        private bool _hitsScheduled;
        private readonly List<Vector3> _hitOrder = new List<Vector3>();
        private Vector3 _origin;

        /// <summary>이번 광역 연출의 모든 피격 표시가 생성된 순간입니다.</summary>
        public event System.Action OnTargetsImpacted;
        private bool _impactPublished;

        public override void SetTargets(IReadOnlyList<VfxTarget> targets)
        {
            _targets.Clear();
            if (targets == null) return;
            for (int i = 0; i < targets.Count; i++) _targets.Add(targets[i]);
        }

        public override void Play(Transform origin, Transform target)
        {
            StopInternal();
            if (_targets.Count == 0 && target != null) _targets.Add(VfxTarget.Of(target));
            if (_targets.Count == 0)
            {
                Debug.LogWarning("[TaoBarrageVfx] 대상 없음 — 재생 생략", this);
                RaiseFinished();
                return;
            }

            var t = preset != null ? preset.TransformSource : null;
            Transform caster = origin != null ? origin : transform;

            // ── 원점·타격 중심 산출 (전부 월드 — 카메라 비의존) ──
            Vector3 center = Vector3.zero;
            for (int i = 0; i < _targets.Count; i++) center += _targets[i].Position;
            center /= _targets.Count;

            Vector3 toCenter = center - caster.position; toCenter.y = 0f;
            Vector3 backDir = toCenter.sqrMagnitude > 1e-4f ? -toCenter.normalized : -caster.forward;
            float h = t != null ? t.originHeight : 7f;
            float back = t != null ? t.originBack : 2.5f;
            _origin = caster.position + Vector3.up * h + backDir * back;

            EnsureBuilt();
            LayoutBeam(center, t);
            ConfigureStars(center, t);

            // 손앞 마법진 — 수직, 시전자→타격 중심을 바라봄
            if (_circle != null)
            {
                Vector3 hand = JcVfxPlacementPreset.Resolve(caster,
                    t != null ? t.handSocketName : "Socket_R_VFX",
                    t != null ? t.handOffset : new Vector3(0f, 0f, 0.15f));
                Vector3 face = center - hand; face.y = 0f;
                _circle.SetUpright(true);
                _circle.SetFacing(face.sqrMagnitude > 1e-4f ? face : caster.forward);
                _circle.Play(hand);
            }

            // 피격 순서 목록 확정 — 부품이 정렬을 소유(스테퍼 수집 순서는 비결정적).
            _hitOrder.Clear();
            for (int i = 0; i < _targets.Count; i++) _hitOrder.Add(_targets[i].Position);
            var order = t != null ? t.hitOrder : TaoBarragePreset.HitOrder.Simultaneous;
            if (order == TaoBarragePreset.HitOrder.ByOriginDistance)
                _hitOrder.Sort((a, b) => (a - _origin).sqrMagnitude.CompareTo((b - _origin).sqrMagnitude));
            else if (order == TaoBarragePreset.HitOrder.ByAxisSum)
                _hitOrder.Sort((a, b) => (a.x + a.z).CompareTo(b.x + b.z));

            _hitFired = 0;
            _impactPublished = false;
            _hitsScheduled = false;
            _hitTimer = 0f;
            _fade = 0f;
            SetActiveVisuals(true);
            ApplyFade(0f);
            EnterPhase(Phase.Build);
            IsPlaying = true;
        }

        public override void Stop() => StopInternal();

        private void OnDestroy()
        {
            KillPillars();
            // 조립물은 자식이라 함께 파괴된다.
        }

        private void Update()
        {
            if (!IsPlaying) return;
            var t = preset != null ? preset.TransformSource : null;
            float buildTime = t != null ? t.buildTime : 0.25f;
            float sustainTime = t != null ? t.sustainTime : 1.4f;
            float fadeOutTime = t != null ? t.fadeOutTime : 0.45f;

            _phaseT += Time.deltaTime;
            switch (_phase)
            {
                case Phase.Build:
                    _fade = Mathf.Clamp01(_phaseT / Mathf.Max(buildTime, 1e-4f));
                    if (_phaseT >= buildTime) { _fade = 1f; EnterPhase(Phase.Sustain); }
                    break;
                case Phase.Sustain:
                    _fade = 1f;
                    if (_phaseT >= sustainTime) BeginOut();
                    break;
                case Phase.Out:
                    _fade = 1f - Mathf.Clamp01(_phaseT / Mathf.Max(fadeOutTime, 1e-4f));
                    if (_phaseT >= fadeOutTime)
                    {
                        StopInternal();
                        RaiseFinished();
                        return;
                    }
                    break;
            }

            ApplyFade(_fade);
            if (_circle != null) _circle.SetEnvelope(_fade);

            // ── 피격 스케줄 — 전개 완료 + hitDelay 후. 동시(기본) 또는 순차(stagger). ──
            if (_phase != Phase.Build && !AllHitsFired())
            {
                _hitTimer += Time.deltaTime;
                float hitDelay = t != null ? t.hitDelay : 0.12f;
                float stagger = t != null ? t.stagger : 0f;
                var order = t != null ? t.hitOrder : TaoBarragePreset.HitOrder.Simultaneous;

                if (!_hitsScheduled && _hitTimer >= hitDelay)
                {
                    _hitsScheduled = true;
                    if (order == TaoBarragePreset.HitOrder.Simultaneous || stagger <= 0f)
                    {
                        for (int i = 0; i < _hitOrder.Count; i++) SpawnPillar(_hitOrder[i], t);
                        _hitFired = _hitOrder.Count;
                    }
                }
                if (_hitsScheduled && _hitFired < _hitOrder.Count)
                {
                    float sinceFirst = _hitTimer - (t != null ? t.hitDelay : 0.12f);
                    while (_hitFired < _hitOrder.Count && sinceFirst >= _hitFired * Mathf.Max(stagger, 1e-4f))
                        SpawnPillar(_hitOrder[_hitFired++], t);
                }
            }
            if (!_impactPublished && AllHitsFired())
            {
                _impactPublished = true;
                OnTargetsImpacted?.Invoke();
            }
        }

        private bool AllHitsFired() => _hitsScheduled && _hitFired >= _hitOrder.Count;

        // ── 절차 조립 ─────────────────────────────────────────────

        /// <summary>시각물 1회 생성(자식) — 이후 Play 마다 배치만 갱신.</summary>
        private void EnsureBuilt()
        {
            if (_beamRoot != null) return;
            _mpb = new MaterialPropertyBlock();
            _fadeRenderers.Clear();

            _beamRoot = new GameObject("BeamRoot").transform;
            _beamRoot.SetParent(transform, false);

            var t = preset != null ? preset.TransformSource : null;
            int sheetCount = t != null ? t.sheetCount : 3;
            for (int i = 0; i < sheetCount; i++)
                _fadeRenderers.Add(MakeSheet($"Sheet{i}", sheetMaterial));
            _fadeRenderers.Add(MakeSheet("Veil", veilMaterial));

            // 코어 — 구형 프레넬. 시트 밑동이 관통해 「경계 없는 코어」로 읽힌다.
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(core.GetComponent<Collider>());
            core.name = "Core";
            core.transform.SetParent(transform, false);
            var coreR = core.GetComponent<MeshRenderer>();
            if (coreMaterial != null) coreR.sharedMaterial = coreMaterial;
            coreR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            coreR.receiveShadows = false;
            _core = core.transform;
            _fadeRenderers.Add(coreR);

            _coreStars = MakeParticles("CoreStars", starHeadMaterial, null);
            _fallStars = MakeParticles("FallStars", starHeadMaterial, starTrailMaterial);

            // 손앞 마법진 — ★부활 오라와 분리된 재질·프리셋 사본(비동일 제약).
            var circleGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(circleGo.GetComponent<Collider>());
            circleGo.name = "CastCircle";
            circleGo.transform.SetParent(transform, false);
            var circleR = circleGo.GetComponent<MeshRenderer>();
            if (circleMaterial != null) circleR.sharedMaterial = circleMaterial;
            circleR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            circleR.receiveShadows = false;
            _circle = circleGo.AddComponent<TaoMagicCircle>();
            _circle.SetPreset(circlePreset);
            _circle.SetUpright(true);

            SetActiveVisuals(false);
        }

        private Renderer MakeSheet(string name, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(_beamRoot, false);
            var r = go.GetComponent<MeshRenderer>();
            if (mat != null) r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private ParticleSystem MakeParticles(string name, Material headMat, Material trailMat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            if (headMat != null) r.sharedMaterial = headMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (trailMat != null)
            {
                var tr = ps.trails;
                tr.enabled = true;
                tr.mode = ParticleSystemTrailMode.PerParticle;
                tr.dieWithParticles = true;
                tr.sizeAffectsWidth = true;
                tr.inheritParticleColor = true;
                tr.widthOverTrail = new ParticleSystem.MinMaxCurve(0.5f);
                r.trailMaterial = trailMat;
            }
            return ps;
        }

        /// <summary>원점→타격 중심으로 빔 시트·코어를 배치. 시트 로컬 +Y = 빔 축(셰이더 v 규약).</summary>
        private void LayoutBeam(Vector3 groundCenter, TaoBarragePreset t)
        {
            Vector3 dir = (groundCenter - _origin).normalized;
            float dist = Vector3.Distance(groundCenter, _origin) * (t != null ? t.sheetLengthMul : 1.25f);
            float width = t != null ? t.sheetWidth : 5.5f;
            float spread = t != null ? t.sheetSpreadDeg : 38f;
            int sheetCount = t != null ? t.sheetCount : 3;

            Vector3 side = Vector3.Cross(dir, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            _beamRoot.position = _origin;
            _beamRoot.rotation = Quaternion.LookRotation(side.normalized, dir);   // up = 빔 축

            int idx = 0;
            foreach (Transform child in _beamRoot)
            {
                bool isVeil = child.name == "Veil";
                float ang = 0f;
                if (!isVeil && sheetCount > 1)
                    ang = Mathf.Lerp(-spread * 0.5f, spread * 0.5f, sheetCount == 1 ? 0.5f : (float)idx / (sheetCount - 1));
                float w = isVeil ? width * (t != null ? t.veilWidthMul : 1.7f) : width;
                child.localRotation = Quaternion.AngleAxis(ang, Vector3.up);
                child.localScale = new Vector3(w, dist, 1f);
                child.localPosition = Vector3.up * (dist * 0.5f);
                if (!isVeil) idx++;
            }

            if (_core != null)
            {
                _core.position = _origin;
                _core.localScale = Vector3.one * (t != null ? t.coreSize : 1.4f);
            }
        }

        private void ConfigureStars(Vector3 groundCenter, TaoBarragePreset t)
        {
            Vector3 dir = (groundCenter - _origin).normalized;
            float dist = Vector3.Distance(groundCenter, _origin);

            if (_fallStars != null)
            {
                _fallStars.transform.position = _origin;
                _fallStars.transform.rotation = Quaternion.LookRotation(dir);   // 콘 축 = +Z
                var main = _fallStars.main;
                float sMin = t != null ? t.starSpeedMin : 9f;
                float sMax = t != null ? t.starSpeedMax : 15f;
                main.startSpeed = new ParticleSystem.MinMaxCurve(sMin, sMax);
                main.startSize = new ParticleSystem.MinMaxCurve(t != null ? t.starSizeMin : 0.14f, t != null ? t.starSizeMax : 0.34f);
                main.startLifetime = dist / Mathf.Max(sMin, 0.1f) * 1.15f;
                var sh = _fallStars.shape;
                sh.enabled = true;
                sh.shapeType = ParticleSystemShapeType.Cone;
                sh.angle = t != null ? t.starConeDeg : 14f;
                sh.radius = 0.25f;
                var em = _fallStars.emission;
                em.rateOverTime = t != null ? t.starRate : 70f;
                var tr = _fallStars.trails;
                tr.lifetime = new ParticleSystem.MinMaxCurve(t != null ? t.starTrailTime : 0.3f);
            }

            if (_coreStars != null)
            {
                _coreStars.transform.position = _origin;
                var main = _coreStars.main;
                main.startSpeed = 0f;
                main.startLifetime = 0.55f;
                main.startSize = new ParticleSystem.MinMaxCurve((t != null ? t.coreStarSize : 2.6f) * 0.6f, t != null ? t.coreStarSize : 2.6f);
                var sh = _coreStars.shape; sh.enabled = true; sh.shapeType = ParticleSystemShapeType.Sphere; sh.radius = 0.15f;
                var em = _coreStars.emission; em.rateOverTime = 7f;
            }
        }

        // ── 엔벨로프·정리 ─────────────────────────────────────────

        private void EnterPhase(Phase p) { _phase = p; _phaseT = 0f; }

        private void BeginOut()
        {
            if (_fallStars != null) _fallStars.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_coreStars != null) _coreStars.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            EnterPhase(Phase.Out);
        }

        private void ApplyFade(float f)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            for (int i = 0; i < _fadeRenderers.Count; i++)
            {
                var r = _fadeRenderers[i];
                if (r == null) continue;
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(FadeMulID, f);
                r.SetPropertyBlock(_mpb);
            }
        }

        private void SetActiveVisuals(bool on)
        {
            if (_beamRoot != null) _beamRoot.gameObject.SetActive(on);
            if (_core != null) _core.gameObject.SetActive(on);
            if (on)
            {
                if (_fallStars != null) { _fallStars.Clear(); _fallStars.Play(); }
                if (_coreStars != null) { _coreStars.Clear(); _coreStars.Play(); }
            }
            else
            {
                if (_fallStars != null) _fallStars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_coreStars != null) _coreStars.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                if (_circle != null) _circle.Stop();
            }
        }

        // ── 피격 (플레이스홀더 — 교체 예정) ──────────────────────

        private void SpawnPillar(Vector3 groundPos, TaoBarragePreset t)
        {
            float w = t != null ? t.pillarWidth : 0.45f;
            float hh = t != null ? t.pillarHeight : 3f;
            float life = t != null ? t.pillarLife : 0.55f;
            Color col = preset != null ? preset.color * Mathf.Max(preset.intensity, 0f) : Color.white;
            col.a = preset != null ? preset.color.a : 1f;

            var root = new GameObject("TaoBarragePillar(Placeholder)");
            root.transform.position = groundPos;
            for (int i = 0; i < 2; i++)
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(root.transform, false);
                quad.transform.localPosition = Vector3.up * (hh * 0.5f);
                quad.transform.localRotation = Quaternion.Euler(0f, 90f * i, 0f);
                quad.transform.localScale = new Vector3(w, hh, 1f);
                var r = quad.GetComponent<MeshRenderer>();
                if (pillarMaterial != null) r.sharedMaterial = pillarMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_Color", col);
                mpb.SetColor("_BaseColor", col);
                r.SetPropertyBlock(mpb);
            }
            _pillars.Add(root);
            Destroy(root, life);
        }

        private void KillPillars()
        {
            for (int i = 0; i < _pillars.Count; i++)
                if (_pillars[i] != null) Destroy(_pillars[i]);
            _pillars.Clear();
        }

        private void StopInternal()
        {
            KillPillars();
            if (_beamRoot != null) SetActiveVisuals(false);
            _phase = Phase.Idle;
            IsPlaying = false;
        }
    }
}
