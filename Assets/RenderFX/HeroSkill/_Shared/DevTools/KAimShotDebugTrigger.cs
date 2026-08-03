using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 블랙불릿 AimShot 검증용 하네스. 키 입력으로 이펙트를 토글한다.
    /// 실 게임에선 이 하네스 대신 스킬 시스템(연출 Cue)이 Vfx.Play(caster, target)를 호출한다.
    ///
    /// 총구 소켓은 UnitSocketHolder 경유가 정석이지만, 캐릭터가 런타임 스폰되기 전에도
    /// 셰이더·타이밍을 보려면 소켓 없이 캐스터 루트 폴백으로 돌아가야 한다. muzzleSocket을
    /// 비워두면 프리셋의 casterFallbackOffset이 쓰인다.
    /// </summary>
    public class KAimShotDebugTrigger : MonoBehaviour
    {
        [Header("대상")]
        [SerializeField] private KAimShotVfx vfxPrefab;
        [SerializeField] private Transform caster;
        [SerializeField] private Transform target;
        [Tooltip("총구 소켓(Gun.001/ShootPlace). 비워두면 캐스터 루트 폴백.")]
        [SerializeField] private Transform muzzleSocket;

        [Header("입력")]
        [SerializeField] private KeyCode playKey = KeyCode.Z;

        private KAimShotVfx _instance;

        private void Update()
        {
            // 우클릭(카메라 조작) 중에는 무시 — 기존 DebugTrigger들과 동일 규약
            if (Input.GetMouseButton(1)) return;
            if (!Input.GetKeyDown(playKey)) return;
            Toggle();
        }

        private void Toggle()
        {
            if (vfxPrefab == null || caster == null || target == null)
            {
                Debug.LogWarning("[KAimShotDebugTrigger] vfxPrefab/caster/target 배선이 비어 있습니다.");
                return;
            }

            if (_instance == null)
            {
                // 씬 루트(lossyScale 1)에 생성 — 소켓 스케일 오염 차단
                _instance = Instantiate(vfxPrefab);
                _instance.transform.SetParent(null, true);
                _instance.transform.localScale = Vector3.one;
            }

            if (_instance.IsPlaying)
            {
                _instance.StopGraceful();
            }
            else
            {
                _instance.SetMuzzleSocket(muzzleSocket);
                _instance.Play(caster, target);
            }
        }
    }
}
