using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 통합 오브(FlareBombOrbFull)의 오라 레이어 토글.
    /// F1(3D 레인 셸, FlareOrbAlt)과 F2(2D 스프라이트, FlareOrbSprite2D)를
    /// 택일 또는 동시(F1F2_Both) 활성화한다. 인스펙터에서 즉시 전환.
    /// </summary>
    [ExecuteAlways]
    public class FlareOrbLayerToggle : MonoBehaviour
    {
        public enum AuraMode
        {
            F1_Lane3D,
            F2_Sprite2D,
            F1F2_Both,
        }

        [Tooltip("오라 레이어 선택. F1=3D 레인 셸(FlareOrbAlt), F2=2D 스프라이트(FlareOrbSprite2D), Both=둘 다.")]
        public AuraMode mode = AuraMode.F2_Sprite2D;

        void Update()
        {
            bool wantF1 = mode == AuraMode.F1_Lane3D || mode == AuraMode.F1F2_Both;
            bool wantF2 = mode == AuraMode.F2_Sprite2D || mode == AuraMode.F1F2_Both;
            var f1 = transform.Find("FlareOrbAlt");
            var f2 = transform.Find("FlareOrbSprite2D");
            if (f1 != null && f1.gameObject.activeSelf != wantF1) f1.gameObject.SetActive(wantF1);
            if (f2 != null && f2.gameObject.activeSelf != wantF2) f2.gameObject.SetActive(wantF2);
        }
    }
}
