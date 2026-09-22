using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// ★LFL 「합체」 부품 (큐 이름 <c>lfl_merge</c>) — 260804 신설.
    /// 양손 위치에서 차오른 오브 2개가 합류점으로 <b>직선+가속(easeIn)</b> 수렴,
    /// 겹치는 순간 섬광과 함께 <b>큰 구체 하나</b>로 합쳐진다.
    ///
    /// 부품 경계 — 이 부품은 <b>자기 몫의 오브 2개를 새로 만든다.</b>
    ///   앞 마디(lfl_charge_r/l)가 만든 오브를 넘겨받을 방법이 없다(부품끼리 참조 없음).
    ///   대신 ChargeOrbVfx.ShowCharged() 로 「이미 차오른 상태」에서 시작하므로,
    ///   앞 마디가 제때 꺼지면(스테퍼 lifeTime) 이어져 보인다.
    ///
    /// 합쳐진 큰 구체 = 발사 투사체와 <b>같은 프리팹</b>을 그냥 보여 준다(Show만, Launch 없음).
    ///   그래서 크기·룩이 lfl_fire 와 자동으로 일치하고, holdTime 이 끝나 이 구체가 사라지는
    ///   순간 다음 마디(lfl_fire)가 같은 자리에서 발사하면 하나의 구체처럼 이어진다.
    ///
    /// 타임라인:  Converge(convergeTime) → 섬광+큰 구체 → Hold(holdTime) → 종료.
    ///   다음 마디까지의 delayAfter = convergeTime + holdTime 으로 맞출 것.
    /// </summary>
    public class LflMergeVfx : VfxEffect
    {
        [Tooltip("이 변종의 합류 시간·섬광 설정입니다. Basic과 Alter는 독립 프리셋을 사용합니다.")]
        [SerializeField] private LflMergePreset preset;
        public void PullFromPreset()
        {
            if(preset==null)return;
            convergeTime=preset.convergeTime;holdTime=preset.holdTime;
            flashColor=preset.flashColor;flashSizeMul=preset.flashSizeMul;
        }

        [Header("References (프리팹 에셋 — 런타임 인스턴스화)")]
        [Tooltip("양손 오브(작은 것 — LFL 전용 차지 프리팹). ShowCharged 로 즉시 표시된다.")]
        [SerializeField] private ChargeOrbVfx orbPrefab;
        [Tooltip("합쳐진 큰 구체. 발사 투사체와 같은 프리팹을 지정해야 lfl_fire 와 크기·룩이 일치한다.")]
        [SerializeField] private ProjectileVfx mergedOrbPrefab;
        [Tooltip("합체 순간 섬광(자식, PawFlash 재사용)")]
        [SerializeField] private PawFlash flash;

        [Header("손 위치 (폴백 전용 — 정본은 차지 프리셋 L1)")]
        [Tooltip("★260807 단일 소스화: 손 시작점의 정본은 orbPrefab 이 문 차지 프리셋(L1)의 spawnOffset.\n" +
                 "오른손 = 그대로, 왼손 = X 부호 반전(스테퍼 mirrorX 와 같은 규칙) — L1 을 고치면 여기도 따라온다.\n" +
                 "이 값은 차지 프리셋이 비어 있을 때만 쓰는 폴백이다.")]
        [SerializeField] private Vector3 handLocalOffsetR = new Vector3(0.35f, 1.30f, 0.20f);
        [Tooltip("왼손 폴백. 위와 같음 — 정본은 L1 spawnOffset 의 X 반전.")]
        [SerializeField] private Vector3 handLocalOffsetL = new Vector3(-0.35f, 1.30f, 0.20f);

        [Header("수렴 / 합체")]
        [Tooltip("수렴 시간(초). 직선+가속(easeIn) — 빨려들 듯 모인다.")]
        [Range(0.05f, 1f)] [SerializeField] private float convergeTime = 0.25f;
        [Tooltip("합쳐진 구체를 유지할 시간(초). 끝나는 순간 lfl_fire 가 이어받는다.")]
        [Range(0.05f, 1f)] [SerializeField] private float holdTime = 0.25f;
        [Tooltip("프리셋이 없을 때 사용하는 합체 섬광 HDR 색상입니다.")]
        [ColorUsage(true, true)] [SerializeField] private Color flashColor = new Color(0.85f, 0.92f, 1f);
        [Tooltip("프리셋이 없을 때 사용하는 섬광 크기 배수입니다.")]
        [Range(0.2f, 3f)] [SerializeField] private float flashSizeMul = 0.8f;

        private enum Phase { Idle, Converge, Hold }
        private Phase _phase = Phase.Idle;
        private float _t;

        private ChargeOrbVfx _orbR, _orbL;
        private Vector3 _startR, _startL;
        private ProjectileVfx _merged;

        /// <summary>합류점 = 이 부품이 스폰된 자기 위치. 출발점은 origin(시전자) 로컬 오프셋.</summary>
        public override void Play(Transform origin, Transform target)
        {
            StopInternal();
            PullFromPreset();

            Vector3 mp = transform.position;
            _startR = HandStart(origin, true, mp);
            _startL = HandStart(origin, false, mp);

            _orbR = SpawnOrb(_startR);
            _orbL = SpawnOrb(_startL);

            _phase = Phase.Converge;
            _t = 0f;
            IsPlaying = true;
        }

        public override void Stop() => StopInternal();

        /// <summary>
        /// ★손 시작점 단일 소스(260807) — 정본은 차지 프리셋(L1)의 spawnOffset.
        /// 오른손 = 그대로 / 왼손 = X 반전(스테퍼 mirrorX 와 같은 규칙). 해석도 차지 마디와 동일한
        /// <see cref="JcVfxPlacementPreset.Resolve(Transform,string,Vector3)"/>(회전 따름·스케일 무시)라
        /// 앞 마디(lfl_charge_r/l)가 만든 오브 위치와 정확히 겹친다. 프리셋이 없으면 내장값 폴백.
        /// 시전자가 없으면(순수 프리뷰) 합류점 좌우에서 시작 — 부품 단독 재생도 굴러가게.
        /// </summary>
        private Vector3 HandStart(Transform origin, bool right, Vector3 mergePoint)
        {
            if (origin == null) return mergePoint + new Vector3(right ? 0.35f : -0.35f, -0.15f, 0f);

            var preset = orbPrefab != null ? orbPrefab.Preset : null;
            if (preset != null)
            {
                var t = preset.TransformSource;   // 따름 규칙(Alter → Basic) 포함
                var off = t.spawnOffset;
                if (!right) off.x = -off.x;
                return JcVfxPlacementPreset.Resolve(origin, t.spawnSocketName, off);
            }
            return origin.TransformPoint(right ? handLocalOffsetR : handLocalOffsetL);
        }

        /// <summary>
        /// ★오브·합체 구체는 씬 루트에 있어 자식이 아니다 — 이 부품이 Stop 없이 파괴되면
        /// (스테퍼 리셋 등) 전부 고아가 되므로 여기서 같이 지운다. 각 오브의 OnDestroy 가
        /// 자기 팔로워를 이어서 지우므로 연쇄적으로 정리된다.
        /// </summary>
        private void OnDestroy()
        {
            if (_orbR) Destroy(_orbR.gameObject);
            if (_orbL) Destroy(_orbL.gameObject);
            if (_merged) Destroy(_merged.gameObject);
        }

        private void Update()
        {
            if (!IsPlaying) return;
            _t += Time.deltaTime;

            switch (_phase)
            {
                case Phase.Converge:
                {
                    float u = Mathf.Clamp01(_t / Mathf.Max(convergeTime, 1e-4f));
                    float e = u * u;   // easeIn — 통짜(LetsFightingLoveVfx.Converge)와 같은 감각
                    Vector3 mp = transform.position;
                    if (_orbR) _orbR.transform.position = Vector3.Lerp(_startR, mp, e);
                    if (_orbL) _orbL.transform.position = Vector3.Lerp(_startL, mp, e);
                    if (u >= 1f)
                    {
                        KillOrbs();   // 오브 즉시 소멸(트레일은 자연 소멸)
                        if (flash) flash.Flash(mp, flashColor, flashSizeMul);
                        if (mergedOrbPrefab != null)
                        {
                            if (_merged == null) _merged = Instantiate(mergedOrbPrefab);   // 씬 루트(lossyScale 1)
                            _merged.Show(mp);   // Launch 없음 — 그 자리에서 「합쳐진 구체」로만 존재
                        }
                        _phase = Phase.Hold;
                        _t = 0f;
                    }
                    break;
                }

                case Phase.Hold:
                    if (_t >= holdTime)
                    {
                        if (_merged) _merged.Stop();
                        Finish();
                    }
                    break;
            }
        }

        private ChargeOrbVfx SpawnOrb(Vector3 pos)
        {
            if (orbPrefab == null) return null;
            var orb = Instantiate(orbPrefab.gameObject).GetComponent<ChargeOrbVfx>();   // 씬 루트(lossyScale 1)
            orb.transform.position = pos;
            orb.ShowCharged();
            return orb;
        }

        private void KillOrbs()
        {
            if (_orbR) { _orbR.Stop(); Destroy(_orbR.gameObject, 2f); _orbR = null; }
            if (_orbL) { _orbL.Stop(); Destroy(_orbL.gameObject, 2f); _orbL = null; }
        }

        private void StopInternal()
        {
            KillOrbs();
            if (_merged) { _merged.Stop(); Destroy(_merged.gameObject, 2f); _merged = null; }
            if (flash) flash.StopAll();
            _phase = Phase.Idle;
            IsPlaying = false;
        }

        private void Finish()
        {
            if (_merged) { Destroy(_merged.gameObject, 2f); _merged = null; }
            _phase = Phase.Idle;
            IsPlaying = false;
            RaiseFinished();
        }
    }
}
