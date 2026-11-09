using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 은행 본게임/테스트가 공유하는 표면 설정입니다. 공용 색상표와 보스 프로필은 수정하지 않습니다.
    [CreateAssetMenu(menuName = "JC/전투/은행 재질 프로필")]
    public sealed class JcBankMaterialProfile : ScriptableObject
    {
        [InspectorName("표면별 설정")] public List<Surface> surfaces = new List<Surface>();

        [Serializable]
        public sealed class Surface
        {
            public string label;
            [InspectorName("은행 전용 원본 재질")] public Material source;
            [InspectorName("색상 보정"), ColorUsage(false), Tooltip("원래 텍스처 색에 곱합니다. 흰색이면 원래 색이며 공용 색상표는 변경하지 않습니다.")]
            public Color tint = Color.white;
            // JC | 2026-11-06 | 분리된 데스크 세 구간에서도 각 재질의 조절 효과를 안내합니다.
            [InspectorName("메탈릭"), Range(0, 1), Tooltip("0은 비금속, 1은 금속 반사입니다. 선택한 구간에만 적용합니다.")] public float metallic;
            [InspectorName("매끄러움·광택"), Range(0, 1), Tooltip("0은 거친 표면, 1은 선명한 광택입니다. 툰 사용 중에도 이 설정을 유지합니다.")] public float smoothness = .15f;
            [InspectorName("직접광 하이라이트"), Tooltip("조명에 의한 표면의 반짝임을 켜거나 끕니다.")] public bool specularHighlights = true;
            [InspectorName("환경 반사"), Tooltip("실내 반사 캡처를 표면에 반영할지 정합니다.")] public bool environmentReflections = true;
            // JC | 2026-11-05 | 벽 램프의 발광색/강도를 조절합니다. 강도 0인 다른 표면은 기존 표현을 유지합니다.
            [InspectorName("발광 색상"), ColorUsage(false), Tooltip("발광 강도가 0보다 클 때 자체 발광하는 색상입니다.")] public Color emissionColor = Color.white;
            [InspectorName("발광 강도"), Range(0, 8), Tooltip("자체 발광 밝기입니다. 주변을 비추는 실시간 Light의 광량과는 별개입니다.")]
            public float emissionIntensity;
            // JC | 2026-11-05 | 바닥 줄눈은 형상 높이를 바꾸지 않고 노멀맵으로만 표현합니다.
            [InspectorName("줄눈 노멀 강도"), Range(0, 3), Tooltip("줄눈 입체감입니다. 0은 평면, 1은 원래 노멀, 3까지 강조합니다. 메시 높이는 변하지 않습니다.")] public float normalStrength = .35f;
            // JC | 2026-11-05 | 흰 바닥 베이스에 줄눈만 어둡게 하며 입체감·틴트와 독립적으로 조절합니다.
            [InspectorName("줄눈 명암 강도"), Range(0, 1), Tooltip("0은 명암 없는 흰 바닥, 1은 검은 줄눈입니다. 타일 면의 틴트와 노멀 강도는 유지합니다.")]
            public float groutContrast;
            [InspectorName("타일 한 변 크기"), Range(2, 16), Tooltip("평탄한 바닥에서 반복되는 타일 한 변의 월드 길이입니다.")]
            public float tileSize = 8;
        }
    }
}
