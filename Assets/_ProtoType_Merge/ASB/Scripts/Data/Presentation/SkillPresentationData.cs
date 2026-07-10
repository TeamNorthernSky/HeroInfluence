using UnityEngine;

/// <summary>
/// Presentation data mapped by SkillIndex through SkillPresentationCatalog.
/// CSV skill values remain separate; this asset stores animation, effect, sound,
/// hit timing, and projectile presentation overrides.
/// </summary>
[CreateAssetMenu(fileName = "SkillPresentation_New", menuName = "Battle/Skill Presentation Data")]
public class SkillPresentationData : ScriptableObject
{
    [Header("Skill Binding")]
    [Tooltip("SkillData.skillIndex used by SkillPresentationEditorWindow and SkillPresentationCatalog.")]
    public int SkillIndex;

    [Header("Animation Override (empty uses CSV)")]
    [Tooltip("Empty uses SkillData.AnimationTrigger.")]
    public string AnimationTriggerOverride;
    [Tooltip("Empty uses SkillData.TargetAnimationTrigger.")]
    public string TargetAnimationTriggerOverride;

    [Header("Sound")]
    public AudioClip AttackSfxClip;
    public AudioClip HitSfxClip;
    [Range(0f, 1f)] public float SfxVolume = 1f;

    [Header("Attack Effect")]
    [Tooltip("Whether to spawn the attack effect prefab.")]
    public bool EnableAttackEffect = true;
    [Tooltip("Effect prefab spawned at the actor attack socket.")]
    public GameObject AttackEffectPrefab;
    [Tooltip("Socket-space position offset.")]
    public Vector3 AttackEffectPositionOffset;
    [Tooltip("Socket rotation offset in Euler degrees.")]
    public Vector3 AttackEffectRotationOffset;

    [Header("Hit Effect")]
    [Tooltip("Whether to spawn the hit effect prefab.")]
    public bool EnableHitEffect = true;
    [Tooltip("Effect prefab spawned at the target hit socket.")]
    public GameObject HitEffectPrefab;
    [Tooltip("Socket-space position offset.")]
    public Vector3 HitEffectPositionOffset;
    [Tooltip("Socket rotation offset in Euler degrees.")]
    public Vector3 HitEffectRotationOffset;

    [Header("Hit Timing")]
    [Tooltip("true: wait for AniEvent_OnHit. false: use HitDelay.")]
    public bool UseAnimEvent = false;
    [Tooltip("Hit delay in seconds when UseAnimEvent is false.")]
    public float HitDelay = 0.25f;

    [Header("Projectile")]
    [Tooltip("Empty uses the normal instant hit sequence.")]
    public GameObject ProjectilePrefab;
    [Tooltip("Projectile travel time in seconds before battle speed scaling.")]
    public float FlightTime = 0.3f;
    public ProjectileTrajectoryType TrajectoryType = ProjectileTrajectoryType.Straight;
    [Tooltip("Max height used when TrajectoryType is Arc.")]
    public float ArcHeight = 2f;
    [Tooltip("Scale projectile to grid cell size.")]
    public bool ScaleByCellSize;
}

public enum ProjectileTrajectoryType
{
    Straight,
    Arc
}
