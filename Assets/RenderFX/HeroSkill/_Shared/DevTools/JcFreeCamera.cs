using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// VFX 확인용 프리 카메라 — 테스트베드·프리뷰 씬 공용.
    ///
    /// 조작 (260804 사용자 확정 규격)
    ///   <b>우클릭 홀드 중에만</b> 전부 동작한다. 떼면 아무 입력도 먹지 않는다.
    ///     · 마우스 이동 = 시점 회전
    ///     · <b>W / S</b> = 전후 이동(거리 조절)      · <b>A / D</b> = 좌우 이동
    ///     · <b>Q / E</b> = 상하 이동
    ///     · <b>Shift</b> = 가속                      · <b>Ctrl</b> = 감속
    ///   <b>휠 줌은 없다</b> — 거리는 W/S로만 조절한다.
    ///
    /// ★우클릭 홀드를 조건으로 두는 이유: 이 씬들의 스킬·검사 키가 W·E·R·Q·T 등을 쓴다.
    ///   홀드 없이 WASD를 먹으면 이펙트 트리거와 충돌한다.
    /// ★Time.unscaledDeltaTime 을 쓴다 — 배속·일시정지 중에도 카메라는 움직여야 관찰이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcFreeCamera : MonoBehaviour
    {
        [Header("이동")]
        [Tooltip("기본 이동 속도(초당 유닛).")]
        [SerializeField] private float moveSpeed = 6f;
        [Tooltip("Shift 를 누르고 있을 때 곱해지는 배수.")]
        [SerializeField] private float fastMultiplier = 3f;
        [Tooltip("Ctrl 을 누르고 있을 때 곱해지는 배수.")]
        [SerializeField] private float slowMultiplier = 0.25f;

        [Header("시점")]
        [SerializeField] private float lookSensitivity = 3f;
        [Tooltip("위아래 시점 제한(도).")]
        [Range(1f, 89.9f)] [SerializeField] private float pitchLimit = 89f;
        [SerializeField] private bool invertY;

        [Header("초기화")]
        [Tooltip("우클릭 홀드 + 이 버튼 = 초기 위치로 복귀. 기본 2 = 휠 클릭.")]
        [SerializeField] private int resetMouseButton = 2;
        [Tooltip("복귀 위치·각도. 기본값은 TmpBattleScene(실전투) 의 Main Camera 와 동일하다 —\n" +
                 "프리뷰에서 본 그림이 실제 전투 화면과 같은 구도인지 바로 대조하기 위함.")]
        [SerializeField] private Vector3 resetPosition = new Vector3(3.2f, 15f, 0f);
        [SerializeField] private Vector3 resetEuler = new Vector3(79.1292f, 270f, 0f);
        [Tooltip("복귀 시 FOV 도 함께 맞춘다(0 이하면 건드리지 않음).")]
        [SerializeField] private float resetFov = 60f;

        [Header("표시")]
        [SerializeField] private bool showHint = true;

        private float _yaw, _pitch;
        private bool _initialized;

        private void OnEnable() => SyncFromTransform();

        /// <summary>현재 트랜스폼 각도를 내부 상태로 가져온다(씬에 놓인 초기 각도 존중).</summary>
        private void SyncFromTransform()
        {
            Vector3 e = transform.eulerAngles;
            _yaw = e.y;
            _pitch = e.x > 180f ? e.x - 360f : e.x;   // 0~360 → -180~180
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized) SyncFromTransform();

            // ★우클릭을 놓으면 회전도 이동도 하지 않는다.
            if (!Input.GetMouseButton(1)) return;

            // ── 초기화 (우클릭 홀드 + 휠 클릭) ──────────────────────
            if (Input.GetMouseButtonDown(resetMouseButton)) { ResetView(); return; }

            // ── 시점 ────────────────────────────────────────────────
            float mx = Input.GetAxis("Mouse X") * lookSensitivity;
            float my = Input.GetAxis("Mouse Y") * lookSensitivity * (invertY ? 1f : -1f);
            _yaw += mx;
            _pitch = Mathf.Clamp(_pitch + my, -pitchLimit, pitchLimit);
            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // ── 이동 ────────────────────────────────────────────────
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) speed *= fastMultiplier;
            if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)) speed *= slowMultiplier;

            // GetAxisRaw 대신 키를 직접 읽는다 — 입력 매니저 설정에 의존하지 않게.
            float fwd = (Input.GetKey(KeyCode.W) ? 1f : 0f) + (Input.GetKey(KeyCode.S) ? -1f : 0f);
            float side = (Input.GetKey(KeyCode.D) ? 1f : 0f) + (Input.GetKey(KeyCode.A) ? -1f : 0f);
            float up = (Input.GetKey(KeyCode.E) ? 1f : 0f) + (Input.GetKey(KeyCode.Q) ? -1f : 0f);

            Vector3 dir = transform.forward * fwd + transform.right * side + Vector3.up * up;
            if (dir.sqrMagnitude > 1e-6f)
                transform.position += dir.normalized * (speed * Time.unscaledDeltaTime);
        }

        /// <summary>초기 구도로 되돌린다. 인스펙터에서 호출해도 된다.</summary>
        [ContextMenu("카메라 초기화")]
        public void ResetView()
        {
            transform.position = resetPosition;
            transform.rotation = Quaternion.Euler(resetEuler);
            if (resetFov > 0f)
            {
                var cam = GetComponent<Camera>();
                if (cam != null) cam.fieldOfView = resetFov;
            }
            SyncFromTransform();   // 내부 yaw/pitch 를 맞춰야 다음 조작이 튀지 않는다
        }

        private void OnGUI()
        {
            if (!showHint) return;
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            string body = Input.GetMouseButton(1)
                ? "<color=#7fd0ff>카메라</color>  W/S 전후 · A/D 좌우 · Q/E 상하 · Shift 가속 · Ctrl 감속 · <b>휠클릭 초기화</b>"
                : "<color=#8f8f8f>카메라 — 우클릭 홀드 중에만 조작 (홀드+휠클릭 = 초기화)</color>";
            GUI.Label(new Rect(12, Screen.height - 24f, 760, 20f), body, style);
        }
    }
}
