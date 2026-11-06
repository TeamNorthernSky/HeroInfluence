using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    // JC | 2026-11-05 | 원본 재질을 보존하는 보스전 표면 보정입니다. 낮밤 조명과 전투 판정은 변경하지 않습니다.
    [CreateAssetMenu(menuName = "JC/전투/보스전 실내 표면")]
    public sealed class JcBossSurfaceProfile : ScriptableObject
    {
        [Tooltip("기존 URP 조명을 받는 보스전 전용 표면 셰이더입니다.")]
        public Shader surfaceShader;
        [InspectorName("타일 한 변 길이"), Range(1,10), Tooltip("큼직한 바닥 타일의 월드 길이입니다.")]
        public float tileSize = 4;
        [InspectorName("줄눈 폭"), Range(.005f,.1f), Tooltip("타일 사이의 얇은 줄눈 폭입니다.")]
        public float jointWidth = .025f;
        [InspectorName("줄눈 명암"), Range(0,.4f), Tooltip("바닥 색에 비해 줄눈이 어두워지는 정도입니다.")]
        public float jointContrast = .12f;
        [InspectorName("얕은 줄눈 요철"), Range(0,.08f), Tooltip("실제 메시 높이를 바꾸지 않는 미세한 표면 노멀입니다. 0이면 완전히 평평합니다.")]
        public float jointNormal = .018f;
        [InspectorName("타일 색 변화"), Range(0,.08f), Tooltip("타일별 색 차이입니다. 작은 값으로 평평한 카툰 느낌을 유지합니다.")]
        public float tileVariation = .012f;
        [InspectorName("표면별 설정"), Tooltip("같은 재질에 여러 항목이 맞으면 아래쪽 항목이 우선합니다. 원본 재질 에셋은 수정하지 않습니다.")]
        public List<Surface> surfaces = new List<Surface>();

        [Serializable]
        public sealed class Surface
        {
            [InspectorName("원본 재질"), Tooltip("보스 무대에서 보정할 기존 재질입니다.")] public Material source;
            [InspectorName("렌더러 경로"), Tooltip("무대 상대 경로입니다. 비우면 해당 재질을 사용하는 무대 렌더러 모두에 적용합니다.")] public string rendererPath;
            [InspectorName("바닥 줄눈"), Tooltip("이 표면의 윗면에만 큼직한 타일 줄눈을 적용합니다.")] public bool floorTiles;
            [InspectorName("매끄러움"), Range(0,1), Tooltip("낮을수록 반사가 넓게 퍼집니다.")] public float smoothness = .18f;
            // JC | 2026-11-05 | 홀스톤·웜스톤을 더 강하게 조절할 수 있도록 상한만 확장합니다. 기존 저장값과 초기 외형은 유지합니다.
            [InspectorName("반사량"), Range(0,1), Tooltip("직접광과 환경의 반사량입니다. 최대 1까지 강하게 조절하며 금속 장식은 원래 재질을 유지합니다.")] public float specular = .02f;
            [InspectorName("거칠기 변화"), Range(0,1), Tooltip("표면의 매끄러움 차이입니다. 큰 값에서는 거친 부분과 매끄러운 부분의 대비가 강해집니다.")] public float variation = .035f;
        }
    }
}
