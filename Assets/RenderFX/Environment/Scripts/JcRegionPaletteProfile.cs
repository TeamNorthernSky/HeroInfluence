using System.Collections.Generic;
using UnityEngine;

namespace JC.Env
{
    /// <summary>
    /// 지역 존 하나 — 월드 XZ 원형 영역에 색조를 배정한다. 존이 겹치면 가중 평균, 가장자리는 페더로 부드럽게 이어진다.
    /// </summary>
    [System.Serializable]
    public class JcRegionZone
    {
        [Tooltip("표시용 이름.")]
        public string label = "Zone";

        [Tooltip("존 중심 (월드 X, Z).")]
        public Vector2 center;

        [Tooltip("반경 (월드 유닛). 이 밖에서는 영향 0.")]
        [Min(0.1f)] public float radius = 12f;

        [Tooltip("페더 비율 — 0 = 경계가 딱 끊김, 1 = 중심에서부터 서서히 사라짐.")]
        [Range(0f, 1f)] public float feather = 0.6f;

        [Tooltip("이 존의 목표 색조. 명도는 셰이더가 A축 결과를 보존하므로 여기선 색조·채도만 의미가 있다.")]
        public Color color = new Color(0.55f, 0.75f, 0.30f, 1f);

        [Tooltip("겹침 시 우선도 겸 강도.")]
        [Range(0f, 2f)] public float weight = 1f;
    }

    /// <summary>
    /// 지역 팔레트 프로파일 — 「어느 구역이 어떤 색조를 띠는가」의 정본(B축).
    ///
    /// ★260908 멘토링 반영: 색은 모델에 굽지 않고 좌표가 결정한다. 이 SO 가 존 목록을 소유하고
    ///   <see cref="Bake"/> 가 소형 텍스처(RGB=색조, A=커버리지)로 구워 <see cref="JcRegionPaletteController"/> 가
    ///   전역 셰이더 변수 <c>_JcRegionTex</c>/<c>_JcRegionBounds</c> 로 밀어 넣는다.
    ///   → 나무 재질이 몇 개든 지역 분포는 여기 한 곳에서 바뀐다. 씬이 덮여도 프로파일이 살아 있으면 복원된다
    ///   (환경 프로파일과 같은 「자산이 값을 소유」 설계).
    ///
    /// 통제 규칙(교수님): 너무 형형색색 금지 — 존 색은 녹색 계열 안에서 움직이는 것이 기본. 단풍 존은 옵트인.
    /// </summary>
    [CreateAssetMenu(menuName = "JC Environment/지역 팔레트", fileName = "REGION_Palette")]
    public class JcRegionPaletteProfile : ScriptableObject
    {
        [Header("① 베이크 범위 (월드 XZ) — 맵 바운드 + 외곽 밴드까지 덮을 것")]
        [Tooltip("x,y = 최소 X,Z / width,height = 크기. 이 밖은 존 영향이 없는 것으로 취급된다(클램프).")]
        public Rect worldBounds = new Rect(-20.5f, -20.5f, 143f, 120f);

        [Header("② 베이크 해상도")]
        [Tooltip("텍셀 수(정방). 128 이면 143 유닛 맵에서 텍셀 ≈ 1.1 유닛 — 존 전이가 부드러워 충분하다.")]
        [Range(32, 512)] public int resolution = 128;

        [Header("③ 존 목록")]
        public List<JcRegionZone> zones = new List<JcRegionZone>();

        // ─────────────────────────────────────────────────────────────
        // ④ 나무 재질 값 — ★260908 사용자 요청: 재질(M_Tree_RegionTint)을 직접 열지 않고 여기서 조절.
        //    환경 프로파일과 같은 구조: 프로파일이 값을 소유하고 컨트롤러가 재질에 기록한다(재질 = 출력물).
        // ─────────────────────────────────────────────────────────────
        [Header("④ 나무 재질 — A축(그루 안 그라데이션)")]
        [Tooltip("수관 하단색.")] public Color bottomColor = new Color(0.13f, 0.32f, 0.15f);
        [Tooltip("수관 상단색.")] public Color topColor = new Color(0.66f, 0.86f, 0.32f);
        [Tooltip("그라데이션 높이(메시 로컬 단위). 나무 높이보다 조금 작게 두면 꼭대기가 완전한 상단색이 된다.")] public float gradientHeight = 1.6f;
        [Tooltip("하단 기준 오프셋 — 줄기 높이만큼 음수로 주면 수관 밑동부터 그라데이션이 시작된다.")] public float gradientBias = -0.45f;
        [Tooltip("곡률 (1=선형, >1 하단색 영역이 넓어짐).")] [Range(0.2f, 4f)] public float gradientPower = 1.4f;
        [Tooltip("하단 음영 강도.")] [Range(0f, 1f)] public float bottomShade = 0.4f;

        [Header("④ 나무 재질 — B축 통제")]
        [Tooltip("지역색 강도 (0 = 존 무시).")] [Range(0f, 1f)] public float regionStrength = 1f;
        [Tooltip("채도 상한 — 형형색색 방지.")] [Range(0f, 1f)] public float saturationCap = 0.75f;
        [Tooltip("표면 Smoothness (무광 미니어처 = 0.1~0.2).")] [Range(0f, 1f)] public float smoothness = 0.15f;

        private static readonly int ID_Bottom = Shader.PropertyToID("_BottomColor");
        private static readonly int ID_Top = Shader.PropertyToID("_TopColor");
        private static readonly int ID_GradH = Shader.PropertyToID("_GradientHeight");
        private static readonly int ID_GradB = Shader.PropertyToID("_GradientBias");
        private static readonly int ID_GradP = Shader.PropertyToID("_GradientPower");
        private static readonly int ID_Shade = Shader.PropertyToID("_BottomShade");
        private static readonly int ID_Strength = Shader.PropertyToID("_RegionStrength");
        private static readonly int ID_SatCap = Shader.PropertyToID("_SaturationCap");
        private static readonly int ID_Smooth = Shader.PropertyToID("_Smoothness");

        /// <summary>④ 값을 재질에 기록한다(프로퍼티가 있는 것만).</summary>
        public void ApplyToMaterial(Material m)
        {
            if (m == null) return;
            if (m.HasProperty(ID_Bottom)) m.SetColor(ID_Bottom, bottomColor);
            if (m.HasProperty(ID_Top)) m.SetColor(ID_Top, topColor);
            if (m.HasProperty(ID_GradH)) m.SetFloat(ID_GradH, gradientHeight);
            if (m.HasProperty(ID_GradB)) m.SetFloat(ID_GradB, gradientBias);
            if (m.HasProperty(ID_GradP)) m.SetFloat(ID_GradP, gradientPower);
            if (m.HasProperty(ID_Shade)) m.SetFloat(ID_Shade, bottomShade);
            if (m.HasProperty(ID_Strength)) m.SetFloat(ID_Strength, regionStrength);
            if (m.HasProperty(ID_SatCap)) m.SetFloat(ID_SatCap, saturationCap);
            if (m.HasProperty(ID_Smooth)) m.SetFloat(ID_Smooth, smoothness);
        }

        /// <summary>재질의 현재 값을 ④ 로 읽어 온다(최초 결선 시 값 보존용).</summary>
        public void CaptureFromMaterial(Material m)
        {
            if (m == null) return;
            if (m.HasProperty(ID_Bottom)) bottomColor = m.GetColor(ID_Bottom);
            if (m.HasProperty(ID_Top)) topColor = m.GetColor(ID_Top);
            if (m.HasProperty(ID_GradH)) gradientHeight = m.GetFloat(ID_GradH);
            if (m.HasProperty(ID_GradB)) gradientBias = m.GetFloat(ID_GradB);
            if (m.HasProperty(ID_GradP)) gradientPower = m.GetFloat(ID_GradP);
            if (m.HasProperty(ID_Shade)) bottomShade = m.GetFloat(ID_Shade);
            if (m.HasProperty(ID_Strength)) regionStrength = m.GetFloat(ID_Strength);
            if (m.HasProperty(ID_SatCap)) saturationCap = m.GetFloat(ID_SatCap);
            if (m.HasProperty(ID_Smooth)) smoothness = m.GetFloat(ID_Smooth);
        }

        /// <summary>
        /// 존 목록을 텍스처로 굽는다. <paramref name="reuse"/> 가 크기·포맷이 맞으면 재사용(GC·에디터 잡음 방지).
        /// RGB = 가중 평균 색조(sRGB), A = 커버리지 합(0~1 클램프). 존이 없는 곳은 (0,0,0,0).
        /// </summary>
        public Texture2D Bake(Texture2D reuse)
        {
            int res = Mathf.Clamp(resolution, 32, 512);
            var tex = reuse;
            if (tex == null || tex.width != res || tex.height != res || tex.format != TextureFormat.RGBA32)
            {
                if (tex != null) DestroyImmediate(tex);
                tex = new Texture2D(res, res, TextureFormat.RGBA32, false, false)
                {
                    name = "JcRegionTex (baked)",
                    hideFlags = HideFlags.DontSave,
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
            }

            var px = new Color32[res * res];
            float sx = worldBounds.width / res, sz = worldBounds.height / res;
            for (int y = 0; y < res; y++)
            {
                float wz = worldBounds.yMin + (y + 0.5f) * sz;
                for (int x = 0; x < res; x++)
                {
                    float wx = worldBounds.xMin + (x + 0.5f) * sx;
                    Vector3 acc = Vector3.zero; float wsum = 0f;
                    for (int i = 0; i < zones.Count; i++)
                    {
                        var z = zones[i];
                        if (z == null || z.radius <= 0f) continue;
                        float d = Vector2.Distance(new Vector2(wx, wz), z.center);
                        if (d >= z.radius) continue;
                        float inner = z.radius * (1f - z.feather);
                        float w = d <= inner ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - inner) / Mathf.Max(z.radius - inner, 1e-4f));
                        w *= z.weight;
                        acc += new Vector3(z.color.r, z.color.g, z.color.b) * w;
                        wsum += w;
                    }
                    Color c = wsum > 0f ? new Color(acc.x / wsum, acc.y / wsum, acc.z / wsum, Mathf.Clamp01(wsum)) : Color.clear;
                    px[y * res + x] = c;
                }
            }
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        /// <summary>셰이더용 바운드 벡터 — x,y = min XZ / z,w = 1/size.</summary>
        public Vector4 BoundsVector => new Vector4(
            worldBounds.xMin, worldBounds.yMin,
            1f / Mathf.Max(worldBounds.width, 1e-3f), 1f / Mathf.Max(worldBounds.height, 1e-3f));
    }
}
