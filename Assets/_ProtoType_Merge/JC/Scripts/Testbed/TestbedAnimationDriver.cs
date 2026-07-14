using UnityEngine;

/// <summary>
/// 테스트베드 전용. 키 입력 → Animator 트리거 발화.
/// 기본 상태(Idle)는 컨트롤러가 알아서 유지하고,
/// 바인딩된 키를 누르면 해당 트리거 파라미터를 쏜다.
/// </summary>
public class TestbedAnimationDriver : MonoBehaviour
{
    [System.Serializable]
    public struct KeyTriggerBinding
    {
        [Tooltip("누를 키")]
        public KeyCode key;

        [Tooltip("발화할 Animator Trigger 파라미터 이름")]
        public string trigger;
    }

    [System.Serializable]
    public struct KeyHoldBinding
    {
        [Tooltip("누르고 있는 동안 Bool이 true가 되는 키")]
        public KeyCode key;

        [Tooltip("연동할 Animator Bool 파라미터 이름")]
        public string boolParam;
    }

    [Tooltip("키 → 트리거 바인딩 목록(단발). 컨트롤러에 같은 이름의 Trigger 파라미터가 있어야 한다.")]
    public KeyTriggerBinding[] bindings =
    {
        new KeyTriggerBinding { key = KeyCode.F, trigger = "Fireball" },
    };

    [Tooltip("키 → Bool 바인딩 목록(홀드). 누르는 동안 true, 떼면 false.")]
    public KeyHoldBinding[] holdBindings =
    {
        new KeyHoldBinding { key = KeyCode.C, boolParam = "Charging" },
    };

    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponentInChildren<Animator>();
        if (_animator == null)
            Debug.LogWarning("[TestbedAnimationDriver] Animator를 찾지 못했습니다.", this);
    }

    private void Update()
    {
        if (_animator == null)
            return;

        foreach (var b in bindings)
        {
            if (Input.GetKeyDown(b.key) && !string.IsNullOrEmpty(b.trigger))
                _animator.SetTrigger(b.trigger);
        }

        foreach (var h in holdBindings)
        {
            if (string.IsNullOrEmpty(h.boolParam))
                continue;
            if (Input.GetKeyDown(h.key))
                _animator.SetBool(h.boolParam, true);
            else if (Input.GetKeyUp(h.key))
                _animator.SetBool(h.boolParam, false);
        }
    }
}
