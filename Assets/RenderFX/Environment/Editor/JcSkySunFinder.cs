using UnityEngine;
using UnityEditor;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 하늘 그림(HDRI) 속 <b>태양이 어디 있는지를 실측</b>하는 도구.
    ///
    /// ★왜 필요한가(260904 실증): 손으로 맞춘 <c>_Rotation</c> 은 <b>라이트를 건드리는 순간 낡는다</b>.
    ///   260903 에 맞춘 87 은 그때(라이트 yaw 128)는 맞았지만, 이후 yaw 를 50 으로 옮기자
    ///   그대로 <b>78° 어긋난 값</b>이 되어 그림자 방향과 하늘의 태양이 서로 다른 곳을 가리켰다.
    ///   그래서 정합을 값이 아니라 <b>관계식</b>으로 박고(컨트롤러), 그 관계식의 상수만 여기서 실측한다.
    ///
    /// ★측정 방식: 카메라를 실제로 그 방향으로 돌려 하늘 밝기를 읽는다.
    ///   큐브맵 면(face)↔방향 매핑 규약을 직접 세우면 부호를 틀리기 쉬우므로(같은 날 1회 실패),
    ///   <b>규약이 개입하지 않는 경로</b>를 택한 것이다. 카메라가 보는 방향은 정의상 명확하다.
    ///   조리개를 3단으로 좁혀 가며(20° → 6° → 4°) 굵게 찾고 → 좁혀서 다듬고 → 무게중심으로 마무리한다.
    /// </summary>
    public static class JcSkySunFinder
    {
        public struct Result
        {
            public bool ok;
            public float azimuth;    // 도(度). +Z = 0, 시계방향
            public float elevation;  // 도(度). 수평 = 0
            public float luminance;  // 피크 휘도(HDR) — 태양이 실제로 있는지 판정용
            public float contrast;   // 피크 / 주변 하늘 평균 — 1에 가까우면 태양이 없는 하늘
        }

        /// <summary>
        /// 하늘 재질을 <c>_Rotation = 0</c> 상태로 두고 태양 위치를 찾는다(측정 후 원상 복구).
        /// 반환 방위는 그대로 프로파일의 <c>skySunAzimuth</c> 에 넣으면 된다.
        /// </summary>
        public static Result Find(Material sky)
        {
            var r = new Result();
            if (sky == null) return r;

            // 측정 대상이 실제로 렌더되도록 잠시 현행 하늘로 세운다(끝나면 되돌린다).
            var prevSky = RenderSettings.skybox;
            bool hasRot = sky.HasProperty("_Rotation");
            float prevRot = hasRot ? sky.GetFloat("_Rotation") : 0f;

            GameObject go = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                RenderSettings.skybox = sky;
                if (hasRot) sky.SetFloat("_Rotation", 0f);

                go = new GameObject("__JcSkySunProbe") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.Skybox;
                cam.cullingMask = 0;            // 하늘만 — 씬 오브젝트가 섞이지 않게
                cam.enabled = false;            // 수동 Render 만
                cam.allowHDR = true;            // ★태양은 1.0 을 한참 넘는다. LDR 로 받으면 전부 포화되어 위치를 잃는다
                cam.allowMSAA = false;
                cam.nearClipPlane = 0.01f;
                cam.transform.position = Vector3.zero;

                const int N = 8;
                rt = new RenderTexture(N, N, 16, RenderTextureFormat.ARGBHalf);
                rt.Create();
                tex = new Texture2D(N, N, TextureFormat.RGBAFloat, false);
                cam.targetTexture = rt;

                float Sample(float az, float el)
                {
                    float ar = az * Mathf.Deg2Rad, er = el * Mathf.Deg2Rad;
                    cam.transform.forward = new Vector3(
                        Mathf.Sin(ar) * Mathf.Cos(er), Mathf.Sin(er), Mathf.Cos(ar) * Mathf.Cos(er));
                    cam.Render();

                    var prevActive = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, N, N), 0, 0);
                    tex.Apply();
                    RenderTexture.active = prevActive;

                    float sum = 0f;
                    var px = tex.GetPixels();
                    foreach (var c in px) sum += 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
                    return sum / px.Length;
                }

                // ① 굵은 탐색 — 하늘 전체를 훑어 태양이 있는 대략의 구역을 찾는다
                cam.fieldOfView = 20f;
                float bA = 0f, bE = 25f, bV = -1f, skySum = 0f; int skyN = 0;
                for (float a = 0f; a < 360f; a += 10f)
                    for (float e = -5f; e <= 75f; e += 15f)
                    {
                        float v = Sample(a, e);
                        skySum += v; skyN++;
                        if (v > bV) { bV = v; bA = a; bE = e; }
                    }
                float skyMean = skyN > 0 ? skySum / skyN : 0f;

                // ② 좁혀서 다듬기
                cam.fieldOfView = 6f;
                for (float a = bA - 10f; a <= bA + 10f; a += 2f)
                    for (float e = Mathf.Max(bE - 12f, -20f); e <= Mathf.Min(bE + 12f, 88f); e += 2f)
                    {
                        float v = Sample(a, e);
                        if (v > bV) { bV = v; bA = a; bE = e; }
                    }

                // ③ 무게중심 — 태양 원반이 격자 사이에 걸쳐도 중심을 놓치지 않게
                cam.fieldOfView = 4f;
                float wa = 0f, we = 0f, ws = 0f, peak = -1f;
                for (float a = bA - 4f; a <= bA + 4f; a += 1f)
                    for (float e = Mathf.Max(bE - 4f, -20f); e <= Mathf.Min(bE + 4f, 88f); e += 1f)
                    {
                        float v = Sample(a, e);
                        if (v > peak) peak = v;
                        if (v > bV * 0.5f) { wa += a * v; we += e * v; ws += v; }
                    }

                r.ok = ws > 0f;
                r.azimuth = r.ok ? Mathf.Repeat(wa / ws, 360f) : bA;
                r.elevation = r.ok ? we / ws : bE;
                r.luminance = peak;
                r.contrast = skyMean > 1e-6f ? peak / skyMean : 0f;
                return r;
            }
            finally
            {
                if (go != null) Object.DestroyImmediate(go);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
                if (hasRot) sky.SetFloat("_Rotation", prevRot);
                RenderSettings.skybox = prevSky;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
