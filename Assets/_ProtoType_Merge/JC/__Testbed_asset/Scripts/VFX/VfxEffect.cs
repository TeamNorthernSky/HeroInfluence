using System;
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

        /// <summary>파라미터 없는 즉시 재생.</summary>
        public virtual void Play() { }

        /// <summary>출발점/목표점을 받는 재생(발사체 등). 기본은 Play()로 위임.</summary>
        public virtual void Play(Transform origin, Transform target) => Play();

        /// <summary>중단.</summary>
        public virtual void Stop() { }

        protected void RaiseFinished() => OnFinished?.Invoke(this);
    }
}
