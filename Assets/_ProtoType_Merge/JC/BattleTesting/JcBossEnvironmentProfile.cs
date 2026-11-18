using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | BE490에서만 사용하는 보정값입니다. 기본값은 기존 실내·도시 환경을 그대로 유지합니다.
    [CreateAssetMenu(menuName = "JC/전투/보스전 환경 프로필")]
    public sealed class JcBossEnvironmentProfile : JcIndoorEnvironmentProfile
    {
        // JC | 2026-11-09 | 추가 효과는 기본 비활성으로 시작하여 기존 보스 외형을 유지합니다.
        public JcBossEnvironmentProfile() { ssaoEnabled = false; }

        // JC | 2026-11-05 | 실내와 캐릭터는 보스 조명만 받고 도시의 낮밤 조명은 그대로 유지합니다.
        [Header("실내·캐릭터 조명 분리")]
        // JC | 2026-11-12 | 외부 환경 차단과 새 실내 전용 IBL의 역할을 구분합니다.
        [InspectorName("월드 조명 차단"), Tooltip("외부 태양광·환경광·환경 반사·자체 밝기 보정을 차단합니다. 창문광·추가 라이트와 켜진 실내 전용 IBL은 받습니다.")]
        public bool isolateInterior = true;
        // JC | 2026-11-18 | 기존 야간 저장값은 유지하면서 네 시간대별 보스 응답으로 확장합니다. 공통 캐릭터 색상·실제 광량과 독립적입니다.
        [Header("시간대별 · 보스 몸체 강조")]
        [UnityEngine.Serialization.FormerlySerializedAs("nightBossContrastEnabled")]
        [InspectorName("보스 몸체 강조 사용")] public bool bossEmphasisEnabled;
        [Range(0,.6f), InspectorName("보스 강조 대비"), Tooltip("0은 원래 명암입니다. 값을 올리면 율리아·증폭기 몸체의 어두운 면을 더 낮춥니다. 밝은 면과 기존 광원은 유지합니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("nightBossContrast")] public float bossContrast = .25f;
        [Range(.05f,1), InspectorName("암부 적용 범위"), Tooltip("실제 조명 계산 후 선형 밝기 기준입니다. 높일수록 중간 밝기까지 보정합니다. 공통 채도·밝기 보정 이전에 적용됩니다.")]
        [UnityEngine.Serialization.FormerlySerializedAs("nightBossContrastPivot")] public float bossContrastPivot = .18f;
        [Range(0,3), InspectorName("강조 스팟 수광 배율"), Tooltip("1은 기존 수광, 0은 강조 스팟의 기여만 제거합니다. 율리아·증폭기 몸체에만 적용하며 다른 광원·IBL·배경의 수광은 유지합니다.")]
        public float bossSpotReception = 1;
        [Range(0,3), InspectorName("강조 스팟 반사광 배율"), Tooltip("강조 스팟의 정반사 광택만 추가로 조절합니다. 0에서도 확산광은 유지합니다. 최종 정반사는 수광 배율 × 반사광 배율입니다.")]
        public float bossSpotReflection = 1;
        [InspectorName("창문 투사광"), Tooltip("실제 창문 메시의 크기와 위치를 사용합니다. 창문광·추가광·실내 IBL을 모두 끄면 실내는 검게 됩니다.")]
        public List<WindowLight> windows = new List<WindowLight>();

        [Serializable]
        public sealed class WindowLight
        {
            [InspectorName("이름"), Tooltip("창문 조명 이름입니다.")] public string label = "창문광";
            [InspectorName("사용"), Tooltip("이 창문의 투사광을 켭니다.")] public bool enabled = true;
            [InspectorName("창문 경로"), Tooltip("보스 무대 아래 GlassBay 메시 경로입니다.")] public string rendererPath;
            [InspectorName("밝기"), Min(0), Tooltip("창문에서 들어오는 실제 조명의 밝기입니다.")] public float intensity = 160;
            [InspectorName("색상"), ColorUsage(false), Tooltip("실내로 들어오는 빛의 색상입니다.")] public Color color = new Color(1,.93f,.83f);
            [InspectorName("아래로 기울기"), Range(1,75), Tooltip("실내를 향하는 빛의 하향 각도입니다.")] public float pitch = 25;
            [InspectorName("좌우 방향"), Range(-60,60), Tooltip("창 안쪽 정면 방향에서 좌우로 회전합니다.")] public float yaw;
            [InspectorName("창밖 광원 거리"), Min(1), Tooltip("창문 중심에서 투사 광원까지의 거리입니다. 클수록 빛이 평행하게 들어옵니다.")] public float standOff = 12;
            [InspectorName("도달 거리"), Min(1), Tooltip("광원에서 빛이 도달하는 월드 거리입니다.")] public float range = 48;
            // JC | 2026-11-17 | 창 경계 안팎으로 퍼지는 폭이며 큰 값일수록 인접 창문광이 완만하게 이어집니다.
            [InspectorName("가장자리 부드러움"), Range(.001f,.2f), Tooltip("창문 투사 마스크가 경계 안팎으로 번지는 폭입니다. 큰 값은 창 사이의 어두운 띠를 부드럽게 풀어 줍니다.")] public float softness = .16f;
            [InspectorName("그림자"), Tooltip("창틀과 캐릭터 등의 실제 그림자를 사용합니다. 현재 품질의 추가광 그림자 설정을 따릅니다.")] public LightShadows shadows = LightShadows.Soft;
            [InspectorName("그림자 강도"), Range(0,1), Tooltip("이 창문광의 그림자 강도입니다.")] public float shadowStrength = 1;
        }

        [Header("기존 실내·외부 환경에 더하는 보정")]
        [Min(0), Tooltip("기존 방향광 밝기에 곱합니다. 1이면 기존 밝기를 유지합니다.")]
        [InspectorName("기존 방향광 밝기 배율")] public float directionalIntensity = 1;
        [ColorUsage(false, true), Tooltip("기존 방향광 색에 곱합니다. 흰색이면 기존 색을 유지합니다.")]
        [InspectorName("기존 방향광 색상 보정")] public Color directionalTint = Color.white;
        [Min(0), Tooltip("기존 방향광 그림자 강도에 곱합니다. 1이면 기존 그림자를 유지합니다.")]
        [InspectorName("그림자 강도")] public float shadowStrength = 1;
        [Min(0), Tooltip("기존 환경광 밝기에 곱합니다. 1이면 현재 실내·외부 환경을 유지합니다.")]
        [InspectorName("환경광 밝기 배율")] public float ambientIntensity = 1;
        [ColorUsage(false, true), Tooltip("기존 환경광 색에 곱합니다. 추가 조명과 대비되는 음영 색을 조절할 수 있습니다.")]
        [InspectorName("환경광 색상 보정")] public Color ambientTint = Color.white;
        [Min(0), Tooltip("기존 환경 반사 강도에 곱합니다. 개별 재질의 반사 설정은 바꾸지 않습니다.")]
        [InspectorName("환경 반사 강도 배율")] public float reflectionIntensity = 1;

        [Header("보스 무대 추가 라이트")]
        [Tooltip("무대 로컬 좌표로 배치합니다. 비어 있으면 추가 조명이 없습니다. 그림자는 현재 URP 품질 설정을 따릅니다.")]
        [InspectorName("추가 라이트 목록")] public List<LocalLight> lights = new List<LocalLight>();
        // JC | 2026-11-09 | 밝기 증폭이나 고속 반복 없이 이벤트마다 한 번만 옅은 보라색으로 왕복합니다.
        [Header("전투 조명 · 완만한 색 변화")]
        // JC | 2026-11-10 | 검수 전 행동 조명은 본게임에서 끄고, 공유 프로필의 테스트 설정은 별도로 유지합니다.
        [InspectorName("본게임 · 행동 조명 효과")] public bool battlePulseEnabled = false;
        [InspectorName("보스 테스트 · 행동 조명 효과")] public bool battlePulseInBossTestEnabled = true;
        [InspectorName("변화 색상"), ColorUsage(false)] public Color battlePulseColor = new Color(.65f,.45f,1);
        [InspectorName("색 혼합량"), Range(0,.3f)] public float battlePulseMix = .22f;
        [InspectorName("왕복 시간 · 실제 초"), Min(1.2f)] public float battlePulseSeconds = 1.4f;
        [InspectorName("최소 시작 간격 · 실제 초"), Min(4)] public float battlePulseInterval = 4;
        // JC | 2026-11-18 | 재질 교체 목록은 공통 JcBossSurfaceProfile로 이동하여 시간대 조명에서 분리합니다.

        public enum LocalLightType { Point, Spot }
        // JC | 2026-11-17 | 보스 전용 URP의 세 해상도 등급을 프로필에 저장합니다. 기존 광원은 중간 등급을 유지합니다.
        public enum LocalShadowQuality { Low, Medium, High }

        [Serializable]
        public sealed class LocalLight
        {
            // JC | 2026-11-09 | 임시 연출 대상 광원만 색 변화/보스 추적에 참여하며 저장된 기본색은 보존합니다.
            [InspectorName("전투 색 변화 참여")] public bool battlePulse;
            // JC | 2026-11-18 | 정면 배치의 X는 실제 몸통 중심에 맞추고 높이와 Z는 저장 위치로 조절합니다.
            [InspectorName("보스 정면 · 몸체 조준"), Tooltip("플레이 중 율리아 몸통 중심과 월드 X를 자동으로 맞춥니다. 높이와 Z는 저장 위치를 유지하므로 Z를 율리아보다 작은 쪽에 두면 -Z 정면에서 비춥니다. 회전은 몸통 중심을 향해 자동 계산합니다.")] public bool aimAtBoss;
            // JC | 2026-11-18 | 수광은 유지하고 이 조명의 그림자에서만 외곽 구조물을 제외합니다. 다른 광원/메시의 그림자 스위치는 변경하지 않습니다.
            [InspectorName("외곽 구조물 그림자 제외"), Tooltip("이 조명에 한해 외곽 기둥·상하단 벽·창호·몰딩의 그림자를 제외합니다. 캐릭터·증폭기·책상 그림자와 구조물 자체의 조명은 유지합니다.")]
            public bool excludeExteriorShadows;
            // JC | 2026-11-09 | 은행과 같은 조명 측 전용 설정이며 기본값은 꺼짐입니다.
            [InspectorName("플레이어 4종 후면 보조광"), Tooltip("일반 표면 대신 플레이어 4종의 뒷면만 비춥니다. 실제 Light 그림자는 만들지 않습니다.")] public bool playerBackFill;
            [Tooltip("하위 라이트 오브젝트 이름입니다.")] [InspectorName("이름")] public string label = "추가 조명";
            [Tooltip("이 라이트 사용 여부입니다.")] [InspectorName("사용")] public bool enabled = true;
            [Tooltip("점광원 또는 원뿔형 스폿 조명입니다.")] [InspectorName("조명 종류")] public LocalLightType type = LocalLightType.Spot;
            [Tooltip("보스 무대 기준 위치입니다.")] [InspectorName("무대 기준 위치")] public Vector3 position = new Vector3(0, 6, 0);
            [Tooltip("보스 무대 기준 회전입니다. 스폿 조명은 로컬 Z축 방향을 비춥니다.")] [InspectorName("무대 기준 회전")] public Vector3 rotation = new Vector3(90, 0, 0);
            [ColorUsage(false), Tooltip("조명 색입니다.")] [InspectorName("색상")] public Color color = Color.white;
            [Min(0), Tooltip("조명 밝기입니다.")] [InspectorName("밝기")] public float intensity = 1;
            [Min(.01f), Tooltip("조명이 도달하는 거리입니다.")] [InspectorName("범위")] public float range = 10;
            [Range(1, 179), Tooltip("스폿 조명의 바깥 각도입니다.")] [InspectorName("스폿 바깥 각도")] public float spotAngle = 60;
            [Range(0, 1), Tooltip("바깥 각도에 대한 중심부 각도 비율입니다.")] [InspectorName("스폿 중심부 비율")] public float innerSpotRatio = .6f;
            [Tooltip("그림자 방식입니다. 추가 라이트 그림자를 지원하는 품질 설정에서 동작합니다.")] [InspectorName("그림자 방식")] public LightShadows shadows = LightShadows.None;
            [Range(0, 1), Tooltip("이 조명의 그림자 불투명도입니다.")] [InspectorName("그림자 강도")] public float shadowStrength = 1;
            [Range(0, 2), Tooltip("그림자 깊이 오차 보정입니다.")] [InspectorName("그림자 깊이 보정")] public float shadowBias = .05f;
            [Range(0, 3), Tooltip("그림자 표면 오차 보정입니다.")] [InspectorName("그림자 표면 보정")] public float shadowNormalBias = .4f;
            [InspectorName("그림자 해상도 등급"), Tooltip("보스 전용 URP 기준 낮음 128 / 중간 256 / 높음 1024입니다.")]
            public LocalShadowQuality shadowQuality = LocalShadowQuality.Medium;
            [Tooltip("이 조명이 영향을 주는 레이어입니다.")] [InspectorName("영향 레이어")] public LayerMask cullingMask = -1;
        }

        [Serializable]
        public sealed class MaterialSlot
        {
            [Tooltip("이 재질 교체 사용 여부입니다.")] [InspectorName("사용")] public bool enabled = true;
            [Tooltip("무대 루트 아래 렌더러의 상대 경로입니다. 조절 인스펙터에서 렌더러를 드래그해 지정할 수 있습니다.")]
            [InspectorName("무대 렌더러 경로")] public string rendererPath;
            [Min(0), Tooltip("렌더러의 재질 슬롯 번호입니다. 첫 슬롯은 0입니다.")] [InspectorName("재질 슬롯")] public int slot;
            [Tooltip("보스 무대에만 적용할 재질입니다. 사용자 셰이더도 사용할 수 있습니다. 실행 시 복제하며 원본은 수정하지 않습니다.")]
            [InspectorName("교체 재질")] public Material material;
        }
    }
}
