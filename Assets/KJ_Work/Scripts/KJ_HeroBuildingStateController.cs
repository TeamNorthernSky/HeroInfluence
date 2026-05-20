using UnityEngine;

namespace KJ_Work
{
    /// <summary>
    /// herobuilding 오브젝트의 상태(State)에 따라
    /// Inspector에서 직접 지정한 슬롯을 Ally / Enemy / Normal 머티리얼로 교체합니다.
    /// </summary>
    public class KJ_HeroBuildingStateController : MonoBehaviour
    {
        // ──────────────────────────────────────
        // 상태 정의
        // ──────────────────────────────────────
        public enum State
        {
            Normal = 0,
            Ally   = 1,
            Enemy  = 2
        }

        // ──────────────────────────────────────
        // Inspector 노출 필드
        // ──────────────────────────────────────
        [Header("현재 상태")]
        [SerializeField] private State _currentState = State.Ally;

        [Header("상태별 머티리얼")]
        [SerializeField] private Material _normalMaterial;
        [SerializeField] private Material _allyMaterial;
        [SerializeField] private Material _enemyMaterial;

        // ──────────────────────────────────────
        // 교체 대상 슬롯 (Inspector에서 직접 지정)
        // ──────────────────────────────────────
        [System.Serializable]
        public struct TargetSlot
        {
            [Tooltip("교체할 Renderer 컴포넌트")]
            public Renderer renderer;
            [Tooltip("교체할 머티리얼 슬롯 인덱스 (0부터 시작)")]
            public int      slotIndex;
        }

        [Header("교체 대상 슬롯 (Renderer + 슬롯 인덱스)")]
        [SerializeField] private TargetSlot[] _targetSlots;

        private State _lastState = (State)(-1); // 직전 프레임 상태 (초기화 전 더미값)

        // ──────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────
        private void Start()
        {
            ApplyState(_currentState);
        }

        private void Update()
        {
            // 이전 상태와 달라졌을 때만 머티리얼 교체
            if (_currentState != _lastState)
                ApplyState(_currentState);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!Application.isPlaying) return;
            ApplyState(_currentState);
        }
#endif

        // ──────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────

        /// <summary>현재 State를 반환합니다.</summary>
        public State CurrentState => _currentState;

        /// <summary>상태를 변경하고 머티리얼을 즉시 교체합니다.</summary>
        public void SetState(State newState)
        {
            _currentState = newState;
            ApplyState(newState);
        }

        /// <summary>정수 인덱스로 상태를 변경합니다. (0=Normal, 1=Ally, 2=Enemy)</summary>
        public void SetStateByIndex(int index)
        {
            if (index < 0 || index > 2)
            {
                Debug.LogWarning($"[KJ_HeroBuildingStateController] 유효하지 않은 인덱스: {index}", this);
                return;
            }
            SetState((State)index);
        }

        // ──────────────────────────────────────
        // 내부 메서드
        // ──────────────────────────────────────

        /// <summary>지정된 슬롯에 상태에 맞는 머티리얼을 적용합니다.</summary>
        private void ApplyState(State state)
        {
            if (_targetSlots == null || _targetSlots.Length == 0)
            {
                Debug.LogWarning("[KJ_HeroBuildingStateController] Target Slots가 비어 있습니다. Inspector에서 Renderer와 슬롯 인덱스를 지정하세요.", this);
                return;
            }

            Material targetMat = GetMaterialForState(state);
            if (targetMat == null)
            {
                Debug.LogWarning($"[KJ_HeroBuildingStateController] {state} 상태의 머티리얼이 할당되지 않았습니다.", this);
                return;
            }

            foreach (var slot in _targetSlots)
            {
                if (slot.renderer == null) continue;

                // 플레이 모드에서는 materials(인스턴스 배열)로 교체해야 반영됨
                Material[] mats = slot.renderer.materials;
                if (slot.slotIndex < 0 || slot.slotIndex >= mats.Length) continue;
                mats[slot.slotIndex] = targetMat;
                slot.renderer.materials = mats;
            }

            _lastState = state;
            Debug.Log($"[KJ_HeroBuildingStateController] 상태 변경 → {state}  (머티리얼: {targetMat.name})");
        }

        private Material GetMaterialForState(State state)
        {
            switch (state)
            {
                case State.Normal: return _normalMaterial;
                case State.Ally:   return _allyMaterial;
                case State.Enemy:  return _enemyMaterial;
                default:           return _normalMaterial;
            }
        }
    }
}
