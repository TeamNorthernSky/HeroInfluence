using System;
using UnityEngine;

namespace JC.Indicators
{
    [Serializable]
    // JC | 2026-11-04 | 탐사 영역의 채움과 테두리를 부모 조절기에서 함께 관리해 하위 표시 설정이 저장값을 덮지 않게 합니다.
    public sealed class JcTriggerAreaSettings
    {
        [Tooltip("월드 단위의 테두리 형태입니다. 크기는 실제 셀 영역에서 자동 계산하며 부유 높이는 아래 최저 높이와 왕복 폭으로 계산합니다.")]
        public BattleCellBorderShape shape = new BattleCellBorderShape {
            size = Vector2.one, padding = .01f, cornerRadius = .16f, curveSegments = 14,
            outerWidth = .015f, mainWidth = .045f, innerWidth = .01f, depth = .025f
        };
        [Tooltip("바깥 보조선을 표시합니다. 보조선도 기존 트리거 색을 사용합니다.")] public bool showOuter = true;
        [Tooltip("안쪽 보조선을 표시합니다. 보조선도 기존 트리거 색을 사용합니다.")] public bool showInner = true;
        [Min(0), Tooltip("본체 테두리 RGB 밝기 배율입니다. 1은 원래 색상, 2는 두 배이며 보조선과 채움에는 영향을 주지 않습니다.")] public float intensity = 1;
        [Range(0, 1), Tooltip("본체 테두리의 최종 불투명도입니다. 0은 투명, 1은 완전 불투명이며 보조선과 채움에는 영향을 주지 않습니다.")] public float opacity = 1;
        [Min(0), Tooltip("바깥 보조선만의 RGB 밝기 배율입니다. 1은 원래 밝기입니다.")] public float outerIntensity = 1;
        [Range(0, 1), Tooltip("바깥 보조선만의 최종 불투명도입니다. 0은 투명, 1은 완전 불투명입니다.")] public float outerOpacity = 1;
        [Min(0), Tooltip("안쪽 보조선만의 RGB 밝기 배율입니다. 1은 원래 밝기입니다.")] public float innerIntensity = 1;
        [Range(0, 1), Tooltip("안쪽 보조선만의 최종 불투명도입니다. 0은 투명, 1은 완전 불투명입니다.")] public float innerOpacity = 1;
        // JC | 2026-11-04 | 가려진 선은 기존 부위별 알파에 배율을 곱하며 두 표면의 값을 독립 저장합니다.
        [Range(0, 1), Tooltip("건물에 가려진 선의 기존 불투명도에 곱할 배율입니다. 0이면 가려진 부분을 숨깁니다.")] public float buildingOccludedOpacity = .15f;
        [Range(0, 1), Tooltip("인도·연석에 가려진 선의 기존 불투명도에 곱할 배율입니다. 도로 높이는 유지합니다.")] public float sidewalkOccludedOpacity = .05f;
        // JC | 2026-11-04 | 채움의 인도 가림만 독립 배율로 조절하며 건물 투과는 허용하지 않습니다.
        [Range(0, 1), Tooltip("인도에 가려진 채움의 기존 불투명도에 곱할 배율입니다. 건물·캐릭터 뒤의 채움은 숨깁니다.")] public float fillSidewalkOccludedOpacity = .25f;
        [Min(.001f), Tooltip("왕복 운동의 최저점에서 메시 밑면과 지면 사이의 거리입니다. 바닥 깜빡임을 막는 작은 여유입니다.")] public float minimumHeight = .006f;
        [Min(0), Tooltip("위아래 운동의 한쪽 폭입니다. 최고점은 최저 높이 + 이 값의 두 배입니다.")] public float bobAmplitude = .03f;
        [Min(.1f), Tooltip("전투 그리드와 같은 공통 위상의 상하 왕복 주기(초)입니다.")] public float bobPeriod = 1.8f;
        [Tooltip("내부 채움을 표시합니다. 초기에는 꺼져 있습니다.")] public bool useFill;
        [Tooltip("채움에 기존 트리거 색상을 사용합니다. 끄면 아래 지정 색을 사용합니다.")] public bool fillUsesTriggerColor = true;
        [ColorUsage(false), Tooltip("지정 채움 RGB 색상입니다. 투명도는 채움 불투명도로만 조절합니다.")] public Color fillColor = Color.white;
        [Min(0), Tooltip("채움만의 RGB 밝기 배율입니다. 1은 원래 색상, 2는 두 배입니다.")] public float fillIntensity = 1;
        [Range(0, 1), Tooltip("채움만의 최종 불투명도입니다. 원본 색상 알파에 제한되지 않습니다. 0은 투명, 1은 완전 불투명이며 문양 텍스처의 알파는 유지합니다.")] public float fillOpacity = .2f;
        [Tooltip("선택적인 채움 문양입니다. 비워 두면 단색이며 제외된 셀과 구멍에는 채우지 않습니다.")] public Texture2D fillTexture;
        public JcTriggerAreaSettings Copy() => JsonUtility.FromJson<JcTriggerAreaSettings>(JsonUtility.ToJson(this));
    }

    [CreateAssetMenu(menuName = "JC/인디케이터/탐사 트리거 영역 프로필")]
    public sealed class JcTriggerAreaProfile : ScriptableObject
    {
        [Tooltip("명시적인 저장 버튼으로 확정한 탐사 전용 설정입니다. 전투 테두리 프로필과 공유하지 않습니다.")]
        public JcTriggerAreaSettings settings = new JcTriggerAreaSettings();
    }
}
