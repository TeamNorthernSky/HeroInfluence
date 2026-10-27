using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 개발용 임시 하네스: Q로 힐 투사체 발사.
    /// directTarget 지정 시 = 키 한 번으로 해당 대상에게 즉시 발사(스킬 키 표준: Q=힐 투사체).
    /// directTarget 미지정 시 = 기존 타깃팅 모드(키 → 정지 생성 → 마커 플레인 클릭 → 발사).
    /// ★실 게임에선 이 하네스를 스킬 로직으로 교체 — ProjectileVfx.Show()/Launch()(또는 Play(origin,target))만 호출하면 됨.
    /// </summary>
    public class ProjectileDebugTrigger : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform caster;              // JC_TestActor
        [SerializeField] private ProjectileVfx projectilePrefab;
        [SerializeField] private Camera cam;                    // 비우면 Camera.main
        [Tooltip("클릭 대상 마커 플레인들(4개). Collider 필요.")]
        [SerializeField] private Transform[] markers;
        [Tooltip("지정 시 키 한 번으로 이 대상(xz)+arrivalY로 즉시 발사(타깃팅 모드 생략). 아군 대상 표준 = Blaster_JC_Test")]
        [SerializeField] private Transform directTarget;

        [Header("Spawn (JC 전방)")]
        [Tooltip("caster 전방 거리(m). 부호 뒤집으면 반대편.")]
        [SerializeField] private float forwardDist = 0.5f;
        [SerializeField] private float spawnY = 0.68f;

        [Header("Arrival")]
        [Tooltip("도착점 y (타깃 xz + 이 높이)")]
        [SerializeField] private float arrivalY = 0.68f;

        [SerializeField] private KeyCode toggleKey = KeyCode.Q;

        private ProjectileVfx _proj;
        private bool _targeting;

        private Vector3 SpawnPos()
        {
            Vector3 p = caster.position + caster.forward * forwardDist;
            p.y = spawnY;
            return p;
        }

        private void Update()
        {
            if (caster == null || projectilePrefab == null) return;
            if (Input.GetMouseButton(1)) return;   // 우클릭 홀드 = 카메라 조작 중 → 스킬 키 무시

            if (Input.GetKeyDown(toggleKey))
            {
                if (_proj == null) _proj = Instantiate(projectilePrefab);   // 씬 루트(lossyScale 1)
                _proj.Show(SpawnPos());
                if (directTarget != null)
                {
                    // 직접 발사: 대상 xz + arrivalY로 즉시
                    _proj.Launch(new Vector3(directTarget.position.x, arrivalY, directTarget.position.z));
                    _targeting = false;
                }
                else _targeting = true;
            }

            if (_targeting && Input.GetMouseButtonDown(0))
            {
                var c = cam != null ? cam : Camera.main;
                if (c == null) return;
                if (Physics.Raycast(c.ScreenPointToRay(Input.mousePosition), out var hit, 1000f))
                {
                    Transform m = null;
                    if (markers != null)
                        foreach (var mk in markers)
                            if (mk != null && (hit.transform == mk || hit.transform.IsChildOf(mk))) { m = mk; break; }
                    if (m != null)
                    {
                        _proj.Launch(new Vector3(m.position.x, arrivalY, m.position.z));
                        _targeting = false;
                    }
                }
            }
        }
    }
}
