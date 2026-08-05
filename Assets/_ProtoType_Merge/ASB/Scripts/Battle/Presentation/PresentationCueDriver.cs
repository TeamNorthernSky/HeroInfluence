using System.Collections.Generic;
using UnityEngine;

/// <summary>State 게이트의 동작 단계. 진단 → 자산 교정 → 강제 순으로 전환한다.</summary>
public enum CueGateMode
{
    /// <summary>불일치를 경고만 하고 발화는 그대로 허용한다(도입 단계). 회귀 없이 불일치 목록을 수집한다.</summary>
    Diagnostic,

    /// <summary>불일치 시 발화를 차단한다. 자산의 AnimationStateName을 전부 교정한 뒤 전환한다.</summary>
    Enforce
}

/// <summary>
/// 유닛 로컬 연출 Cue 드라이버. 두 가지 일을 한다.
///
/// ① <b>State 게이트</b> — Cue가 <see cref="PresentationRuntimeContext"/>에 등록되어 있다는 이유만으로
///    발화하지 않게 막는다. 컨텍스트는 <c>PlayState</c> <b>이전에</b> 새 Cue 표로 교체되므로
///    (SkillPresentationDirector.PlayCuePhaseRoutine), 이전 상태가 블렌드 중이거나 늦은 Animation Event를
///    내면 새 페이즈의 Cue가 잘못 발화한다. 기대 state에 실제로 진입했는지 확인해 그것을 막는다.
///
/// ② <b>시간 기반 발화</b> — <c>Timing != ClipEvent</c>인 Cue를 상태 경과 시간에 맞춰 발화한다.
///    클립에 <c>AniEvent_PresentationCue</c>가 없는 클립(파이터 계열)을 데이터로 메우는 경로다.
///
/// 하는 일은 "이름을 외치는 것" 하나뿐이며, 그 뒤(이펙트/사운드/앵커/수명)는 전부 기존 경로가 처리한다.
/// 배속·홀드는 Animator의 시간을 <b>읽기만</b> 하므로 자동으로 따라간다 — 별도 곱셈이 없다.
///
/// JcCueTimingDriver(전역 임시표 대행)의 판정 로직을 유닛 로컬로 이식한 것이다.
/// 전역 표와 병행 동작해도 ③ 억제 규칙 덕분에 이중 발화가 없다.
/// </summary>
[DisallowMultipleComponent]
public class PresentationCueDriver : MonoBehaviour
{
    /// <summary>
    /// 게이트 단계. 기본은 Diagnostic — 켜자마자 발화를 막으면 자산의 AnimationStateName이
    /// 실제 재생 state와 다른 경우 지금 동작하는 Cue가 조용히 침묵한다.
    /// 진단 로그로 불일치를 전부 교정한 뒤 Enforce로 바꾼다.
    /// </summary>
    public static CueGateMode GateMode = CueGateMode.Diagnostic;

    /// <summary>데이터 시각 발화를 콘솔에 남긴다. 타이밍을 맞출 때만 켠다.</summary>
    public static bool LogFire;

    /// <summary>클립 이벤트가 Cue를 부를 때 쓰는 함수명. 억제(이중 발화 방지) 판정 기준.</summary>
    private const string CueFunctionName = "AniEvent_PresentationCue";

    /// <summary>시간이 되돌아갔다고 볼 여유값. 부동소수 오차로 재진입이 오탐되지 않게 한다.</summary>
    private const float ReenterEpsilon = 0.0001f;

    private PresentationRuntimeContext _ctx;
    private CharactorAnimationController _anim;
    private BattleCharactor _charactor;

    // ── 이번 state 진입 추적 ──
    private int _trackedActionId;
    private int _trackedStateHash;
    private float _prevSeconds = -1f;
    private bool _clipResolved;

    /// <summary>이번 진입에서 이미 발화한 데이터 시각 Cue. 한 진입당 한 번만 발화하기 위한 것.</summary>
    private readonly HashSet<string> _firedThisEntry = new HashSet<string>();

    /// <summary>재생 중 클립이 같은 이름의 진짜 이벤트를 가져 이번 진입에서 건너뛸 Cue 이름.</summary>
    private readonly HashSet<string> _suppressedThisEntry = new HashSet<string>();

    /// <summary>이번 진입에서 이미 경고한 Cue 이름. 프레임마다 같은 경고가 쌓이는 것을 막는다.</summary>
    private readonly HashSet<string> _warnedThisEntry = new HashSet<string>();

    private readonly List<AnimatorClipInfo> _clipInfoBuffer = new List<AnimatorClipInfo>(4);

    /// <summary>클립이 가진 Cue 이름 집합(정규화). clip.events는 호출마다 배열을 새로 만들므로 반드시 캐시한다.</summary>
    private static readonly Dictionary<AnimationClip, HashSet<string>> ClipCueCache =
        new Dictionary<AnimationClip, HashSet<string>>();

    private PresentationRuntimeContext Ctx =>
        _ctx != null ? _ctx : (_ctx = GetComponent<PresentationRuntimeContext>());

    private CharactorAnimationController AnimController =>
        _anim != null ? _anim : (_anim = GetComponent<CharactorAnimationController>());

    private BattleCharactor Charactor =>
        _charactor != null ? _charactor : (_charactor = GetComponent<BattleCharactor>());

    private Animator TargetAnimator => AnimController != null ? AnimController.Animator : null;

    // ──────────────────────────────────────────────────────────────
    // ① State 게이트 — 프리젠터가 발화 직전에 묻는다
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 이 Cue를 지금 발화해도 되는지. Animator를 <b>그 시점에</b> 읽으므로 한 프레임 묵은 값을 쓰지 않는다.
    /// Diagnostic 단계에서는 불일치를 경고만 하고 true를 반환한다.
    /// </summary>
    public bool IsCueAllowed(string normalizedCueName)
    {
        PresentationRuntimeContext ctx = Ctx;

        // 연출 컨텍스트가 없으면 게이트할 근거가 없다(레거시 경로 등). 기존 동작 유지.
        if (ctx == null || !ctx.HasValidContext)
        {
            return true;
        }

        int expected = ctx.ExpectedStateHash;

        // state를 지정하지 않은 자산(AnimationStateName 비어 있음)은 게이트 대상이 아니다.
        // 0을 차단하면 그런 자산의 Cue가 전부 죽는다.
        if (expected == 0)
        {
            return true;
        }

        Animator animator = TargetAnimator;
        if (animator == null)
        {
            return true;
        }

        if (MatchesExpectedState(animator, expected))
        {
            return true;
        }

        WarnStateMismatch(ctx, animator, normalizedCueName, expected);
        return GateMode != CueGateMode.Enforce;
    }

    /// <summary>
    /// 기대 state에 있는지. 전환 중이면 <b>들어오는</b> 상태도 인정한다 —
    /// 블렌드 초반(0.02초 등)에 놓인 이른 Cue를 놓치지 않기 위해서다.
    ///
    /// fullPathHash와 shortNameHash를 모두 비교한다. ExpectedStateHash는
    /// Animator.StringToHash(AnimationStateName)로 만들어지는데, 그 값이 짧은 이름("ClassSkill_1")인지
    /// 전체 경로("Base Layer.ClassSkill_1")인지가 자산마다 다를 수 있다. 둘 다 받아주면 어느 표기든 맞는다.
    /// </summary>
    private static bool MatchesExpectedState(Animator animator, int expected)
    {
        if (Matches(animator.GetCurrentAnimatorStateInfo(0), expected))
        {
            return true;
        }

        return animator.IsInTransition(0) && Matches(animator.GetNextAnimatorStateInfo(0), expected);
    }

    private static bool Matches(AnimatorStateInfo info, int expected)
    {
        return StateHashMatches(info.fullPathHash, info.shortNameHash, expected);
    }

    /// <summary>
    /// 기대 해시가 현재 state의 해시와 맞는지. fullPath와 shortName 둘 다 인정한다(위 주석 참조).
    /// AnimatorStateInfo를 테스트에서 만들 수 없으므로 순수 함수로 분리했다.
    /// </summary>
    public static bool StateHashMatches(int fullPathHash, int shortNameHash, int expected)
    {
        return fullPathHash == expected || shortNameHash == expected;
    }

    /// <summary>
    /// 이 프레임에 발화 시각을 지나쳤는지. 프레임 사이 구간 (prevSeconds, seconds] 에 target이 들어오면 한 번 발화한다.
    /// 재진입 직후 prevSeconds는 -1이므로 이미 지난 시각의 Cue도 놓치지 않는다.
    /// target이 음수면(길이 불명 등) 발화하지 않는다.
    /// </summary>
    public static bool CrossedFireTime(float prevSeconds, float seconds, float target)
    {
        return target >= 0f && prevSeconds < target && target <= seconds;
    }

    private void WarnStateMismatch(PresentationRuntimeContext ctx, Animator animator, string cueName, int expected)
    {
        string key = cueName ?? string.Empty;
        if (!_warnedThisEntry.Add(key))
        {
            return;
        }

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        string action = GateMode == CueGateMode.Enforce ? "차단" : "허용(진단 단계)";
        Debug.LogWarning(
            $"[PresentationCueDriver] {name}: Cue '{cueName}' 의 기대 state가 아님 → {action}.\n" +
            $"  연출: {ctx.DebugLabel}\n" +
            $"  기대 hash: {expected} / 현재 state hash: full={current.fullPathHash} short={current.shortNameHash}\n" +
            $"  → 연출 자산의 AnimationStateName이 실제 재생되는 state와 다를 수 있습니다.",
            this);
    }

    // ──────────────────────────────────────────────────────────────
    // ② 시간 기반 발화
    // ──────────────────────────────────────────────────────────────

    private void Update()
    {
        PresentationRuntimeContext ctx = Ctx;
        Animator animator = TargetAnimator;
        if (ctx == null || animator == null || !ctx.HasValidContext)
        {
            ResetTracking();
            return;
        }

        // 전환 중에는 들어오는 상태를 기준으로 본다 — 이른 시각의 Cue를 놓치지 않기 위해서다.
        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        bool useNext = false;
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
            if (next.length > 0f)
            {
                info = next;
                useNext = true;
            }
        }

        int hash = info.fullPathHash;
        float length = info.length;

        // 배속(animator.speed)이 normalizedTime 진행에 이미 반영되어 있다. 여기서 곱하지 않는다.
        // 홀드(프리즈) 중에는 normalizedTime이 멈추므로 Cue도 자동으로 멈춘다.
        float seconds = Mathf.Max(0f, info.normalizedTime) * length;

        bool reentered = hash != _trackedStateHash
                         || ctx.CurrentActionInstanceId != _trackedActionId
                         || seconds + ReenterEpsilon < _prevSeconds;
        if (reentered)
        {
            _trackedStateHash = hash;
            _trackedActionId = ctx.CurrentActionInstanceId;
            _prevSeconds = -1f;   // 첫 관측이 이미 지난 시각이어도 놓치지 않도록
            _clipResolved = false;
            _firedThisEntry.Clear();
            _suppressedThisEntry.Clear();
            _warnedThisEntry.Clear();
        }

        if (!_clipResolved)
        {
            ResolveSuppression(animator, useNext);
            _clipResolved = true;
        }

        // ★게이트 — 기대 state가 아니면 데이터 시각 발화를 하지 않는다.
        //   여기서는 Diagnostic 여부와 무관하게 건너뛴다. 시간 기반 경로는 신규이므로
        //   회귀 대상이 없고, 잘못된 state에서 시각을 해석하는 것 자체가 무의미하다.
        int expected = ctx.ExpectedStateHash;
        if (expected != 0 && !Matches(info, expected))
        {
            _prevSeconds = seconds;
            return;
        }

        IReadOnlyList<RuntimeCue> cues = ctx.OrderedCues;
        for (int i = 0; i < cues.Count; i++)
        {
            RuntimeCue cue = cues[i];
            if (cue == null || !cue.IsDataTimed)
            {
                continue;
            }

            string cueName = cue.NormalizedCueName;
            if (string.IsNullOrEmpty(cueName)
                || _firedThisEntry.Contains(cueName)
                || _suppressedThisEntry.Contains(cueName))
            {
                continue;
            }

            float target = cue.ResolveFireSeconds(length);
            if (CrossedFireTime(_prevSeconds, seconds, target))
            {
                _firedThisEntry.Add(cueName);

                // ★여기가 전부다 — 클립 이벤트가 했을 호출을 그대로 대신한다.
                Charactor?.AniEvent_PresentationCue(cueName);

                if (LogFire)
                {
                    Debug.Log(
                        $"[PresentationCueDriver] {name} / '{cueName}' @ {target:F3}s (관측 {seconds:F3}s, 클립 {length:F3}s)",
                        this);
                }
            }
        }

        _prevSeconds = seconds;
    }

    private void ResetTracking()
    {
        _trackedActionId = 0;
        _trackedStateHash = 0;
        _prevSeconds = -1f;
        _clipResolved = false;
    }

    // ──────────────────────────────────────────────────────────────
    // ③ 억제 — 클립이 진짜 이벤트를 가지면 데이터 시각이 물러난다
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 이번 진입에서 재생되는 클립을 조사해, 같은 이름의 AniEvent_PresentationCue를 이미 가진 Cue를 억제 목록에 넣는다.
    /// 이것이 있어서 클립 이벤트와 데이터 시각이 <b>Cue 이름 단위로</b> 공존한다 — 한 페이즈 안에서 섞어도 된다.
    /// </summary>
    private void ResolveSuppression(Animator animator, bool useNext)
    {
        _suppressedThisEntry.Clear();

        _clipInfoBuffer.Clear();
        if (useNext)
        {
            animator.GetNextAnimatorClipInfo(0, _clipInfoBuffer);
        }
        else
        {
            animator.GetCurrentAnimatorClipInfo(0, _clipInfoBuffer);
        }

        for (int i = 0; i < _clipInfoBuffer.Count; i++)
        {
            AnimationClip clip = _clipInfoBuffer[i].clip;
            if (clip == null)
            {
                continue;
            }

            foreach (string cueName in GetClipCues(clip))
            {
                _suppressedThisEntry.Add(cueName);
            }
        }
    }

    /// <summary>클립이 가진 Cue 이름 집합(정규화). clip.events는 호출마다 배열을 새로 만들므로 반드시 캐시한다.</summary>
    private static HashSet<string> GetClipCues(AnimationClip clip)
    {
        if (ClipCueCache.TryGetValue(clip, out HashSet<string> cached))
        {
            return cached;
        }

        var set = new HashSet<string>();
        AnimationEvent[] events = clip.events;
        for (int i = 0; i < events.Length; i++)
        {
            if (events[i].functionName != CueFunctionName)
            {
                continue;
            }

            string param = events[i].stringParameter;
            if (!string.IsNullOrWhiteSpace(param))
            {
                set.Add(param.Trim().ToLowerInvariant());
            }
        }

        ClipCueCache[clip] = set;
        return set;
    }
}
