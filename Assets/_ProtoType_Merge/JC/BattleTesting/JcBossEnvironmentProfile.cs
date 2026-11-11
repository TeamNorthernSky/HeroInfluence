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
        [InspectorName("월드 조명 차단"), Tooltip("실내 표면과 캐릭터의 태양광·환경광·환경 반사·자체 밝기 보정을 차단합니다. 창문광과 아래 추가 라이트만 받습니다.")]
        public bool isolateInterior = true;
        [InspectorName("창문 투사광"), Tooltip("실제 창문 메시의 크기와 위치를 사용합니다. 모두 끄고 추가 라이트도 끄면 실내·캐릭터는 검게 됩니다.")]
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
            [InspectorName("가장자리 부드러움"), Range(.001f,.2f), Tooltip("창문 마스크 가장자리의 흐림 비율입니다.")] public float softness = .025f;
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
        [Header("보스 무대 전용 셰이딩")]
        [Tooltip("지정한 무대 렌더러 슬롯에만 재질 복제본을 적용합니다. 원본 재질·캐릭터·다른 전투에는 적용하지 않습니다.")]
        [InspectorName("무대 전용 재질 목록")] public List<MaterialSlot> materials = new List<MaterialSlot>();

        public enum LocalLightType { Point, Spot }

        [Serializable]
        public sealed class LocalLight
        {
            // JC | 2026-11-09 | 임시 연출 대상 광원만 색 변화/보스 추적에 참여하며 저장된 기본색은 보존합니다.
            [InspectorName("전투 색 변화 참여")] public bool battlePulse;
            [InspectorName("보스 몸체 조준"), Tooltip("위치는 유지하고 살아 있는 율리아 몸체를 향해 스폿 방향만 맞춥니다.")] public bool aimAtBoss;
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
