using System.Collections.Generic;
using UnityEditor.Animations;
using UnityEngine;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>state 경로 해석 결과. 실패 시 <see cref="FailureReason"/>에 사람이 읽을 이유가 담긴다.</summary>
    public struct JigStateResolution
    {
        public bool Success;
        public string FailureReason;
        public AnimationClip Clip;
        public float StateSpeed;

        /// <summary>
        /// 그 state의 실효 길이(초). 런타임 <c>AnimatorStateInfo.length</c>에 대응하는 값이며
        /// NormalizedTime ↔ 초 변환의 분모다.
        /// </summary>
        public double EffectiveLength;

        public static JigStateResolution Fail(string reason) =>
            new JigStateResolution { Success = false, FailureReason = reason };
    }

    /// <summary>
    /// <c>SkillPresentationData</c>의 <c>AnimationStateName</c>(예: <c>"Base Layer.ClassSkill_1"</c>)을
    /// 실제 <see cref="AnimationClip"/>과 실효 길이로 해석한다.
    ///
    /// <b>짧은 이름 재귀 검색을 하지 않는다.</b> 서브 스테이트머신에 같은 이름의 state가 여러 개 있을 때
    /// 잘못된 클립을 잡고, 조용히 틀린 길이를 쓰면 마커가 전부 어긋난다.
    /// 전체 경로의 모든 세그먼트를 순서대로 따라가고, 모호하면 실패로 처리한다.
    /// </summary>
    public static class JigStateResolver
    {
        /// <summary>경로가 한 세그먼트뿐일 때 가정하는 레이어 인덱스.</summary>
        public const int AssumedLayerIndex = 0;

        /// <summary>
        /// <c>"Base Layer.Combat.ClassSkill_1"</c> → <c>["Base Layer", "Combat", "ClassSkill_1"]</c>.
        /// 빈 세그먼트는 제거한다. 순수 함수 — 테스트 대상.
        /// </summary>
        public static string[] SplitStatePath(string animationStateName)
        {
            if (string.IsNullOrWhiteSpace(animationStateName))
            {
                return new string[0];
            }

            string[] raw = animationStateName.Trim().Split('.');
            var segments = new List<string>(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                string s = raw[i].Trim();
                if (s.Length > 0)
                {
                    segments.Add(s);
                }
            }
            return segments.ToArray();
        }

        /// <summary>
        /// 실효 길이. <c>state.speed</c>가 1이 아니면 클립 길이와 달라진다.
        /// 순수 함수 — 테스트 대상.
        /// </summary>
        public static double ComputeEffectiveLength(double clipLength, float stateSpeed)
        {
            float speed = Mathf.Abs(stateSpeed);
            if (speed < 1e-4f)
            {
                return 0d;
            }
            return clipLength / speed;
        }

        public static JigStateResolution Resolve(RuntimeAnimatorController controller, string animationStateName)
        {
            if (controller == null)
            {
                return JigStateResolution.Fail("AnimatorController가 없습니다.");
            }

            string[] path = SplitStatePath(animationStateName);
            if (path.Length == 0)
            {
                return JigStateResolution.Fail("AnimationStateName이 비어 있습니다.");
            }

            // AnimatorOverrideController → 원본 컨트롤러 + 오버라이드 맵
            var overrideMap = new Dictionary<AnimationClip, AnimationClip>();
            RuntimeAnimatorController baseController = controller;
            while (baseController is AnimatorOverrideController aoc)
            {
                var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(aoc.overridesCount);
                aoc.GetOverrides(pairs);
                for (int i = 0; i < pairs.Count; i++)
                {
                    // 이미 상위 오버라이드가 정한 것을 하위가 덮지 않게 한다(가장 바깥이 우선).
                    if (pairs[i].Key != null && pairs[i].Value != null && !overrideMap.ContainsKey(pairs[i].Key))
                    {
                        overrideMap[pairs[i].Key] = pairs[i].Value;
                    }
                }
                baseController = aoc.runtimeAnimatorController;
                if (baseController == null)
                {
                    return JigStateResolution.Fail("AnimatorOverrideController의 원본 컨트롤러가 비어 있습니다.");
                }
            }

            var animatorController = baseController as AnimatorController;
            if (animatorController == null)
            {
                return JigStateResolution.Fail(
                    $"AnimatorController로 해석할 수 없는 타입입니다: {baseController.GetType().Name}");
            }

            // ── 레이어 ──
            AnimatorControllerLayer[] layers = animatorController.layers;
            if (layers == null || layers.Length == 0)
            {
                return JigStateResolution.Fail("컨트롤러에 레이어가 없습니다.");
            }

            AnimatorStateMachine machine;
            int stateNameIndex;
            if (path.Length == 1)
            {
                // 레이어를 특정할 수 없다. Base Layer를 가정하되 호출자가 알 수 있게 이유에 남긴다.
                machine = layers[AssumedLayerIndex].stateMachine;
                stateNameIndex = 0;
            }
            else
            {
                int layerIndex = -1;
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i].name == path[0])
                    {
                        layerIndex = i;
                        break;
                    }
                }
                if (layerIndex < 0)
                {
                    return JigStateResolution.Fail($"레이어 '{path[0]}'를 찾지 못했습니다.");
                }
                machine = layers[layerIndex].stateMachine;
                stateNameIndex = path.Length - 1;

                // ── 중간 세그먼트 = 서브 스테이트머신 ──
                for (int seg = 1; seg < path.Length - 1; seg++)
                {
                    AnimatorStateMachine next = null;
                    int hits = 0;
                    ChildAnimatorStateMachine[] children = machine.stateMachines;
                    for (int c = 0; c < children.Length; c++)
                    {
                        if (children[c].stateMachine != null && children[c].stateMachine.name == path[seg])
                        {
                            next = children[c].stateMachine;
                            hits++;
                        }
                    }
                    if (hits == 0)
                    {
                        return JigStateResolution.Fail($"서브 스테이트머신 '{path[seg]}'를 찾지 못했습니다.");
                    }
                    if (hits > 1)
                    {
                        return JigStateResolution.Fail($"서브 스테이트머신 '{path[seg]}'가 중복입니다.");
                    }
                    machine = next;
                }
            }

            if (machine == null)
            {
                return JigStateResolution.Fail("스테이트머신을 해석하지 못했습니다.");
            }

            // ── state (그 머신의 직계에서만 찾는다 — 재귀 금지) ──
            AnimatorState found = null;
            int stateHits = 0;
            ChildAnimatorState[] states = machine.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state != null && states[i].state.name == path[stateNameIndex])
                {
                    found = states[i].state;
                    stateHits++;
                }
            }

            if (stateHits == 0)
            {
                return JigStateResolution.Fail(
                    $"state '{path[stateNameIndex]}'를 '{machine.name}' 안에서 찾지 못했습니다. " +
                    "서브 스테이트머신 안에 있다면 전체 경로로 지정하세요.");
            }
            if (stateHits > 1)
            {
                return JigStateResolution.Fail(
                    $"state '{path[stateNameIndex]}'가 '{machine.name}' 안에 중복입니다. 해석을 중단합니다.");
            }

            // ── Motion → 클립 ──
            Motion motion = found.motion;
            if (motion == null)
            {
                return JigStateResolution.Fail($"state '{found.name}'에 Motion이 없습니다.");
            }

            var clip = motion as AnimationClip;
            if (clip == null)
            {
                return JigStateResolution.Fail(
                    $"state '{found.name}'의 Motion이 AnimationClip이 아닙니다({motion.GetType().Name}). " +
                    "BlendTree는 길이를 정적으로 알 수 없어 1차 지그에서 지원하지 않습니다.");
            }

            // 오버라이드 치환
            if (overrideMap.TryGetValue(clip, out AnimationClip overridden) && overridden != null)
            {
                clip = overridden;
            }

            // ── 속도 가드 ──
            // state.speed가 1이 아니면 런타임 AnimatorStateInfo.length와의 정확한 관계가 미검증이다.
            // 현재 전투 컨트롤러에는 speed != 1인 state가 없으므로(스캔 확인) 거부해도 손실이 없다.
            if (found.speedParameterActive)
            {
                return JigStateResolution.Fail(
                    $"state '{found.name}'가 파라미터로 속도를 제어합니다(speedParameterActive). " +
                    "길이를 정적으로 알 수 없어 지원하지 않습니다.");
            }
            if (!Mathf.Approximately(found.speed, 1f))
            {
                return JigStateResolution.Fail(
                    $"state '{found.name}'의 speed가 {found.speed}입니다(1이 아님). " +
                    "런타임 AnimatorStateInfo.length와의 관계가 미검증이므로 1차 지그에서 지원하지 않습니다.");
            }

            if (clip.length <= 0f)
            {
                return JigStateResolution.Fail($"클립 '{clip.name}'의 길이가 0입니다.");
            }

            return new JigStateResolution
            {
                Success = true,
                Clip = clip,
                StateSpeed = found.speed,
                EffectiveLength = ComputeEffectiveLength(clip.length, found.speed),
            };
        }
    }
}
