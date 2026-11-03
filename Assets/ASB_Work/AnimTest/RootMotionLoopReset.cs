using UnityEngine;

/// <summary>
/// 루트 모션을 직접 적용하면서, 반복 재생으로 다른 상태에 들어갈 때마다 처음 자리(로컬 위치·회전)로 되돌린다.
/// 루트 모션을 켠 채 같은 클립을 반복 재생하는 테스트 씬용. 반복할 때마다 이동량이 쌓여 캐릭터가 밀려나는 것을 막는다.
/// <para>Animator와 같은 오브젝트에 붙인다. 컨트롤러는 같은 클립을 가진 두 상태를 번갈아 오가게 만든다
/// (같은 상태로의 전환은 상태가 바뀌지 않아 감지하지 못한다).</para>
/// <para>StateMachineBehaviour.OnStateEnter에서 위치를 바꾸면 Animator가 그 뒤에 루트 모션으로 덮어쓰므로 여기서 처리한다.</para>
/// </summary>
[RequireComponent(typeof(Animator))]
public class RootMotionLoopReset : MonoBehaviour
{
    [Tooltip("감시할 Animator 레이어.")]
    [SerializeField] private int layer = 0;

    private Animator _animator;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private int _lastStateHash;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _startPosition = transform.localPosition;
        _startRotation = transform.localRotation;
    }

    // 이 메시지를 구현하면 Animator가 루트 모션을 직접 적용하지 않으므로 여기서 적용한다.
    private void OnAnimatorMove()
    {
        // 전환 중이면 도착할 상태 기준으로 판단해, 전환이 시작되는 순간 되돌린다.
        AnimatorStateInfo target = _animator.IsInTransition(layer)
            ? _animator.GetNextAnimatorStateInfo(layer)
            : _animator.GetCurrentAnimatorStateInfo(layer);

        if (target.fullPathHash != _lastStateHash)
        {
            bool first = _lastStateHash == 0;
            _lastStateHash = target.fullPathHash;
            if (!first)
            {
                transform.localPosition = _startPosition;
                transform.localRotation = _startRotation;
                return;
            }
        }

        transform.position += _animator.deltaPosition;
        transform.rotation = _animator.deltaRotation * transform.rotation;
    }
}
