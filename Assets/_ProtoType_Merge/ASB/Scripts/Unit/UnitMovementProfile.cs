using UnityEngine;

/// <summary>
/// 전투 이동/회전 연출 수치.
/// RotateOnly = false : 근접 — 타겟 앞으로 전진 후 복귀
/// RotateOnly = true  : 원거리 — 타겟 방향으로 회전 후 원위치
/// 이 컴포넌트가 없으면 BattleManager가 연출을 건너뜁니다.
/// </summary>
public class UnitMovementProfile : MonoBehaviour
{
    [Header("Mode")]
    public bool RotateOnly = false;

    [Header("Melee (RotateOnly = false)")]
    public float ApproachDistance = 1.2f;
    public float MoveDuration = 0.25f;
    public float ReturnDuration = 0.3f;

    [Header("Ranged (RotateOnly = true)")]
    public float RotateDuration = 0.15f;
    public float RotateReturnDuration = 0.2f;

    [Header("Movement SFX (DHAudioClipCatalog SfxClips 키)")]
    [Tooltip("근접 접근 이동 시작 시 재생할 효과음 키. 스킬 MovePhase.SfxKey가 있으면 그쪽이 우선. 비면 무음.")]
    public string ApproachSfxKey;
    [Tooltip("근접 복귀 이동 시작 시 재생할 효과음 키. 스킬 ReturnPhase.SfxKey가 있으면 그쪽이 우선. 비면 무음.")]
    public string ReturnSfxKey;
}
