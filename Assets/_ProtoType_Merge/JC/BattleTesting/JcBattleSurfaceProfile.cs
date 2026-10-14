using UnityEngine;
namespace JC.BattleTesting
{
    [CreateAssetMenu(menuName="JC/전투 테스트/배경 디테일 설정")]
    public sealed class JcBattleSurfaceProfile : ScriptableObject
    {
        [Tooltip("도로 형상과 위치 선정은 유지하고 전투용 표시와 자연 소품만 보강합니다.")]
        public bool enableDetails=true;
        [Tooltip("도로 표시 전용 셰이더입니다. 탐사 횡단보도도 이 참조를 사용합니다.")]
        public Shader roadShader;
        [Tooltip("기존 표시를 지울 때 사용할 원본 무표시 아스팔트입니다.")]
        public Texture2D plainAsphalt;
        [Min(.02f),Tooltip("전투 월드 단위의 중앙 점선 폭입니다.")]
        public float lineWidth=.15f;
        [Min(.1f),Tooltip("전투 월드 단위의 중앙 점선 한 마디 길이입니다.")]
        public float dashLength=2.5f;
        [Min(.1f),Tooltip("전투 월드 단위의 점선 사이 간격입니다.")]
        public float dashGap=3.5f;
        [Range(0,.3f),Tooltip("흰색 횡단보도 페인트 경계의 미세한 노멀 요철 강도입니다.")]
        public float crosswalkEmboss=.045f;
        [Tooltip("전투 배경의 풀밭에만 돌과 수목을 배치합니다.")]
        public bool scatterNature=true;
        [Tooltip("시각 메시와 재질만 복사할 재사용 소품입니다. 충돌체나 동작 스크립트는 복사하지 않습니다.")]
        public GameObject[] naturePrefabs;
        [Range(0,100),Tooltip("전투 한 배경의 최대 자연 소품 수입니다. 안전한 풀밭 면적이 적으면 더 적게 배치됩니다.")]
        public int propLimit=28;
        [Min(1),Tooltip("자연 소품 사이의 최소 거리입니다.")]
        public float propSpacing=4;
        [Tooltip("같은 조우 좌표는 같은 소품 배치를 사용합니다. Unity 전역 난수 상태를 변경하지 않습니다.")]
        public int seed=731;
    }
}
