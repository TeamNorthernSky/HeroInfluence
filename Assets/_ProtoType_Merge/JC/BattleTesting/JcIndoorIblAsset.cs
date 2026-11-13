using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-12 | 원본 HDR 환경과 오프라인으로 구운 확산광·거칠기별 반사를 묶습니다. 실행 중 재베이크하지 않습니다.
    [CreateAssetMenu(menuName = "JC/전투/실내 IBL 환경")]
    public sealed class JcIndoorIblAsset : ScriptableObject
    {
        public enum Input { NeutralRoom, CustomCubemap }
        [InspectorName("환경 원본"), Tooltip("중립 실내는 천장 패널과 벽·바닥의 가상 환경을 생성합니다. 사용자 큐브맵은 지정한 HDR 환경을 굽습니다.")]
        public Input input;
        [InspectorName("원본 HDR 큐브맵"), Tooltip("사용자 큐브맵 모드의 원본입니다. 변경 후 환경 데이터 굽기를 눌러야 적용됩니다. 읽기 가능 설정은 필요 없습니다.")]
        public Cubemap source;
        [HideInInspector] public Cubemap specular;
        [HideInInspector] public Vector4[] diffuse = new Vector4[9];
        [HideInInspector] public string bakedSource;
        public bool Ready => specular && diffuse != null && diffuse.Length == 9;
    }

    // JC | 2026-11-12 | 은행·보스 각 초안에 저장하여 같은 자산을 사용하더라도 조절값이 섞이지 않습니다.
    [System.Serializable]
    public sealed class JcIndoorIblSettings
    {
        [InspectorName("실내 IBL 사용"), Tooltip("실내 전용 환경광과 반사를 사용합니다. 끄면 기존 전투별 조명 방식으로 돌아갑니다.")]
        public bool enabled;
        [InspectorName("실내 환경 데이터"), Tooltip("미리 구운 HDR 큐브맵과 확산광 데이터입니다. 외부 스카이박스나 도시 원경은 바꾸지 않습니다.")]
        public JcIndoorIblAsset environment;
        [Min(0), InspectorName("환경광 밝기"), Tooltip("그늘의 확산광 밝기 배율입니다. 0은 확산 환경광을 끕니다. 직접 조명의 세기는 바뀌지 않습니다.")]
        public float diffuseIntensity = .35f;
        [Min(0), InspectorName("환경 반사 밝기"), Tooltip("금속·유광 표면의 IBL 반사 밝기 배율입니다. 0은 IBL 반사를 끕니다.")]
        public float specularIntensity = .5f;
        [ColorUsage(false), InspectorName("환경 색상"), Tooltip("확산광과 반사에 곱하는 색입니다. 흰색은 원래 HDR 환경 색상을 유지합니다.")]
        public Color tint = Color.white;
        [Range(-180,180), InspectorName("환경 회전"), Tooltip("월드 Y축 기준 회전 각도입니다. 환경광 방향과 반사 방향을 함께 회전합니다. 단위는 도입니다.")]
        public float rotation;
        [Range(0,1), InspectorName("은행 실내 반사 혼합"), Tooltip("은행만 적용: 0은 IBL 반사, 1은 기존 실내 캡처 반사입니다. 보스는 외부 반사 차단을 위해 항상 IBL만 사용합니다.")]
        public float bankProbeBlend;
    }
}
