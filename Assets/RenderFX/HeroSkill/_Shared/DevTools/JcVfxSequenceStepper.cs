using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// VFX 시퀀스 스테퍼 — <b>키를 누를 때마다 대장의 다음 부품을 하나씩</b> 재생한다.
    ///
    /// 통짜로 한 번에 재생하는 기존 DebugTrigger 와 목적이 다르다.
    /// 이건 <b>부품이 각자 제 구간만 도는지</b>, 그리고 <b>이어 붙였을 때 연출이 이어지는지</b>를 본다.
    ///
    /// 조작: <b>스텝 키</b>(기본 R) = 다음 단계 / <b>리셋 키</b>(기본 Z) = 처음으로 + 남은 것 정리
    /// ★리셋 키는 스킬 키를 피해서 고른다 — T 를 쓰다가 LFL 시전 키와 겹쳐 스킬이 안 나갔다(260804).
    ///
    /// ★대장(JcVfxCatalog)에서 이름으로 부품을 찾으므로, 대장만 채우면 어느 스킬이든 이걸로 밟아 볼 수 있다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcVfxSequenceStepper : MonoBehaviour
    {
        /// <summary>
        /// 단계를 어디에 생성할지.
        /// <b>ChainTarget</b> = 본 대상에서 십자로 인접한 아군 중 <see cref="Step.chainIndex"/> 번째.
        /// LFL 연쇄처럼 「본 대상 옆으로 튀는」 마디가 쓴다. 해당 순번이 없으면 그 단계는 조용히 생략된다
        /// (대상이 하나뿐인 배치에서 두 번째 연쇄가 안 나오는 게 정상이므로 경고를 띄우지 않는다).
        /// </summary>
        public enum SpawnAt { Caster, Target, Self, ChainTarget }

        [Serializable]
        public class Step
        {
            [Tooltip("대장의 이름(cueName).")]
            public string cueName;

            [Tooltip("택일 갈래. 해당 변종이 없으면 Base 로 폴백한다.")]
            public JcVfxCatalog.Variant variant = JcVfxCatalog.Variant.Base;

            [Tooltip("어느 지점에 생성할지. paw_warp 처럼 같은 이름을 두 지점에서 부를 때 쓴다.")]
            public SpawnAt spawnAt = SpawnAt.Caster;

            [Tooltip("spawnAt = ChainTarget 일 때 몇 번째 연쇄 대상인지(0부터). 가까운 순으로 정렬돼 있다.")]
            [Min(0)] public int chainIndex;

            [Tooltip("생성 지점에 더할 오프셋(머리 높이 등).")]
            public Vector3 offset;

            [Tooltip("★켜면 오프셋을 대상의 로컬 축으로 적용한다(TransformPoint).\n" +
                     "부품 내부가 TransformPoint 로 앵커를 잡는 경우 이걸 켜야 위치가 일치한다.\n" +
                     "끄면 월드 오프셋 — 캐릭터가 회전해 있으면 엉뚱한 쪽에 생긴다.")]
            public bool localOffset;

            [Tooltip("이 단계를 낸 뒤 다음 단계까지 기다릴 시간(초). 자동 재생에서만 쓰인다.\n" +
                     "부품 내부 타임라인(마스터 프리셋)과 맞춰야 연출이 이어진다.")]
            [Min(0f)] public float delayAfter = 0.3f;

            [Tooltip("★이 시간(초) 뒤에 부품을 Stop 한다. 0 = 스스로 끝나거나 리셋까지 유지.\n" +
                     "차지 오브처럼 「스스로 끝나지 않는」 부품이 다음 마디(합체 등)에 자리를 넘길 때 쓴다.\n" +
                     "실전에서는 큐/스킬 로직이 Stop 을 부르는 자리다.")]
            [Min(0f)] public float lifeTime;

            [Tooltip("★위치 프리셋 키. 비면 cueName 을 그대로 쓴다.\n" +
                     "같은 큐를 두 지점에서 부를 때만 채운다(예: paw_warp_out / paw_warp_in).\n" +
                     "여러 스텝이 같은 키를 쓰면 한 값으로 같이 움직인다(heal_fire → heal_charge).")]
            public string placementKey;

            [Tooltip("★탄착 위치 프리셋 키(발사체 전용). 비면 대상 루트(발밑)로 날아간다.\n" +
                     "3등신 캐릭터는 머리에 맞는 편이 자연스러워서 도착점도 프리셋으로 조절한다(예: heal_impact).")]
            public string impactPlacementKey;

            [Tooltip("★켜면 이 단계를 타이머 대신 <b>발사체의 착탄 시그널</b>로 잇는다.\n" +
                     "대응 발사체 = 같은 chainIndex 의 발사체(일반 단계는 본 발사체).\n" +
                     "비행 시간은 거리의 함수라 타이머(delayAfter)로는 원거리에서 어긋난다 — 착지 오라는 이걸 켤 것.")]
            public bool waitForImpact;

            [Tooltip("화면에 표시할 설명. 비우면 이름을 쓴다.")]
            public string label;
        }

        /// <summary>
        /// ManualStep = 키를 누를 때마다 한 단계씩(부품이 제 구간만 도는지 확인용).
        /// SkillCast  = <b>스킬 키 → 타겟 클릭 → 전 시퀀스 자동 재생</b>. 저스티스 프리뷰와 같은 흐름.
        /// </summary>
        public enum Mode { SkillCast, ManualStep }

        [Header("참조")]
        [SerializeField] private JcVfxCatalog catalog;
        [Tooltip("★위치 프리셋(스킬별) — 「앵커의 어디에 놓는가」의 정본. 각 스킬의 Presets 폴더 자산을 꽂는다.\n" +
                 "키는 위에서부터 순서대로 찾는다(접두 규약 덕에 실제 충돌은 없다).\n" +
                 "항목이 있으면 스텝의 offset/localOffset 은 무시된다. 플레이모드에서 고친 값은 그대로 저장된다(SO 자산).")]
        [SerializeField] private JcVfxPlacementPreset[] placements = new JcVfxPlacementPreset[0];
        [Tooltip("비워 두면 플레이 중 씬에서 자동으로 찾는다(프리뷰 씬은 유닛이 런타임에 생성되므로).")]
        [SerializeField] private Transform caster;
        [SerializeField] private Transform target;
        [Tooltip("자동 탐색 시 시전자로 삼을 유닛 이름 조각. 비면 첫 번째 유닛.")]
        [SerializeField] private string casterNameHint = "";
        [Tooltip("자동 탐색 시 대상으로 삼을 유닛 이름 조각. 비면 두 번째 유닛.")]
        [SerializeField] private string targetNameHint = "";

        /// <summary>이 스킬이 겨눌 수 있는 진영. 기획상 대상이 정해진 스킬은 잘못 찍히지 않게 막는다.</summary>
        public enum TargetSide { Any, Ally, Enemy }

        /// <summary>키 하나에 배정되는 시퀀스 묶음. 스킬×변종 조합마다 하나씩 둔다.</summary>
        [Serializable]
        public class Cast
        {
            [Tooltip("이 묶음을 시전할 키.")]
            public KeyCode key = KeyCode.E;
            [Tooltip("화면 표시용 이름.")]
            public string label;
            [Tooltip("이 묶음 전체에 적용할 변종. 각 단계가 Base 로 두면 이 값을 쓴다.")]
            public JcVfxCatalog.Variant variant = JcVfxCatalog.Variant.Base;
            [Tooltip("겨눌 수 있는 진영. Ally = 아군만, Enemy = 적만.")]
            public TargetSide targetSide = TargetSide.Any;
            public Step[] steps = new Step[0];
        }

        [Header("시퀀스")]
        [Tooltip("키마다 하나씩. 위에서부터 검사한다.")]
        [SerializeField] private Cast[] casts = new Cast[0];

        [Header("조작")]
        [SerializeField] private Mode mode = Mode.SkillCast;
        [Tooltip("ManualStep — 이 키로 한 단계씩 진행.\n" +
                 "★기본 None = 수동 진행 안 씀. 자동 진행(SkillCast)만 쓸 때 스킬 키를 하나라도 잡아먹지 않게 한다.")]
        [SerializeField] private KeyCode stepKey = KeyCode.None;
        [Tooltip("정리·취소. ★스킬 키와 겹치지 않게 둘 것 — 겹치면 그 스킬이 아예 시전되지 않는다.")]
        [SerializeField] private KeyCode resetKey = KeyCode.Z;
        [Tooltip("ManualStep 에서 마지막 다음에 처음으로 돌아간다.")]
        [SerializeField] private bool loop = true;
        [Tooltip("타겟 클릭 판정 최대 거리.")]
        [SerializeField] private float pickDistance = 200f;

        private int _index;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _message;
        private Coroutine _playing;
        private bool _awaitingTarget;
        private Cast _pending;
        private readonly List<Transform> _chainTargets = new List<Transform>(4);
        private readonly List<Transform> _chainExclude = new List<Transform>(2);
        private readonly List<VfxTarget> _chainVfxTargets = new List<VfxTarget>(4);

        /// <summary>이번 시전에서 쏜 발사체들의 착탄 플래그. 키 = chainIndex (본 발사체는 -1).</summary>
        private class ImpactFlag { public bool hit; }
        private readonly Dictionary<int, ImpactFlag> _impactFlags = new Dictionary<int, ImpactFlag>();
        private const float ImpactWaitTimeout = 8f;   // 시그널 유실 시 무한 대기 방지

        /// <summary>★실측 릴레이 — 이번 시전에서 차지를 실제로 스폰한 위치. 「투사체 출발 = 차지 위치」의 정본.</summary>
        private Vector3? _lastChargePos;

        private void Update()
        {
            // 카메라 조작(우클릭 홀드) 중에는 스킬 입력을 먹지 않는다 — JcFreeCamera 와 공존.
            if (Input.GetMouseButton(1)) return;

            if (Input.GetKeyDown(resetKey)) { ResetAll(); return; }

            if (mode == Mode.ManualStep)
            {
                if (Input.GetKeyDown(stepKey)) StepOnce();
                return;
            }

            // ── SkillCast ───────────────────────────────────────────
            for (int i = 0; i < casts.Length; i++)
            {
                if (!Input.GetKeyDown(casts[i].key)) continue;
                if (_playing != null) { StopCoroutine(_playing); _playing = null; }
                ResetAll();
                _pending = casts[i];
                _awaitingTarget = true;
                _message = $"[{CastName(_pending)}] {SideName(_pending.targetSide)}을(를) 클릭하세요 ({resetKey}=취소)";
                return;
            }

            if (_awaitingTarget && Input.GetMouseButtonDown(0))
            {
                BattleCharactor picked = PickUnderCursor();
                if (picked == null) { _message = "★대상을 못 찾았습니다 — 유닛을 클릭하세요"; return; }

                // ★진영 제한 — 아군 힐 스킬을 적에게, 공격 스킬을 아군에게 찍는 사고를 막는다.
                if (!SideAllowed(picked, _pending))
                {
                    _message = $"★{SideName(_pending.targetSide)}만 겨눌 수 있습니다 — {picked.name} 은(는) 대상 아님";
                    return;   // 대기 상태를 유지해 다시 찍을 수 있게 한다
                }

                _awaitingTarget = false;
                target = picked.transform;
                ResolveActors();
                ResolveChainTargets();   // 스테퍼가 「스킬 로직」 역할 — 연쇄 대상을 여기서 정한다
                _playing = StartCoroutine(PlayAll());
            }
        }

        private static bool SideAllowed(BattleCharactor c, Cast cast)
        {
            if (cast == null || cast.targetSide == TargetSide.Any) return true;
            bool isAlly = c.TeamType == TeamType.Player;
            return cast.targetSide == TargetSide.Ally ? isAlly : !isAlly;
        }

        private static string SideName(TargetSide s) =>
            s == TargetSide.Ally ? "아군" : s == TargetSide.Enemy ? "적" : "아무나";

        /// <summary>커서 아래의 BattleCharactor 를 집는다.</summary>
        private BattleCharactor PickUnderCursor()
        {
            var cam = Camera.main;
            if (cam == null) return null;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), pickDistance);
            return hits.OrderBy(h => h.distance)
                       .Select(h => h.collider.GetComponentInParent<BattleCharactor>())
                       .FirstOrDefault(c => c != null);
        }

        private static string CastName(Cast c) =>
            c == null ? "-" : (string.IsNullOrWhiteSpace(c.label) ? $"{c.key}" : c.label);

        /// <summary>
        /// 전 단계를 이어 재생한다. 잇는 방식은 두 가지 —
        /// 기본은 타이머(delayAfter), waitForImpact 단계는 <b>대응 발사체의 착탄 시그널</b>.
        /// 착탄 대기 단계 앞의 타이머는 건너뛴다(이중 대기로 가까운 거리에서 늦어지는 것 방지).
        /// </summary>
        private IEnumerator PlayAll()
        {
            Cast c = _pending;
            if (c == null || c.steps == null) { _playing = null; yield break; }
            _impactFlags.Clear();
            _lastChargePos = null;   // 시전마다 실측 릴레이 초기화

            for (int i = 0; i < c.steps.Length; i++)
            {
                _index = i;
                Step s = c.steps[i];

                if (s.waitForImpact)
                {
                    int key = s.spawnAt == SpawnAt.ChainTarget ? s.chainIndex : -1;
                    if (_impactFlags.TryGetValue(key, out ImpactFlag flag))
                    {
                        float t0 = Time.time;
                        yield return new WaitUntil(() => flag.hit || Time.time - t0 > ImpactWaitTimeout);
                        if (!flag.hit)
                            Debug.LogWarning($"[Stepper] '{s.cueName}' 착탄 대기 시간 초과({ImpactWaitTimeout}s) — 그냥 진행", this);
                    }
                    // 대응 발사체가 아예 없으면(연쇄 생략 등) 그냥 진행 — SpawnStep 의 생략 로직이 마저 거른다
                }

                SpawnStep(s, i + 1, c.steps.Length, c.variant);

                // 다음 단계가 착탄 대기면 타이머는 무의미 — 시그널이 이음새를 쥔다
                bool nextWaits = i + 1 < c.steps.Length && c.steps[i + 1].waitForImpact;
                if (s.delayAfter > 0f && !nextWaits) yield return new WaitForSeconds(s.delayAfter);
            }
            _index = c.steps.Length;
            _message = $"[{CastName(c)}] 완료 — {resetKey} 정리";
            _playing = null;
        }

        /// <summary>
        /// 프리뷰 씬은 유닛이 <b>런타임에 생성</b>되므로 에디터에서 미리 꽂을 수 없다.
        /// 비어 있으면 첫 스텝 때 씬에서 찾는다.
        /// </summary>
        private void ResolveActors()
        {
            if (caster != null && target != null) return;

            var units = FindObjectsByType<BattleCharactor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (units == null || units.Length == 0) return;

            Transform Pick(string hint, int fallbackIndex)
            {
                if (!string.IsNullOrWhiteSpace(hint))
                {
                    var hit = units.FirstOrDefault(u =>
                        u.name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0);
                    if (hit != null) return hit.transform;
                }
                return fallbackIndex < units.Length ? units[fallbackIndex].transform : null;
            }

            if (caster == null) caster = Pick(casterNameHint, 0);
            if (target == null) target = Pick(targetNameHint, units.Length > 1 ? 1 : 0);

            Debug.Log($"[Stepper] 배우 자동 탐색 — caster={(caster ? caster.name : "없음")} / target={(target ? target.name : "없음")}" +
                      $"  (씬 유닛 {units.Length}명)");
        }

        /// <summary>수동 모드 — 첫 묶음을 한 단계씩 밟는다.</summary>
        private void StepOnce()
        {
            if (catalog == null) { _message = "★대장이 비어 있습니다"; return; }
            Cast c = _pending ?? (casts.Length > 0 ? casts[0] : null);
            if (c == null || c.steps == null || c.steps.Length == 0) { _message = "★시퀀스가 비어 있습니다"; return; }
            _pending = c;
            ResolveActors();

            if (_index >= c.steps.Length)
            {
                if (!loop) { _message = $"끝 — {resetKey} 로 리셋"; return; }
                var keep = c; ResetAll(); _pending = keep;
            }

            // ManualStep 은 타겟 클릭 단계를 거치지 않으므로 여기서 한 번 정해 둔다.
            if (_chainTargets.Count == 0) ResolveChainTargets();

            Step s = c.steps[_index];
            _index++;
            SpawnStep(s, _index, c.steps.Length, c.variant);
        }

        /// <summary>한 단계를 실제로 생성·재생한다. 수동/자동 양쪽이 공유한다.</summary>
        private void SpawnStep(Step s, int ordinal, int total, JcVfxCatalog.Variant castVariant)
        {
            if (catalog == null) return;

            // 단계가 Base 면 묶음 변종을 따른다 — 바닐라/얼터를 묶음 단위로 갈아끼우기 위함.
            var want = s.variant == JcVfxCatalog.Variant.Base ? castVariant : s.variant;

            if (!catalog.TryGet(s.cueName, want, out JcVfxCatalog.Entry e) || e.prefab == null)
            {
                _message = $"[{ordinal}/{total}] {s.cueName} ({want}) — ★부품 없음";
                Debug.LogWarning($"[Stepper] '{s.cueName}' ({want}) 부품이 없습니다. 대장 확인 필요.", this);
                return;
            }

            Transform anchor;
            if (s.spawnAt == SpawnAt.ChainTarget)
            {
                if (s.chainIndex >= _chainTargets.Count)
                {
                    // 그 순번의 이웃이 없는 배치 — 정상이므로 조용히 건너뛴다.
                    _message = $"[{ordinal}/{total}] {s.cueName} — 연쇄 #{s.chainIndex} 없음(생략)";
                    return;
                }
                anchor = _chainTargets[s.chainIndex];
            }
            else anchor = s.spawnAt == SpawnAt.Target ? target
                        : s.spawnAt == SpawnAt.Self ? transform
                        : caster;
            // ★로컬 오프셋 = TransformPoint. 부품 내부(PawForYouVfx.CasterAnchor 등)가 로컬로 잡는데
            //   여기서 월드로 더하면, 캐릭터가 전투에서 적을 향해 회전해 있을 때 위치가 어긋난다.
            // ★위치 결정 — 우선순위: ① 호출자 지정(placement 자산) ② 부품 프리셋의 위치 항목 ③ 스텝 오프셋(레거시).
            //   ①이 이기는 이유: 같은 부품을 두 지점에서 부르는 경우(paw_warp, LFL 양손)는 호출자만 안다.
            string pkey = string.IsNullOrWhiteSpace(s.placementKey) ? s.cueName : s.placementKey.Trim();
            bool hasPlacement = TryGetPlacement(pkey, out JcVfxPlacementPreset.Entry pe);
            Vector3 pos;
            if (hasPlacement)
                pos = JcVfxPlacementPreset.Resolve(anchor != null ? anchor : transform, pe);
            else if (TryPartPresetSpawn(e.prefab, anchor != null ? anchor : transform, out Vector3 partPos))
                pos = partPos;
            else
                pos = PointOn(anchor, s.offset, s.localOffset);

            GameObject go = Instantiate(e.prefab, pos, Quaternion.identity);
            _spawned.Add(go);

            // 부품은 「받는 구멍」만 갖는다 — 여기서는 스테퍼가 스킬 로직 자리를 대신 채운다.
            // ★연쇄 마디는 본 대상에서 이웃으로 튄다 — 출발점이 시전자가 아니라 본 대상이다.
            Transform playOrigin = s.spawnAt == SpawnAt.ChainTarget
                                 ? (target != null ? target : transform)
                                 : (caster != null ? caster : transform);
            Transform playTarget = s.spawnAt == SpawnAt.ChainTarget
                                 ? anchor
                                 : (target != null ? target : transform);

            var vfx = go.GetComponent<VfxEffect>();
            if (vfx != null)
            {
                // 실측 릴레이 — 차지가 실제로 뜬 자리를 기억해 두면 투사체가 이어받는다.
                if (vfx is ChargeOrbVfx) _lastChargePos = pos;

                // 다중 대상을 받는 부품(통짜 오케스트레이터 등)에는 결정된 목록을 그대로 넘긴다.
                if (_chainTargets.Count > 0) vfx.SetTargets(BuildChainVfxTargets());

                // ★발사체는 Play(origin,target) 이 Show(origin.position) 으로 생성 위치를 덮어쓴다.
                //   그대로 두면 단계에 적어 둔 오프셋(손 높이 등)이 무시되고 발밑에서 출발한다.
                //   그래서 출발점만 여기서 직접 넣고, 도착점은 앵커를 그대로 쓴다.
                if (vfx is ProjectileVfx proj)
                {
                    // ★출발점은 앵커가 아니라 「쏘는 쪽」이다. 연쇄 마디의 앵커는 도착점(이웃)이므로
                    //   여기서 앵커를 쓰면 도착점에서 생겨 거리 0으로 날아간다.
                    Vector3 from;
                    if (hasPlacement)
                        from = JcVfxPlacementPreset.Resolve(playOrigin != null ? playOrigin : transform, pe);
                    else if (TryProjectilePresetSpawn(proj, playOrigin != null ? playOrigin : transform, out Vector3 pf))
                        from = pf;
                    else
                        from = PointOn(playOrigin, s.offset, s.localOffset);

                    // 도착점 — 우선순위: ① 호출자 탄착 키 ② 투사체 프리셋의 탄착점 ③ 대상 루트(발밑).
                    //   탄착은 대상측 — 월드 축 해석(대상 회전에 따라 반전되지 않게).
                    Vector3 to = playTarget != null ? playTarget.position : from;
                    if (!string.IsNullOrWhiteSpace(s.impactPlacementKey) &&
                        TryGetPlacement(s.impactPlacementKey.Trim(), out JcVfxPlacementPreset.Entry ie))
                        to = JcVfxPlacementPreset.ResolveWorld(playTarget != null ? playTarget : transform, ie);
                    else if (proj.Preset != null && playTarget != null)
                    {
                        var pt = proj.Preset.TransformSource;
                        to = JcVfxPlacementPreset.ResolveWorld(playTarget, pt.impactSocketName, pt.impactOffset);
                    }

                    proj.Show(from);
                    proj.Launch(to);

                    // 착탄 플래그 등록 — 뒤의 waitForImpact 단계가 이 시그널로 이어진다.
                    int impactKey = s.spawnAt == SpawnAt.ChainTarget ? s.chainIndex : -1;
                    var flag = new ImpactFlag();
                    _impactFlags[impactKey] = flag;
                    System.Action<ProjectileVfx> h = null;
                    h = _ => { proj.OnImpact -= h; flag.hit = true; };
                    proj.OnImpact += h;
                }
                else vfx.Play(playOrigin, playTarget);

                if (s.lifeTime > 0f) StartCoroutine(StopAfter(vfx, s.lifeTime));
            }
            else go.GetComponent<ISkillEffectBehaviour>()?.Play(null);

            string label = string.IsNullOrWhiteSpace(s.label) ? s.cueName : s.label;
            _message = $"[{ordinal}/{total}] {label}  ({e.prefab.name})";
            Debug.Log($"[Stepper] {_message}");
        }

        /// <summary>스킬별 위치 프리셋들에서 키를 찾는다. 위에서부터 첫 일치가 이긴다.</summary>
        private bool TryGetPlacement(string key, out JcVfxPlacementPreset.Entry entry)
        {
            for (int i = 0; i < placements.Length; i++)
                if (placements[i] != null && placements[i].TryGet(key, out entry)) return true;
            entry = null;
            return false;
        }

        /// <summary>
        /// ★부품 프리셋이 위치를 소유하는 경우(우선순위 ②).
        /// 차지 오브 = 「발사 시작점」(시전자 기준) / 착지 오라 = 「착지점 오프셋」(스텝 앵커 = 대상 기준).
        /// 호출자 지정(①)이 없을 때만 여기로 온다.
        /// </summary>
        private bool TryPartPresetSpawn(GameObject prefab, Transform stepAnchor, out Vector3 pos)
        {
            pos = default;
            if (prefab == null) return false;

            var charge = prefab.GetComponent<ChargeOrbVfx>();
            if (charge != null && charge.Preset != null)
            {
                var t = charge.Preset.TransformSource;
                pos = JcVfxPlacementPreset.Resolve(caster != null ? caster : transform, t.spawnSocketName, t.spawnOffset);
                return true;
            }

            var orbit = prefab.GetComponent<HealOrbitVfx>();
            if (orbit != null && orbit.Preset != null)
            {
                // 착지 오라도 대상측 — 월드 축 해석.
                pos = JcVfxPlacementPreset.ResolveWorld(stepAnchor, null, orbit.Preset.TransformSource.landOffset);
                return true;
            }

            // ★PawForYou 계열 — 발 좌표의 정본 = P1_PawSprite 프리셋.
            //   앵커가 대상이면 재등장(머리 위) 좌표, 아니면 등장(시전자) 좌표.
            //   paw_warp 가 출발(시전자)/도착(대상) 두 앵커로 들어와도 이 규칙 하나로 기둥이 발과 겹친다.
            var pawFx = prefab.GetComponent<PawForYouVfx>();
            if (pawFx != null && pawFx.PawPreset != null)
            {
                var pp = pawFx.PawPreset.TransformSource;   // 위치는 트랜스폼 — 따름 규칙(변종→Basic)
                bool atTarget = stepAnchor != null && stepAnchor == target;
                // 대상측(머리 위)은 월드 축, 시전자측(등장)은 회전 따름 — Resolve 용도 구분 참조.
                pos = atTarget
                    ? JcVfxPlacementPreset.ResolveWorld(stepAnchor, pp.headSocketName, pp.headOffset)
                    : JcVfxPlacementPreset.Resolve(stepAnchor, pp.spawnSocketName, pp.spawnOffset);
                return true;
            }
            return false;
        }

        /// <summary>
        /// ★투사체 시작점 — 「차지 위치를 시작점으로」 토글 처리.
        /// 실측 릴레이가 최우선: 이번 시전에서 차지를 실제로 스폰한 위치가 있으면 그걸 쓴다
        /// (호출자 오버라이드로 차지가 다른 곳에 떴어도 자동 일치). 없으면 chargeRef 프리셋 값.
        /// </summary>
        private bool TryProjectilePresetSpawn(ProjectileVfx proj, Transform origin, out Vector3 pos)
        {
            pos = default;
            var p = proj != null ? proj.Preset : null;
            if (p == null) return false;
            var t = p.TransformSource;

            if (t.useChargeOrbPosition)
            {
                if (_lastChargePos.HasValue) { pos = _lastChargePos.Value; return true; }
                var cs = p.chargeRef != null ? p.chargeRef.TransformSource : null;
                if (cs == null) return false;   // 차지도 실측도 없으면 레거시 폴백으로
                pos = JcVfxPlacementPreset.Resolve(origin, cs.spawnSocketName, cs.spawnOffset);
                return true;
            }
            pos = JcVfxPlacementPreset.Resolve(origin, t.spawnSocketName, t.spawnOffset);
            return true;
        }

        /// <summary>lifeTime 경과 후 부품을 정지 — 리셋으로 이미 파괴됐으면 아무것도 안 한다.</summary>
        private IEnumerator StopAfter(VfxEffect vfx, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (vfx != null) vfx.Stop();
        }

        /// <summary>
        /// 기준 트랜스폼에 오프셋을 얹은 월드 좌표.
        /// localOffset = 대상의 로컬 축(TransformPoint) — 부품 내부가 로컬로 앵커를 잡을 때 맞춘다.
        /// </summary>
        private Vector3 PointOn(Transform t, Vector3 offset, bool local)
        {
            if (t == null) return transform.position + offset;
            return local ? t.TransformPoint(offset) : t.position + offset;
        }

        /// <summary>씬의 아군 전원. 연쇄 판정에 넘길 후보 풀이다.</summary>
        private Transform[] CollectAllies()
        {
            return UnityEngine.Object.FindObjectsByType<BattleCharactor>(
                       FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                   .Where(c => c.TeamType == TeamType.Player)
                   .Select(c => c.transform)
                   .ToArray();
        }

        /// <summary>
        /// ★스킬 로직 대역 — 본 대상 기준 십자 인접 아군을 고른다(시전자·본 대상 제외).
        /// 실전에서는 격자 인덱스를 아는 스킬이 이 자리를 맡고, 여기 결과 대신 그쪽 결과가 주입된다.
        /// 규칙 자체는 <see cref="VfxGridAdjacency"/> 한 곳에만 있다.
        /// </summary>
        private void ResolveChainTargets()
        {
            _chainExclude.Clear();
            _chainExclude.Add(caster);
            _chainExclude.Add(target);

            VfxGridAdjacency.ResolveCross(
                target != null ? target.position : transform.position,
                CollectAllies(), _chainTargets, _chainExclude);

            if (_chainTargets.Count > 0)
                Debug.Log($"[Stepper] 연쇄 대상 {_chainTargets.Count}: " +
                          string.Join(", ", _chainTargets.Select(t => t.name)));
        }

        private IReadOnlyList<VfxTarget> BuildChainVfxTargets()
        {
            _chainVfxTargets.Clear();
            foreach (var t in _chainTargets) _chainVfxTargets.Add(VfxTarget.Of(t));
            return _chainVfxTargets;
        }

        private void ResetAll()
        {
            if (_playing != null) { StopCoroutine(_playing); _playing = null; }
            _awaitingTarget = false;
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] == null) continue;
                // ★파괴 전에 Stop — 부품이 씬 루트에 만든 것(트레일 팔로워 등)의 방출을 끈다.
                //   이거 없이 Destroy 만 하면 팔로워가 공중에서 계속 반짝이는 고아가 된다(260805 실증).
                _spawned[i].GetComponent<VfxEffect>()?.Stop();
                Destroy(_spawned[i]);
            }
            _spawned.Clear();
            _chainTargets.Clear();
            _impactFlags.Clear();
            _lastChargePos = null;
            _index = 0;
            _message = "리셋";
        }

        private void OnGUI()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            float y = 12f;

            if (mode == Mode.SkillCast)
            {
                GUI.Label(new Rect(12, y, 900, 22),
                    $"<b>스킬 키</b> → <b>좌클릭</b> 타겟 선택   <b>{resetKey}</b> 정리", style);
                y += 22f;
                for (int i = 0; i < casts.Length; i++)
                {
                    bool cur = _pending == casts[i];
                    string side = casts[i].targetSide == TargetSide.Any ? "" : $"  <i>[{SideName(casts[i].targetSide)}]</i>";
                    string line = $"  <b>{casts[i].key}</b>  {CastName(casts[i])}{side}";
                    GUI.Label(new Rect(12, y, 900, 20),
                        cur ? $"<color=#7fd0ff>{line}</color>" : $"<color=#b0b0b0>{line}</color>", style);
                    y += 20f;
                }
            }
            else
            {
                GUI.Label(new Rect(12, y, 900, 22),
                    $"<b>{stepKey}</b> 다음 단계   <b>{resetKey}</b> 리셋" + (loop ? "   (끝나면 자동 순환)" : ""), style);
                y += 22f;
            }

            if (_awaitingTarget)
            {
                GUI.Label(new Rect(12, y, 900, 22),
                    $"<color=#ffd479><b>타겟 선택 대기</b> — {SideName(_pending != null ? _pending.targetSide : TargetSide.Any)}을(를) 클릭하세요</color>",
                    style);
                y += 22f;
            }

            if (!string.IsNullOrEmpty(_message))
            {
                GUI.Label(new Rect(12, y, 720, 22), $"<color=#7fd0ff>{_message}</color>", style);
                y += 22f;
            }

            var cur2 = _pending;
            if (cur2 == null || cur2.steps == null) return;
            for (int i = 0; i < cur2.steps.Length; i++)
            {
                bool done = i < _index;
                string mark = done ? "✔" : "·";
                string name = string.IsNullOrWhiteSpace(cur2.steps[i].label) ? cur2.steps[i].cueName : cur2.steps[i].label;
                GUI.Label(new Rect(24, y, 900, 20),
                    done ? $"<color=#8f8f8f>{mark} {name}</color>" : $"{mark} {name}", style);
                y += 20f;
            }
        }
    }
}
