using UnityEngine;
namespace JC.BattleTesting
{
    // JC | 2026-11-06 | 은행 PBR 툰만 저장합니다. 후면 보조광 저장값은 은행 환경 조명 목록으로 이관했습니다.
    [CreateAssetMenu(menuName = "JC/전투/은행 PBR 툰 프로필")]
    public sealed class JcBankToonProfile : ScriptableObject
    {
        [HideInInspector] public Shader surfaceShader;
        [InspectorName("툰 셰이딩 사용"), Tooltip("끄면 기존 연속 명암으로 돌아갑니다. 금속·광택·줄눈·접점 음영은 유지합니다.")]
        public bool toonEnabled = true;
        [InspectorName("배경에 적용"), Tooltip("은행 배경의 PBR 확산 명암만 단계화합니다. 반사 하이라이트는 유지합니다.")]
        public bool background = true;
        [InspectorName("캐릭터에 적용"), Tooltip("기존 전투 캐릭터 셰이더의 명암에 같은 단계 설정을 적용합니다.")]
        public bool characters = true;
        [Range(0,1), InspectorName("툰 혼합 강도"), Tooltip("0은 기존 명암, 1은 단계화한 명암입니다. 은행 본게임·테스트가 공유합니다.")]
        public float strength = .45f;
        [Range(2,4), InspectorName("명암 단계 수"), Tooltip("2는 밝음·어두움, 3~4는 중간 단계를 포함합니다.")]
        public int steps = 3;
        [Range(.01f,.49f), InspectorName("단계 경계 부드러움"), Tooltip("작을수록 경계가 선명하고 클수록 부드럽게 이어집니다.")]
        public float softness = .12f;
        [ColorUsage(false), InspectorName("툰 음영 색"), Tooltip("툰 명암의 어두운 쪽 확산광에 곱합니다. 흰색이면 색상 변화가 없습니다.")]
        public Color shadowTint = new Color(.9f,.93f,1);
    }
}
