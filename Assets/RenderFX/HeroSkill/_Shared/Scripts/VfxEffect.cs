using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 모든 VFX의 공통 베이스. 트리거링을 분리하기 위한 최소 계약.
    /// 이펙트는 "누가 부르는지" 모른다 — C키/스킬버튼/애니이벤트 무엇이든 Play()만 호출하면 된다.
    /// </summary>
    public abstract class VfxEffect : MonoBehaviour
    {
        /// <summary>이펙트가 자연 종료됐을 때(또는 Stop 완료 시) 발생.</summary>
        public event Action<VfxEffect> OnFinished;

        public bool IsPlaying { get; protected set; }

        /// <summary>전투가 주입하는 재생 배속. 씬의 Time.timeScale과 별도로 적용합니다.</summary>
        public float PlaybackSpeed { get; set; } = 1f;
        protected float EffectDeltaTime => Time.deltaTime * Mathf.Max(0.01f, PlaybackSpeed);

        /// <summary>파라미터 없는 즉시 재생.</summary>
        public virtual void Play() { }

        /// <summary>출발점/목표점을 받는 재생(발사체 등). 기본은 Play()로 위임.</summary>
        public virtual void Play(Transform origin, Transform target) => Play();

        /// <summary>
        /// ★다중 대상 주입 — <b>Play 보다 먼저</b> 호출한다.
        ///
        /// 「누가 대상인가」는 이펙트가 정할 일이 아니다. 사거리·아군판별·연쇄 규칙은 전부 스킬의 몫이고,
        /// 이펙트는 <b>정해진 곳을 향해 그림을 그릴 뿐</b>이다. 그래서 이 문은 결정된 결과만 받는다.
        ///
        /// 기본 구현은 아무것도 하지 않는다 — 단일 대상 이펙트는 <see cref="Play(Transform,Transform)"/> 만
        /// 쓰면 되므로 이 문을 열어 둘 이유가 없다.
        /// </summary>
        public virtual void SetTargets(IReadOnlyList<VfxTarget> targets) { }

        /// <summary>중단.</summary>
        public virtual void Stop() { }

        protected void RaiseFinished() => OnFinished?.Invoke(this);
    }
}
