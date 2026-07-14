using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// 테스트베드 화염 셰이더 공용 커스텀 인스펙터.
/// 각 프로퍼티에 호버 툴팁(설명 + 적정값)을 붙인다.
/// 셰이더에 `CustomEditor "FlameShaderGUI"` 를 추가하면 적용됨.
/// </summary>
public class FlameShaderGUI : ShaderGUI
{
    static readonly Dictionary<string, string> Tips = new Dictionary<string, string>
    {
        // 색
        {"_ColorLow",  "불꽃 하단/차가운 색 (HDR). 보통 진한 빨강."},
        {"_ColorMid",  "불꽃 중간 색 (HDR). 주황."},
        {"_ColorHigh", "불꽃 상단/뜨거운 색 (HDR). 노랑~흰."},
        {"_EdgeColor", "혀 끝 핫 엣지 색 (HDR). 밝은 노랑/흰."},
        {"_Color",     "색 (HDR)."},
        {"_ColorCore", "코어 심부 색 (HDR). 진한 빨강."},
        {"_ColorRim",  "외곽/뜨거운 색 (HDR). 노랑."},
        // 공통
        {"_Emission",  "발광 강도. 적정 1.5~3. 높을수록 밝고 Bloom 강함."},
        {"_EmissionStrength", "발광 강도. 적정 1.5~3."},
        {"_NoiseScale","불꽃 노이즈 밀도. 높을수록 혀가 얇고 잘게 많아짐. 적정 1.5~4."},
        {"_RiseSpeed", "불꽃이 위로 흐르는 속도. 적정 2~4."},
        {"_NoiseSpeed","노이즈 상승 속도. 적정 0.5~1.5."},
        {"_SwirlSpeed","좌우로 휘감기는 회전 속도. 적정 0~0.6."},
        {"_Opacity",   "전체 불투명도. 낮출수록 투명/은은. 적정 0.7~1."},
        // FireFlameClip
        {"_Cutoff",    "불꽃 임계값. 높을수록 불꽃이 적게(틈 많이) 남아 또렷한 혀. 적정 0.4~0.55."},
        {"_TopBias",   "위로 갈수록 불꽃을 깎는 정도. 높을수록 위로 빨리 가늘어지며 소멸(낮고 뾰족). 적정 0.2~1.0."},
        {"_EdgeWidth", "혀 가장자리 부드러움. 낮을수록 또렷/날카로움. 적정 0.03~0.08."},
        {"_EdgeHot",   "혀 끝 핫컬러 폭. 적정 0.1~0.2."},
        {"_HeightMin", "메시 오브젝트 Y 하단값(높이 0 기준). 메시 bounds.min.y에 맞춤."},
        {"_HeightMax", "메시 오브젝트 Y 상단값(높이 1 기준). 메시 bounds.max.y에 맞춤."},
        {"_VerticalStretch", "노이즈 세로 늘임. 낮을수록 길쭉한 streak. 적정 0.15~0.4."},
        {"_BaseFill",  "하단 상시 채움량. 0=없음, 1=완전. 하단 반구를 항상 덮음. 적정 0~0.7."},
        {"_BaseHeight","상시 채움 영역의 높이. 낮을수록 바닥만. 적정 0.1~0.4."},
        {"_TopCut",    "이 높이 위로는 안 그림. 1.2=컷 없음. 낮출수록 위를 잘라냄."},
        {"_TopCutBand","상단 컷 경계 부드러움. 넓으면 그라데이션. 적정 0.06~0.3."},
        {"_BaseJagged","하단 채움 경계의 들쭉날쭉(뾰족)함. 0=매끈, 1=강한 지그재그."},
        // FireVolume (레이마칭)
        {"_Density",   "볼륨 밀도. 높을수록 진함. 적정 2~5."},
        {"_Threshold", "밀도 임계값. 낮을수록 빽빽. 적정 0.25~0.45."},
        {"_AngleScroll","불꽃 혀가 도는 속도(각도). 적정 0~0.2."},
        {"_ShapeNoise","형태 노이즈 흔들림. 적정 0~0.2."},
        {"_DetailNoise","고주파 터뷸런스 디테일 양. 높을수록 휘날리는 잔결이 거칠게 추가됨. 적정 0.3~0.7."},
        {"_DetailScale","터뷸런스 잔결의 밀도(주파수). 높을수록 더 잘게. 적정 2.5~5."},
        {"_DensityBoost","밀도 상한 헤드룸 배수. _Density로 부족할 때 더 진하게. 적정 1~2."},
        {"_StretchY","물방울 몸통 가늘기. 1=구, 클수록 옆이 가늘어 세로로 길어 보임(컨테이너 안이라 클리핑 없음). 전체 길이는 오브젝트 Y스케일로. 적정 1.2~1.7."},
        {"_TopTaper","물방울 상단 뾰족함. 0=둥근 타원, 클수록 위가 점으로 모임. 적정 0.3~0.6."},
        {"_MirrorX","좌우(화면) 대칭. 1=카메라를 향한 평면 기준 완전 대칭(어느 각도서든), 0=자연 비대칭. 대칭이면 Swirl/AngleScroll 자동 off. 적정 0.8~1."},
        {"_Steps",     "셰이더별: 볼륨=레이마칭 스텝 수(24~36), 코어=카툰 밴딩 단계(0=off)."},
        {"_EdgeFade",  "셸 가장자리 페이드(레이마칭). 미사용 시 무시."},
        {"_FlameTex",  "불꽃 실루엣 프로파일 텍스처(가로=각도, 세로=높이, 흰색=불꽃 반경)."},
        // FireShell (M1)
        {"_TipNoiseScale", "꼭대기 화염의 잔노이즈 밀도. 높을수록 위쪽이 잘게 갈라짐. 적정 4~8."},
        {"_TipSharp",   "꼭대기 혀의 뾰족함/날카로움. 높을수록 들쭉날쭉(알파 경계 좁힘+버텍스 봉우리). 적정 1.2~3."},
        {"_FlameContrast", "내부 화염 명암 대비. 높을수록 밝은 곳 더 밝고 어두운 곳 더 어둡게. 적정 1~2.5."},
        {"_RimPower",   "Fresnel 외곽 집중도. 높을수록 얇은 테. 적정 1.5~4."},
        {"_RimStrength","외곽 화염 림 세기. 적정 0.5~1.5."},
        {"_FlameStrength","상단 혀 세기. 적정 1~2.5."},
        {"_Spread",     "혀가 전체를 감싸는지(낮음) 상단만(높음). 적정 0.3~0.8."},
        {"_RiseHeight", "상단 버텍스 상승 변위(솟구침). 적정 0~1.5."},
        // FireTongue
        {"_Dissolve",  "끝으로 갈수록 디졸브. 적정 0.3~0.6."},
        {"_TipFade",   "끝 페이드 강도. 적정 1~2.5."},
        {"_BaseFade",  "밑동 페이드인. 적정 0~0.2."},
        {"_Flicker",   "인스턴스별 위상(보통 스크립트가 설정)."},
        // FireOrbCore
        {"_BaseAlpha", "구체 기본 반투명도. 적정 0.2~0.4."},
        {"_Distort",   "도메인 워프(불규칙 일렁임). 적정 0~0.5."},
        {"_LitAmount", "씬 조명 수신 비율. 0=발광만, 1=조명 강함. 적정 0.3~0.6."},
        // AdditiveTrail
        {"_SoftEdge",  "폭 방향 부드러움. 적정 0.2~0.4."},
        {"_HeadFade",  "꼬리 페이드. 적정 1~2."},
    };

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        foreach (var p in properties)
        {
            if ((p.flags & MaterialProperty.PropFlags.HideInInspector) != 0)
                continue;

            string tip;
            string label = p.displayName;
            var content = Tips.TryGetValue(p.name, out tip)
                ? new GUIContent(label, tip)
                : new GUIContent(label);

            materialEditor.ShaderProperty(p, content);
        }

        EditorGUILayout.Space();
        materialEditor.RenderQueueField();
        materialEditor.EnableInstancingField();
        materialEditor.DoubleSidedGIField();
    }
}
