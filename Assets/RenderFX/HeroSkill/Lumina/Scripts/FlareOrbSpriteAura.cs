using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 루미나 「플레어 봄」 F2 — 2D 스프라이트 오라 마커/스케일러.
    /// 빌보드 자체는 FlareAuraSprite 셰이더가 처리하고, 이 컴포넌트는
    /// 프리셋 에디터의 씬 탐색 마커 + 쿼드 월드 크기(worldSize) 유지만 담당한다.
    /// 텍스처 여백(파란 영역)은 worldSize를 도달한계 지름보다 넉넉히 잡아 확보한다.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class FlareOrbSpriteAura : MonoBehaviour
    {
        [Tooltip("빌보드 쿼드 월드 크기(m). 도달한계(_MaxReach) 지름보다 넉넉하게 — 경계 클리핑 방지 여백.")]
        public float worldSize = 1.8f;

        void Update()
        {
            var s = transform.localScale;
            if (!Mathf.Approximately(s.x, worldSize) || !Mathf.Approximately(s.y, worldSize))
                transform.localScale = new Vector3(worldSize, worldSize, 1f);
        }
    }
}
