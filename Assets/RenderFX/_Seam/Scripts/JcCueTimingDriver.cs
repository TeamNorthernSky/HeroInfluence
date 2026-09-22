using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 클립 이벤트 대행 드라이버 — 애니 상태의 경과 시간을 보고 <c>AniEvent_PresentationCue</c>를 대신 부른다.
    ///
    /// ═══ 이 컴포넌트가 하는 일 (그리고 하지 않는 일) ═══
    /// 하는 일: **이름을 부르는 것 하나뿐이다.**
    ///   원래 애니메이션 클립에 심긴 이벤트가 `AniEvent_PresentationCue("impact")`를 부르는데,
    ///   파이터 계열 클립에는 그 이벤트가 없다. 그래서 표를 보고 같은 시각에 같은 이름을 부른다.
    /// 하지 않는 일: 그 뒤의 모든 것.
    ///   어떤 이펙트를 띄울지, 어디에 띄울지, 시전자·타깃이 누구인지는 **전부 ASB가 이미 준비해 둔 것**을
    ///   그대로 쓴다(BattleManager가 페이즈 진입 시 Cue 표를 유닛에 등록해 둔다).
    ///   전투 로직·피해·타이밍에는 일절 관여하지 않는다 — 이 드라이버를 꺼도 전투 결과는 똑같다.
    ///
    /// ═══ 스스로 물러나는 조건 ═══
    /// 재생 중인 클립에 같은 이름의 `AniEvent_PresentationCue` 이벤트가 **이미 있으면 부르지 않는다.**
    /// 즉 ASB가 클립에 진짜 이벤트를 심는 날, 이 드라이버는 아무 작업 없이 자동으로 은퇴하고
    /// 이중 발화도 일어나지 않는다.
    ///
    /// ═══ 배치 ═══
    /// 씬에 오브젝트를 두지 않는다. 표(Resources/JC_CueTiming)가 있을 때만 런타임에 스스로 생성된다.
    /// 표가 없으면 아무것도 하지 않으므로, 파일을 지우는 것만으로 완전히 비활성화된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcCueTimingDriver : MonoBehaviour
    {
        /// <summary>표의 Resources 경로. 이 이름의 에셋이 없으면 드라이버 자체가 생성되지 않는다.</summary>
        public const string ResourcePath = "JC_CueTiming";

        /// <summary>클립 이벤트가 Cue를 부를 때 쓰는 함수 이름. 자동 회피 판정 기준.</summary>
        const string CueFunctionName = "AniEvent_PresentationCue";

        /// <summary>유닛 목록 재수집 주기(초). 전투 중 스폰/소멸을 따라가기 위한 값.</summary>
        const float RefreshInterval = 0.5f;

        private JcCueTimingTable table;
        private float refreshTimer;

        private readonly List<Tracked> tracked = new List<Tracked>();
        private readonly List<AnimatorClipInfo> clipInfoBuffer = new List<AnimatorClipInfo>(4);

        /// <summary>클립이 어떤 Cue 이름을 자체 이벤트로 갖고 있는지 캐시. clip.events는 호출마다 배열을 새로 만든다.</summary>
        private static readonly Dictionary<AnimationClip, HashSet<string>> clipCueCache =
            new Dictionary<AnimationClip, HashSet<string>>();

        /// <summary>표에 있으나 한 번도 등장하지 않은 상태(오타 탐지용).</summary>
        private readonly HashSet<string> seenStates = new HashSet<string>();

        private class Tracked
        {
            public PresentationRuntimeContext ctx;
            public BattleCharactor charactor;
            public Animator animator;

            public int actionId;          // 현재 추적 중인 액션
            public int stateHash;         // 현재 추적 중인 상태
            public float prevSeconds;     // 직전 프레임의 상태 경과 초
            public bool[] fired;          // 표의 줄별 발화 여부
            public bool[] suppressed;     // 클립이 자체 이벤트를 가져 이번 상태에서 건너뛸 줄
            public bool clipResolved;     // 이번 상태 진입에서 클립 판정을 마쳤는지
            public readonly HashSet<string> clipNames = new HashSet<string>();   // 지금 재생 중인 클립 이름
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var table = Resources.Load<JcCueTimingTable>(ResourcePath);
            if (table == null || table.entries == null || table.entries.Length == 0)
            {
                return;   // 표가 없으면 존재하지 않는 것과 같다
            }

            // ★HideFlags.DontSave 금지 — 「플레이 종료 시에도 파괴하지 않음」이 포함돼 있어
            //   오브젝트가 에디트 모드로 새어 나가고 플레이할 때마다 누적된다.
            if (FindAnyObjectByType<JcCueTimingDriver>() != null) return;   // 중복 방지

            var go = new GameObject("[JC] CueTimingDriver");
            DontDestroyOnLoad(go);
            go.AddComponent<JcCueTimingDriver>().table = table;
        }

        private void Update()
        {
            if (table == null) return;

            refreshTimer -= Time.unscaledDeltaTime;
            if (refreshTimer <= 0f)
            {
                refreshTimer = RefreshInterval;
                Refresh();
            }

            for (int i = tracked.Count - 1; i >= 0; i--)
            {
                Tracked t = tracked[i];
                if (t.ctx == null || t.charactor == null || t.animator == null)
                {
                    tracked.RemoveAt(i);
                    continue;
                }
                Step(t);
            }
        }

        /// <summary>살아 있는 연출 컨텍스트를 다시 모은다. 기존 추적 상태는 그대로 이어 간다.</summary>
        private void Refresh()
        {
            var found = FindObjectsByType<PresentationRuntimeContext>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < found.Length; i++)
            {
                PresentationRuntimeContext ctx = found[i];
                bool already = false;
                for (int k = 0; k < tracked.Count; k++)
                {
                    if (tracked[k].ctx == ctx) { already = true; break; }
                }
                if (already) continue;

                var charactor = ctx.GetComponent<BattleCharactor>();
                var animator = ctx.GetComponentInChildren<Animator>();
                if (charactor == null || animator == null) continue;

                tracked.Add(new Tracked
                {
                    ctx = ctx,
                    charactor = charactor,
                    animator = animator,
                    fired = new bool[table.entries.Length],
                    suppressed = new bool[table.entries.Length],
                });
            }
        }

        private void Step(Tracked t)
        {
            // 연출 액션 중이 아니면 아무것도 하지 않는다(Cue 표가 등록돼 있지 않다).
            if (!t.ctx.HasValidContext)
            {
                t.actionId = 0;
                t.stateHash = 0;
                return;
            }

            // 현재(또는 전환 중이라면 다음) 상태의 경과 시간을 읽는다.
            AnimatorStateInfo info = t.animator.GetCurrentAnimatorStateInfo(0);
            bool useNext = false;
            if (t.animator.IsInTransition(0))
            {
                AnimatorStateInfo next = t.animator.GetNextAnimatorStateInfo(0);
                // 전환 중에는 들어오는 상태를 기준으로 본다 — 이른 시각(0.02초 등)의 Cue를 놓치지 않기 위해서다.
                if (next.length > 0f) { info = next; useNext = true; }
            }

            int hash = info.fullPathHash;
            float seconds = Mathf.Max(0f, info.normalizedTime) * info.length;

            // 새 페이즈의 Cue 표가 먼저 등록되고 Animator가 뒤늦게 전환될 수 있다.
            // 이전 상태의 경과 시간으로 새 페이즈의 동명 Cue를 발화하지 않는다.
            // ASB의 데이터 시각 Cue 드라이버와 같은 상태 경계를 따른다.
            int expected = t.ctx.ExpectedStateHash;
            if (expected != 0 && expected != hash && expected != info.shortNameHash)
            {
                return;
            }

            // 상태·액션이 바뀌었거나 시간이 되돌아갔으면 새 진입으로 보고 초기화한다.
            bool reentered = hash != t.stateHash
                             || t.ctx.CurrentActionInstanceId != t.actionId
                             || seconds + 0.0001f < t.prevSeconds;
            if (reentered)
            {
                t.stateHash = hash;
                t.actionId = t.ctx.CurrentActionInstanceId;
                t.prevSeconds = -1f;             // 첫 관측이 이미 지난 시각이어도 놓치지 않도록
                t.clipResolved = false;
                for (int i = 0; i < t.fired.Length; i++) { t.fired[i] = false; t.suppressed[i] = false; }
            }

            // 이번 상태 진입에서 한 번만 — 재생 클립 이름을 모으고, 자체 이벤트를 가진 줄을 골라 둔다.
            if (!t.clipResolved)
            {
                ResolveClipContext(t, useNext);
                t.clipResolved = true;
            }

            for (int i = 0; i < table.entries.Length; i++)
            {
                JcCueTimingTable.Entry e = table.entries[i];
                if (e == null || !e.enabled || t.fired[i] || t.suppressed[i]) continue;
                if (string.IsNullOrEmpty(e.stateName) || string.IsNullOrEmpty(e.cueName)) continue;
                if (Animator.StringToHash(e.stateName) != hash) continue;

                // ★클립 범위 제한 — 상태 이름은 전 클래스가 공유하므로 이것까지 봐야 남의 유닛에 외치지 않는다.
                if (!string.IsNullOrEmpty(e.clipName) && !t.clipNames.Contains(e.clipName)) continue;

                if (t.prevSeconds < e.time && e.time <= seconds)
                {
                    t.fired[i] = true;
                    seenStates.Add(e.stateName);

                    // ★여기가 전부다 — 클립 이벤트가 했을 호출을 그대로 대신한다.
                    t.charactor.AniEvent_PresentationCue(e.cueName);

                    if (table.logFire)
                    {
                        Debug.Log($"[JcCueTiming] {t.charactor.name} / {e.stateName} / '{e.cueName}' " +
                                  $"@ {e.time:F3}s (관측 {seconds:F3}s)", t.charactor);
                    }
                }
            }

            t.prevSeconds = seconds;
        }

        /// <summary>
        /// 이번 상태에서 재생되는 클립을 조사해 두 가지를 정한다.
        ///   ① 클립 이름 집합 — 표의 클립 범위 제한에 쓴다(남의 클래스에 외치지 않기 위해).
        ///   ② 억제 대상 — 클립이 이미 같은 이름의 Cue 이벤트를 가졌으면 그 줄은 발화하지 않는다.
        /// ②가 있어서 ASB가 클립에 진짜 이벤트를 심으면 여기서 자동으로 손을 뗀다(이중 발화 방지).
        /// </summary>
        private void ResolveClipContext(Tracked t, bool useNext)
        {
            t.clipNames.Clear();

            clipInfoBuffer.Clear();
            if (useNext) t.animator.GetNextAnimatorClipInfo(0, clipInfoBuffer);
            else t.animator.GetCurrentAnimatorClipInfo(0, clipInfoBuffer);

            for (int c = 0; c < clipInfoBuffer.Count; c++)
            {
                AnimationClip clip = clipInfoBuffer[c].clip;
                if (clip == null) continue;

                t.clipNames.Add(clip.name);

                HashSet<string> cues = GetClipCues(clip);
                if (cues.Count == 0) continue;

                for (int i = 0; i < table.entries.Length; i++)
                {
                    JcCueTimingTable.Entry e = table.entries[i];
                    if (e != null && !string.IsNullOrEmpty(e.cueName) && cues.Contains(e.cueName))
                    {
                        t.suppressed[i] = true;
                    }
                }
            }
        }

        /// <summary>클립이 가진 Cue 이름 집합. clip.events는 호출마다 배열을 새로 만들므로 반드시 캐시한다.</summary>
        private static HashSet<string> GetClipCues(AnimationClip clip)
        {
            if (clipCueCache.TryGetValue(clip, out HashSet<string> cached)) return cached;

            var set = new HashSet<string>();
            AnimationEvent[] events = clip.events;
            for (int i = 0; i < events.Length; i++)
            {
                if (events[i].functionName == CueFunctionName && !string.IsNullOrEmpty(events[i].stringParameter))
                {
                    set.Add(events[i].stringParameter);
                }
            }
            clipCueCache[clip] = set;
            return set;
        }

        private void OnDestroy()
        {
            if (table == null || !table.warnUnusedStates) return;

            var missing = new HashSet<string>();
            for (int i = 0; i < table.entries.Length; i++)
            {
                JcCueTimingTable.Entry e = table.entries[i];
                if (e != null && e.enabled && !string.IsNullOrEmpty(e.stateName) && !seenStates.Contains(e.stateName))
                {
                    missing.Add(e.stateName);
                }
            }
            if (missing.Count > 0)
            {
                Debug.LogWarning("[JcCueTiming] 표에 있으나 한 번도 발화하지 않은 상태: " +
                                 string.Join(", ", missing) +
                                 "\n(상태 이름 오타이거나, 해당 스킬을 재생하지 않았거나, 클립이 자체 이벤트를 가졌을 수 있습니다.)");
            }
        }
    }
}
