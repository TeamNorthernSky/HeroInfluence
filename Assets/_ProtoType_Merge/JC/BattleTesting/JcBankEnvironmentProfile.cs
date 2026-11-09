using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 실외 값의 배율이 아닌 은행 전용 절대값입니다. 낮밤·보스 프로필과 독립적으로 저장합니다.
    [CreateAssetMenu(menuName = "JC/전투/은행 실내 조명 프로필")]
    public sealed class JcBankEnvironmentProfile : JcIndoorEnvironmentProfile
    {
        [Header("시간대와 무관한 실내 환경광")]
        [Tooltip("태양·하늘 대신 사용하는 고정 실내 간접광 색상입니다.")]
        [ColorUsage(false), InspectorName("실내 음영 색상")] public Color ambientColor = new Color(.65f, .68f, .73f);
        [Tooltip("실내 음영의 밝기입니다. 0이면 환경광을 끕니다.")]
        [Range(0, 2), InspectorName("실내 환경광 밝기")] public float ambientIntensity = .6f;
        [Header("실내 반사 캡처")]
        [Tooltip("실내에서 캡처한 반사의 강도입니다. 0이면 반사를 끕니다.")]
        [Range(0, 2), InspectorName("실내 반사 강도")] public float reflectionIntensity = .7f;
        [Tooltip("은행 배경 로컬 좌표의 실내 반사 캡처 위치입니다. 단위는 미터입니다.")]
        [InspectorName("반사 캡처 위치")] public Vector3 reflectionPosition = new Vector3(8, 5, -8);
        [Tooltip("실내 반사가 적용될 상자 영역의 크기입니다. 단위는 미터입니다.")]
        [InspectorName("반사 영역 크기")] public Vector3 reflectionSize = new Vector3(65, 26, 72);
        // JC | 2026-11-05 | 글리프 후광은 벽면 발광으로 표현하여 실제 광원 8개 구성과 금속 전면을 유지합니다.
        [Header("은행 문양 뒤 네온 후광")]
        [Tooltip("금속 문양 뒤 벽에 번지는 후광 색상입니다. 기본값은 흰색입니다.")]
        [ColorUsage(false), InspectorName("후광 색상")] public Color glyphHaloColor = Color.white;
        [Tooltip("후광 밝기입니다. 0이면 후광을 끕니다. 실제 라이트 개수에는 포함되지 않습니다.")]
        [Range(0, 4), InspectorName("후광 밝기")] public float glyphHaloIntensity = .85f;
        [Tooltip("문양 윤곽 밖으로 빛이 퍼지는 거리입니다. 단위는 미터입니다.")]
        [Range(.03f, .5f), InspectorName("후광 번짐 폭")] public float glyphHaloWidth = .23f;
        [Header("실내 조명 — 은행 배경 기준 좌표")]
        // JC | 2026-11-06 | 벽 램프 앞의 점광원 4개를 그림자 없는 천장 보조광으로 대체합니다.
        [Tooltip("은행 전용 Forward+에서 천장3·벽 램프용 천장 보조광4·금고1·월스폿3·주광1을 조절합니다. 보조광은 그림자를 끕니다. 본게임과 BankTestScene이 공유합니다.")]
        [InspectorName("실내 조명 목록")] public List<CeilingLight> lights = new List<CeilingLight>();

        // JC | 2026-11-05 | 기존 Spot/Point 저장값을 보존하고 시간대와 무관한 실내 주광을 뒤에 추가합니다.
        public enum LocalLightType { Spot, Point, Directional }

        [Serializable]
        public sealed class CeilingLight
        {
            // JC | 2026-11-06 | 이 항목은 플레이어 4종 후면 전용입니다. UI에서 범위를 넓히거나 주광으로 바꾸지 않습니다.
            [HideInInspector] public bool playerBackFill;
            [Tooltip("생성되는 라이트의 이름입니다. 천장·벽·금고 위치를 구분합니다.")]
            [InspectorName("이름")] public string label = "천장 조명";
            // JC | 2026-11-05 | 신규 월스폿의 외형만 조명 자식으로 연결합니다. 기존 벽/금고 메시와 분리됩니다.
            [Tooltip("조명과 함께 이동할 은행 전용 램프 외형입니다. 비워두면 기존 배경 메시를 사용합니다.")]
            public GameObject fixturePrefab;
            [Tooltip("이 조명만 켜거나 끕니다.")]
            [InspectorName("사용")] public bool enabled = true;
            // JC | 2026-11-05 | 기존 저장 형식의 Spot=0을 유지하며 벽 램프용 확산 점광원을 추가합니다.
            [Tooltip("Spot은 원뿔, Point는 주변 확산, Directional은 같은 방향의 실내 주광입니다.")]
            [InspectorName("조명 종류")] public LocalLightType type = LocalLightType.Spot;
            [Tooltip("은행 배경 로컬 기준 조명 위치입니다. Y는 천장 높이이며 단위는 미터입니다.")]
            [InspectorName("은행 기준 위치")] public Vector3 position = new Vector3(0, 14, 0);
            [Tooltip("은행 배경 로컬 기준 회전입니다. X=90이면 바닥을 향합니다.")]
            [InspectorName("은행 기준 회전")] public Vector3 rotation = new Vector3(90, 0, 0);
            [Tooltip("실제 빛의 색상입니다. 흰색은 노란 기 없는 백색광입니다.")]
            [ColorUsage(false), InspectorName("조명 색상")] public Color color = new Color(1, .96f, .88f);
            [Tooltip("빛의 밝기입니다. 0이면 빛을 끕니다. 방향광과 국소광은 밝기 단위가 달라 수치를 직접 비교하지 않습니다.")]
            [Range(0, 250), InspectorName("조명 밝기")] public float intensity = 100;
            [Tooltip("빛이 도달하는 최대 거리입니다. 단위는 미터입니다.")]
            [Range(1, 70), InspectorName("도달 거리")] public float range = 48;
            [Tooltip("원뿔 바깥 각도입니다. 값이 클수록 넓게 비춥니다. 단위는 도입니다.")]
            [Range(1, 170), InspectorName("바깥 각도")] public float spotAngle = 135;
            [Tooltip("빛의 밝기가 일정한 중심부의 비율입니다. 낮추면 가장자리가 부드러워집니다.")]
            [Range(0, 1), InspectorName("중심부 비율")] public float innerSpotRatio = .7f;
            [Tooltip("천장 조명의 그림자 방식입니다. None이면 그림자를 끕니다.")]
            [InspectorName("그림자")] public LightShadows shadows = LightShadows.Soft;
            [Tooltip("그림자 강도입니다. 0이면 그림자가 보이지 않습니다.")]
            [Range(0, 1), InspectorName("그림자 강도")] public float shadowStrength = .65f;
            [Tooltip("그림자 깊이 오차 보정입니다. 과하면 물체와 그림자가 떨어집니다.")]
            [Range(0, 2), InspectorName("그림자 깊이 보정")] public float shadowBias = .05f;
            [Tooltip("표면 방향의 그림자 오차 보정입니다. 과하면 빛이 새어 보일 수 있습니다.")]
            [Range(0, 3), InspectorName("그림자 표면 보정")] public float shadowNormalBias = .2f;
        }
    }
}
