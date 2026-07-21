using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 개발용 임시 하네스: H로 타깃팅 모드 진입 → JC 전방에 발사체 정지 생성 →
    /// 마우스로 마커 플레인 클릭 → 그 위치(마커 xz, arrivalY)로 발사체 발사.
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

        [Header("Spawn (JC 전방)")]
        [Tooltip("caster 전방 거리(m). 부호 뒤집으면 반대편.")]
        [SerializeField] private float forwardDist = 0.5f;
        [SerializeField] private float spawnY = 0.68f;

        [Header("Arrival")]
        [Tooltip("도착점 y (타깃 xz + 이 높이)")]
        [SerializeField] private float arrivalY = 0.68f;

        [SerializeField] private KeyCode toggleKey = KeyCode.H;

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

            if (Input.GetKeyDown(toggleKey))
            {
                _targeting = true;
                if (_proj == null) _proj = Instantiate(projectilePrefab);   // 씬 루트(lossyScale 1)
                _proj.Show(SpawnPos());
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
