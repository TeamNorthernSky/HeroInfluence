using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 개발용 임시 트리거 하네스. Animator의 bool 파라미터(기본 "Charging")를 구독해
    /// 지정한 소켓들에 차징 오브를 생성/재생/정지한다.
    /// ★실제 게임에선 이 하네스를 스킬 로직으로 교체 — 동일하게 ChargeOrbVfx.Play()/Stop()만 호출하면 된다.
    /// (VFX 자체는 트리거 소스를 모른다 = 트리거링 분리)
    /// </summary>
    public class ChargeVfxDebugTrigger : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private Animator animator;
        [SerializeField] private string boolParam = "Charging";

        [Header("Spawn")]
        [SerializeField] private GameObject orbPrefab;
        [Tooltip("차징 오브를 붙일 소켓들(양손). 각 소켓 자식으로 인스턴스화됨.")]
        [SerializeField] private Transform[] sockets;
        [Tooltip("소켓 로컬 축 기준 오프셋(월드 단위 m). z+ = 손바닥 앞쪽. 소켓 lossyScale를 자동 보정.")]
        [SerializeField] private Vector3 localOffsetWorld = new Vector3(0f, 0f, 0.04f);

        private ChargeOrbVfx[] _orbs;
        private bool _prev;

        private void Start()
        {
            if (orbPrefab == null || sockets == null) return;
            _orbs = new ChargeOrbVfx[sockets.Length];
            for (int i = 0; i < sockets.Length; i++)
            {
                if (sockets[i] == null) continue;
                var go = Instantiate(orbPrefab, sockets[i]);
                go.transform.localRotation = Quaternion.identity;
                float ps = sockets[i].lossyScale.x;
                if (ps < 1e-6f) ps = 1f;
                go.transform.localPosition = localOffsetWorld / ps;   // 월드 오프셋을 소켓 스케일로 보정
                _orbs[i] = go.GetComponent<ChargeOrbVfx>();
            }
        }

        private void Update()
        {
            if (animator == null || _orbs == null) return;
            bool now = animator.GetBool(boolParam);
            if (now && !_prev)
            {
                foreach (var orb in _orbs) if (orb) orb.Play();
            }
            else if (!now && _prev)
            {
                foreach (var orb in _orbs) if (orb) orb.Stop();
            }
            _prev = now;
        }
    }
}
