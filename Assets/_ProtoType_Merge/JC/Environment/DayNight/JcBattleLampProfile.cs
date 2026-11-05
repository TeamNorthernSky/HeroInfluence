using System;
using UnityEngine;

namespace JC.Environment
{
    // JC | 2026-11-04 | 시간대별 전장 조명을 조절하고 가로등의 중심광과 주변광을 배치에 맞게 적용합니다.
    public sealed class JcBattleLampProfile : ScriptableObject
    {
        public const string ResourceName = "JC_BattleStreetlights";
        public GameObject lampPrefab;

        [Serializable]
        public sealed class Cone
        {
            [InspectorName("광량"), Min(0), Tooltip("이 광원이 더하는 빛의 세기입니다.")] public float intensity = 2;
            [InspectorName("조사각"), Range(1, 179), Tooltip("빛이 퍼지는 전체 각도입니다.")] public float angle = 60;
            [InspectorName("가장자리 부드러움"), Range(0, 1), Tooltip("조사각 중 중심에서 바깥으로 감쇠하는 비율입니다.")] public float softness = .6f;
        }

        [Serializable]
        public sealed class FieldFill
        {
            [InspectorName("사용"), Tooltip("가로등이 있는 야외 전장에 그림자 없는 보조광 하나를 켭니다.")] public bool enabled = true;
            [InspectorName("빛 색상")] public Color color = new Color(1f, .82f, .62f);
            [InspectorName("광량"), Min(0)] public float intensity = 3f;
            [InspectorName("도달 거리"), Min(.01f)] public float range = 24f;
            [InspectorName("가로등 쪽 이동 비율"), Range(0, 1)] public float towardLamp = .25f;
            [InspectorName("위치 오프셋"), Tooltip("전투 월드 단위이며 Y는 바닥에서의 높이입니다.")] public Vector3 offset = new Vector3(0, 5, 0);
        }

        [Serializable]
        public sealed class BulbGlow
        {
            [InspectorName("전구 발광"), Min(0), Tooltip("렌즈 재질에 더하는 HDR 발광 세기입니다.")] public float emission = 3f;
            [InspectorName("블룸 강도"), Min(0)] public float intensity = .12f;
            [InspectorName("블룸 밝기 기준"), Min(0)] public float threshold = 1.35f;
            [InspectorName("번짐 폭"), Range(0, 1)] public float scatter = .55f;
        }

        [Serializable]
        public sealed class Segment
        {
            [InspectorName("전장 보조광")] public FieldFill fill = new FieldFill();
            [InspectorName("근접 중심광 그림자"), Range(0, 1), Tooltip("진형 중앙에 가장 가까운 가로등 한 개의 부드러운 그림자 강도입니다. 0이면 끕니다.")] public float shadowStrength = .7f;
            [InspectorName("전구 발광과 블룸")] public BulbGlow glow = new BulbGlow();
            [InspectorName("점등"), Tooltip("이 구간에서 가로등을 켭니다.")] public bool lit;
            [InspectorName("빛 색상"), Tooltip("중심광과 주변광에 공통으로 적용합니다.")] public Color color = new Color(1, .87f, .7f);
            [InspectorName("도달 거리"), Min(.01f), Tooltip("전투 공간에서 빛이 도달하는 최대 거리입니다.")] public float range = 12;
            [InspectorName("중심 하이라이트")] public Cone core = new Cone();
            [InspectorName("스팟 주변광")] public Cone spill = new Cone { intensity = .6f, angle = 100, softness = .85f };
        }

        public Segment[] segments = { new Segment(), new Segment(), new Segment { lit = true }, new Segment { lit = true } };
    }
}
