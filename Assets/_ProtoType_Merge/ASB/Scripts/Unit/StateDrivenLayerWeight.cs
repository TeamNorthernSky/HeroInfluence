using UnityEngine;

/// <summary>
/// 지정한 상태(예: MoveForward/MoveReturn)에 있는 동안만 대상 레이어 가중치를 1로 올리고, 벗어나면 0으로 내린다.
/// 걷기 중에만 상하체 분리 레이어를 켜는 용도.
/// <para>Base Layer의 상태머신 자체에 붙인다. 상태머신에 붙은 Behaviour는 그 안의 모든 상태 콜백을 받으므로
/// 걷기 상태를 빠져나간 뒤에도 매 프레임 가중치를 되돌릴 수 있다.</para>
/// <para>게임 코드(PlayState 등)는 건드리지 않는다. 대상 레이어가 없으면 아무것도 하지 않는다.</para>
/// </summary>
public class StateDrivenLayerWeight : StateMachineBehaviour
{
    [Tooltip("가중치를 조절할 레이어 이름.")]
    [SerializeField] private string targetLayerName = "WalkLayer";

    [Tooltip("이 상태들에 있는 동안만 레이어를 켠다. Base Layer 상태 이름 그대로.")]
    [SerializeField] private string[] activeStateNames = { "MoveForward", "MoveReturn" };

    [Tooltip("켜질 때 0→1까지 걸리는 시간(초). 0이면 즉시.")]
    [SerializeField, Min(0f)] private float blendInSeconds = 0.15f;

    [Tooltip("꺼질 때 1→0까지 걸리는 시간(초). 0이면 즉시.")]
    [SerializeField, Min(0f)] private float blendOutSeconds = 0.15f;

    private int _targetLayer = -2; // -2 = 미캐시, -1 = 레이어 없음
    private int[] _activeHashes;
    private int _lastFrame = -1;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) => Tick(animator, layerIndex);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex) => Tick(animator, layerIndex);

    private void Tick(Animator animator, int layerIndex)
    {
        // 전환 중에는 현재/다음 상태 모두 콜백이 오므로 프레임당 한 번만 처리한다.
        if (_lastFrame == Time.frameCount) return;
        _lastFrame = Time.frameCount;

        if (_targetLayer == -2) Cache(animator);
        if (_targetLayer < 0) return;

        // 전환 중이면 도착할 상태 기준으로 판단해 블렌드를 미리 시작한다.
        AnimatorStateInfo state = animator.IsInTransition(layerIndex)
            ? animator.GetNextAnimatorStateInfo(layerIndex)
            : animator.GetCurrentAnimatorStateInfo(layerIndex);

        float target = IsActiveState(state.shortNameHash) ? 1f : 0f;
        float current = animator.GetLayerWeight(_targetLayer);
        if (Mathf.Approximately(current, target)) return;

        float seconds = target > current ? blendInSeconds : blendOutSeconds;
        float next = seconds <= 0f ? target : Mathf.MoveTowards(current, target, Time.deltaTime / seconds);
        animator.SetLayerWeight(_targetLayer, next);
    }

    private void Cache(Animator animator)
    {
        _targetLayer = animator.GetLayerIndex(targetLayerName);
        if (_targetLayer < 0)
            Debug.LogWarning($"[StateDrivenLayerWeight] '{animator.name}'에 레이어 '{targetLayerName}'가 없습니다.", animator);

        _activeHashes = new int[activeStateNames != null ? activeStateNames.Length : 0];
        for (int i = 0; i < _activeHashes.Length; i++)
            _activeHashes[i] = Animator.StringToHash(activeStateNames[i]);
    }

    private bool IsActiveState(int shortNameHash)
    {
        for (int i = 0; i < _activeHashes.Length; i++)
            if (_activeHashes[i] == shortNameHash) return true;
        return false;
    }
}
