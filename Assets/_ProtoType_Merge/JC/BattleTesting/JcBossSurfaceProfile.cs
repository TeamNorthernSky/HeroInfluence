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
        // JC | 2026-11-17 | 무대 X/Z 축의 월드 길이로 줄눈 시작 위치를 이동합니다. 메시 위치는 바꾸지 않습니다.
        [InspectorName("바닥 타일 위치 오프셋 (X / Z)")] public Vector2 tileOffset;

        // JC | 2026-11-17 | 별도 창틀은 유리·실내 색상 규칙과 사용자 재질 덮어쓰기 대상에서 제외합니다.
        public static bool IsWindowFrame(Renderer renderer, Transform stage)
        {
            var frame = stage ? stage.Find("JC_WindowFrames") : null;
            return frame && renderer && (renderer.transform == frame || renderer.transform.IsChildOf(frame));
        }
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
        // JC | 2026-11-18 | 실내 슬롯 교체도 시간대별 조명 프로필이 아닌 공통 표면 초안에 저장합니다.
        [InspectorName("공통 무대 재질 교체"), Tooltip("무대 상대 경로와 슬롯을 지정합니다. 창틀은 보호되며 유리는 시간대별 유리 프로필에서 조절하세요.")]
        public List<JcBossEnvironmentProfile.MaterialSlot> materials = new List<JcBossEnvironmentProfile.MaterialSlot>();

        // JC | 2026-11-09 | 내보내기가 생성한 별도 재질을 원본 설정에 명시적으로 대응시킵니다.
        // 이름 비교는 사용하지 않아 같은 이름의 도시 재질이나 다른 슬롯을 잘못 보정하지 않습니다.
        [HideInInspector] public List<ExportedMaterial> exportedMaterials = new List<ExportedMaterial>();
        [Serializable] public sealed class ExportedMaterial { public Material source, exported; }
        public Material ResolveSource(Material material)
        {
            foreach(var pair in exportedMaterials) if(pair!=null && pair.exported==material && pair.source) return pair.source;
            return material;
        }
        public void RegisterExported(Material source, Material exported)
        {
            if(!source || !exported || source==exported)return;
            exportedMaterials.RemoveAll(p=>p==null || p.exported==exported);
            exportedMaterials.Add(new ExportedMaterial {source=source,exported=exported});
        }

        [Serializable]
        public sealed class Surface
        {
            [InspectorName("원본 재질"), Tooltip("보스 무대에서 보정할 기존 재질입니다.")] public Material source;
            [InspectorName("렌더러 경로"), Tooltip("무대 상대 경로입니다. 비우면 해당 재질을 사용하는 무대 렌더러 모두에 적용합니다.")] public string rendererPath;
            // JC | 2026-11-17 | 초기 재질 색을 별도 보관해 프로필 저장 후에도 같은 기본색으로 되돌립니다.
            [InspectorName("표면 색상"), Tooltip("원본 무늬에 곱할 색입니다. 기본값으로 되돌리면 각 원본 슬롯의 기존 색을 그대로 사용합니다.")]
            public Color color = Color.white;
            [HideInInspector] public Color defaultColor = Color.white;
            [HideInInspector] public bool colorInitialized;
            public void ResetColor() { color = defaultColor; }
            // JC | 2026-11-17 | 새로 추가한 항목도 현재 원본색에서 시작하며 저장된 기준색은 다시 덮어쓰지 않습니다.
            public void EnsureColor()
            {
                if (colorInitialized || !source) return;
                color = defaultColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                colorInitialized = true;
            }
            [InspectorName("색상만 조절"), Tooltip("원본 재질의 광택·금속 반사를 유지하고 색상만 변경합니다.")]
            public bool colorOnly;
            // JC | 2026-11-17 | 연장 바닥은 같은 바닥 색과 줄눈 설정을 사용하되 기둥에는 전파하지 않습니다.
            [InspectorName("추가 렌더러 경로")] public string[] additionalRendererPaths = Array.Empty<string>();
            public bool Matches(Renderer renderer, Transform stage)
            {
                if (string.IsNullOrEmpty(rendererPath) || stage.Find(rendererPath) == renderer.transform) return true;
                if (additionalRendererPaths != null)
                    foreach (var path in additionalRendererPaths)
                        if (!string.IsNullOrEmpty(path) && stage.Find(path) == renderer.transform) return true;
                return false;
            }
            [InspectorName("바닥 줄눈"), Tooltip("이 표면의 윗면에만 큼직한 타일 줄눈을 적용합니다.")] public bool floorTiles;
            [InspectorName("매끄러움"), Range(0,1), Tooltip("낮을수록 반사가 넓게 퍼집니다.")] public float smoothness = .18f;
            // JC | 2026-11-05 | 홀스톤·웜스톤을 더 강하게 조절할 수 있도록 상한만 확장합니다. 기존 저장값과 초기 외형은 유지합니다.
            [InspectorName("반사량"), Range(0,1), Tooltip("직접광과 환경의 반사량입니다. 최대 1까지 강하게 조절하며 금속 장식은 원래 재질을 유지합니다.")] public float specular = .02f;
            [InspectorName("거칠기 변화"), Range(0,1), Tooltip("표면의 매끄러움 차이입니다. 큰 값에서는 거친 부분과 매끄러운 부분의 대비가 강해집니다.")] public float variation = .035f;
        }
    }
}
