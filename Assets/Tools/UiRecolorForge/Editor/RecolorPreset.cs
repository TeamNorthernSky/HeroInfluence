using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.UiRecolor
{
    /// <summary>
    /// 색역 슬롯 하나 — 「어떤 색을(게이트) 어떻게 옮기는가(편집)」 한 벌.
    /// 마스크 w = hue 게이트 × 채도 게이트 × 명도 게이트(옵션). 스무딩 = 각 게이트의 감쇠 폭.
    /// </summary>
    [Serializable]
    public class RecolorSlot
    {
        public bool enabled = true;
        public string label = "슬롯";

        [Tooltip("기준색(스포이드) — 유사도 판정의 중심")]
        public Color referenceColor = new Color(0.1f, 0.3f, 1f, 1f);

        // ── 게이트(임계값) ──
        [Tooltip("완전 선택 반경(°) — 기준색과 이 hue 거리 이내는 가중치 1")]
        public float hueRadius = 25f;
        [Tooltip("감쇠 폭(°) — 반경 밖에서 0까지 부드럽게 떨어지는 구간(경계 스무딩)")]
        public float hueFalloff = 20f;
        [Tooltip("채도 하한(OKLab C) — 미만은 제외. 무채색(흰 텍스트·검정 프레임)의 hue 노이즈 차단")]
        public float chromaMin = 0.03f;
        [Tooltip("채도 게이트 감쇠 폭")]
        public float chromaSoft = 0.04f;
        [Tooltip("명도 창 게이트 사용 — 아주 어두운/밝은 영역을 조절에서 제외하고 싶을 때만")]
        public bool useLightGate = false;
        public float lightMin = 0f;
        public float lightMax = 1f;
        [Tooltip("명도 창 감쇠 폭")]
        public float lightSoft = 0.08f;

        // ── 편집 ──
        [Tooltip("절대 모드: 앵커(기준색/배치 자동검출)→목표색 매핑. 상대 모드: 아래 노브의 델타 적용")]
        public bool absoluteMode = false;
        [Tooltip("절대 모드 목표색")]
        public Color targetColor = new Color(0.1f, 0.3f, 1f, 1f);
        [Tooltip("상대: 색상 회전(°)")]
        public float hueShift = 0f;
        [Tooltip("상대: 채도 배율")]
        public float chromaScale = 1f;
        [Tooltip("상대: 채도 오프셋(OKLab C)")]
        public float chromaOffset = 0f;
        [Tooltip("상대: 명도 배율")]
        public float lightScale = 1f;
        [Tooltip("상대: 명도 오프셋")]
        public float lightOffset = 0f;
        [Tooltip("색 조임(0~1) — 마스크에 잡힌 픽셀의 색상 산포를 대표색 쪽으로 당김(채도는 절반 강도). AI 잡색 정리용, 0=끔")]
        public float tighten = 0f;

        // ── 배치 ──
        [Tooltip("배치 앵커 검출용 넓은 hue 창(°) — 이 창 안 픽셀들의 대표색을 그 이미지의 앵커로 삼는다")]
        public float anchorWindow = 45f;
    }

    /// <summary>
    /// 경계 혼합 보정(2색 언믹싱) 설정 — 두 슬롯 기준색을 잇는 선분 근처의 픽셀을
    /// 「A색 α + B색 (1-α) 혼합」으로 분해해, 편집된 양 끝색으로 재합성한다.
    /// 극단 변화 시 경계 안티에일리어싱 띠가 옛 색으로 잔류하는 문제의 근본 해법.
    /// </summary>
    [Serializable]
    public class BoundaryUnmix
    {
        public bool enabled = false;
        [Tooltip("경계를 이루는 슬롯 A(예: 파랑 본체)")]
        public int slotA = 0;
        [Tooltip("경계를 이루는 슬롯 B(예: 노랑 테두리 — 편집 없이 기준색만 등록해도 됨)")]
        public int slotB = 1;
        [Tooltip("선분에서 이 거리(linear RGB) 이내면 경계 혼합으로 간주")]
        public float distRadius = 0.08f;
        [Tooltip("거리 게이트 감쇠 폭")]
        public float distFalloff = 0.08f;
        [Tooltip("잡색 억제(0~1) — 경계 픽셀에서 순수한 두 색 혼합을 벗어난 성분(원본 잡음)을 감쇠. 1=완전 재합성")]
        public float residualSuppress = 0f;
        [Tooltip("경계 평활 반경(px) — 혼합비를 공간 평활화해 뭉개진 전이를 매끈한 램프로 재구성. 0=끔")]
        public int smoothRadius = 0;
    }

    /// <summary>
    /// 편집 파라미터 전체(JSON 직렬화 대상). 격리 작업 폴더 모델이라 SO 가 아닌 JSON —
    /// 작업 폴더(Source/Out/preset.json)가 자기완결로 이동 가능해야 한다.
    /// </summary>
    [Serializable]
    public class RecolorPreset
    {
        public List<RecolorSlot> slots = new List<RecolorSlot>();

        public BoundaryUnmix boundary = new BoundaryUnmix();

        [Tooltip("배치: 앵커-기준색 ΔE(OKLab) 가 이 값을 넘으면 그 슬롯은 스킵+플래그(조용한 오적용 방지)")]
        public float batchSkipDeltaE = 0.12f;

        [Tooltip("저장 시 미세 디더 — 8bit 그라데이션 밴딩 완화")]
        public bool dither = true;

        public const int MaxSlots = 4;

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        public static RecolorPreset FromJson(string json)
        {
            var p = JsonUtility.FromJson<RecolorPreset>(json);
            return p ?? new RecolorPreset();
        }
    }
}
