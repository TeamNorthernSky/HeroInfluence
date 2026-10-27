using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// ASB EffectManager.SpawnById는 프리팹을 Instantiate만 하고 Play()를 호출하지 않는다
    /// (스폰 즉시 자동 재생 가정). 이 브릿지를 이펙트 프리팹에 붙이면 활성화 시 VfxEffect.Play()가 자동 호출된다.
    /// 존재 자체가 검증 결과물: "ASB 스폰 규약에 JC 이펙트를 얹으려면 auto-play 브릿지가 필요"함을 코드로 증명.
    /// </summary>
    public class VfxAutoPlayOnSpawn : MonoBehaviour
    {
        [Tooltip("재생할 VfxEffect. 비우면 같은 GameObject에서 탐색.")]
        [SerializeField] private VfxEffect target;

        private void Reset() => target = GetComponent<VfxEffect>();

        private void OnEnable()
        {
            if (target == null) target = GetComponent<VfxEffect>();
            if (target != null) target.Play();
        }
    }
}
