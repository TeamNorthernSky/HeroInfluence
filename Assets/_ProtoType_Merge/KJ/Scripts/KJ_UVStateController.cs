using UnityEngine;

namespace KJ_Work
{
    /// <summary>
    /// 오브젝트의 State를 MaterialPropertyBlock으로 셰이더에 전달합니다.
    /// 실제 UV 교체 판단은 KJ/StateUV 셰이더 내부에서 버텍스 컬러 기준으로 수행합니다.
    ///
    ///   State.Normal (0) → 흰색 정점 UV: (0.10, 0.10)
    ///   State.Ally   (1) → 흰색 정점 UV: (0.50, 0.10)
    ///   State.Enemy  (2) → 흰색 정점 UV: (0.60, 0.10)
    ///
    /// 자식 오브젝트의 모든 MeshRenderer/SkinnedMeshRenderer에 자동 적용됩니다.
    /// </summary>
    public class KJ_UVStateController : MonoBehaviour
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
        [SerializeField] private State _currentState = State.Normal;

        // ──────────────────────────────────────
        // 내부 상태
        // ──────────────────────────────────────
        private static readonly int _StateID = Shader.PropertyToID("_State");

        private MaterialPropertyBlock  _mpb;
        private Renderer[]             _renderers;

        // ──────────────────────────────────────
        // Unity 생명주기
        // ──────────────────────────────────────
        private void Awake()
        {
            _mpb       = new MaterialPropertyBlock();
            _renderers = GetComponentsInChildren<Renderer>(true);

            if (_renderers.Length == 0)
                Debug.LogWarning("[KJ_UVStateController] 자식 Renderer를 찾지 못했습니다.", this);
        }

        private void Start()
        {
            ApplyState(_currentState);
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // 에디터 플레이 중 Inspector 변경 시 즉시 반영
            if (!Application.isPlaying) return;
            if (_renderers == null || _renderers.Length == 0)
                _renderers = GetComponentsInChildren<Renderer>(true);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            ApplyState(_currentState);
        }
#endif

        // ──────────────────────────────────────
        // 공개 API
        // ──────────────────────────────────────

        /// <summary>현재 State를 반환합니다.</summary>
        public State CurrentState => _currentState;

        /// <summary>
        /// 상태를 변경하고 셰이더 프로퍼티를 즉시 갱신합니다.
        /// </summary>
        public void SetState(State newState)
        {
            _currentState = newState;
            ApplyState(newState);
        }

        /// <summary>
        /// 정수 인덱스로 상태를 변경합니다. (0=Normal, 1=Ally, 2=Enemy)
        /// </summary>
        public void SetStateByIndex(int index)
        {
            if (index < 0 || index > 2)
            {
                Debug.LogWarning($"[KJ_UVStateController] 유효하지 않은 인덱스: {index}. 0~2 만 허용합니다.", this);
                return;
            }
            SetState((State)index);
        }

        // ──────────────────────────────────────
        // 내부 메서드
        // ──────────────────────────────────────

        private void ApplyState(State state)
        {
            if (_renderers == null) return;

            float stateValue = (float)state;

            foreach (Renderer r in _renderers)
            {
                if (r == null) continue;

                // 기존 PropertyBlock 값을 읽어 온 뒤 _State 만 덮어쓴다
                r.GetPropertyBlock(_mpb);
                _mpb.SetFloat(_StateID, stateValue);
                r.SetPropertyBlock(_mpb);
            }

            Debug.Log($"[KJ_UVStateController] 상태 변경 → {state} (_State = {stateValue})");
        }
    }
}
