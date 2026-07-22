using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 이펙트 재료(ISkillEffectBehaviour)에 전달되는 연출 실행 컨텍스트.
/// 시퀀서가 beat 실행 직전에 PresentationRuntimeContext에 등록하고, 재료가 Play(ctx)로 읽는다.
/// </summary>
public class SkillEffectContext
{
    /// <summary>이 연출 실행의 토큰. 늦게 도착한 이전 실행 이벤트를 무시하는 데 사용.</summary>
    public int ActionInstanceId;
    public BattleCharactor Caster;
    public BattleCharactor PrimaryTarget;
    public IReadOnlyList<BattleCharactor> Targets;
    public Vector3 TargetPosition;
    /// <summary>이펙트 생성 기준 소켓(시전자). null이면 재료가 자체 판단.</summary>
    public Transform SocketTransform;
    public float PlaybackSpeed = 1f;
    /// <summary>다단히트에서 몇 번째 타격인지(0-base). 단일 타격은 0.</summary>
    public int HitIndex;
}
