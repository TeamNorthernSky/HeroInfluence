using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 하늘을 6방향으로 촬영하고 HDR 피크의 방향 무게중심을 측정합니다.
    /// 넓은 시야의 평균 밝기로 탐색하면 작은 태양보다 큰 구름을 선택하므로
    /// 전체 방향의 픽셀 밝기를 비교합니다. 카메라 ray로 방향을 얻어 큐브맵 규약에 의존하지 않습니다.
    /// </summary>
    public static class JcSkySunFinder
    {
        public struct Result
        {
            public bool ok;
            public float azimuth;
            public float elevation;
            public float luminance;
            public float contrast;
        }
        public static Result Find(Material sky)
        {
            var result = new Result();
            if (sky == null) return result;
            var prevSky = RenderSettings.skybox;
            bool hasRot = sky.HasProperty("_Rotation");
            float prevRot = hasRot ? sky.GetFloat("_Rotation") : 0;
            bool hasCloud = sky.HasProperty("_CloudOpacity");
            float prevCloud = hasCloud ? sky.GetFloat("_CloudOpacity") : 0;
            var prevActive = RenderTexture.active;
            GameObject go = null;
            RenderTexture rt = null;
            Texture2D tex = null;
            try
            {
                RenderSettings.skybox = sky;
                if (hasCloud) sky.SetFloat("_CloudOpacity", 0); // 구름에 가려져도 고정 태양 위치를 측정합니다.
                if (hasRot) sky.SetFloat("_Rotation", 0);
                go = new GameObject("__JcSkySunProbe") { hideFlags = HideFlags.HideAndDontSave };
                var cam = go.AddComponent<Camera>();
                cam.enabled = false; cam.clearFlags = CameraClearFlags.Skybox; cam.cullingMask = 0;
                cam.allowHDR = true; cam.allowMSAA = false; cam.nearClipPlane = .01f;
                cam.fieldOfView = 90; cam.aspect = 1;
                cam.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                const int size = 256;
                rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGBFloat);
                rt.Create();
                tex = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
                var samples = new List<Vector4>(size * size * 3);
                float peak = 0;
                double skySum = 0;
                foreach (var dir in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.left, Vector3.up, Vector3.down })
                {
                    cam.transform.rotation = Quaternion.LookRotation(dir, Mathf.Abs(dir.y) > .5f ? Vector3.forward : Vector3.up);
                    RenderPipeline.SubmitRenderRequest(cam, new UniversalRenderPipeline.SingleCameraRequest { destination = rt });
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, size, size), 0, 0); tex.Apply();
                    var pixels = tex.GetPixels();
                    for (int j = 0; j < pixels.Length; j++)
                    {
                        var ray = cam.ViewportPointToRay(new Vector3((j % size + .5f) / size, (j / size + .5f) / size, 0));
                        if (ray.direction.y < -.08716f) continue; // 지평선 아래 5도까지만 후보로 허용.
                        var c = pixels[j];
                        float lum = Mathf.Max(0, c.r * .2126f + c.g * .7152f + c.b * .0722f);
                        if (float.IsNaN(lum) || float.IsInfinity(lum)) continue;
                        peak = Mathf.Max(peak, lum); skySum += lum;
                        samples.Add(new Vector4(ray.direction.x, ray.direction.y, ray.direction.z, lum));
                    }
                }
                Vector3 center = Vector3.zero;
                float weight = 0;
                foreach (var sample in samples)
                {
                    if (sample.w < peak * .8f) continue;
                    center += new Vector3(sample.x, sample.y, sample.z) * sample.w;
                    weight += sample.w;
                }
                result.ok = weight > 1e-6f && center.sqrMagnitude > 1e-8f;
                if (result.ok)
                {
                    center.Normalize();
                    result.azimuth = Mathf.Repeat(Mathf.Atan2(center.x, center.z) * Mathf.Rad2Deg, 360);
                    result.elevation = Mathf.Asin(Mathf.Clamp(center.y, -1, 1)) * Mathf.Rad2Deg;
                }
                result.luminance = peak;
                result.contrast = skySum > 1e-6 ? (float)(peak * samples.Count / skySum) : 0;
                return result;
            }
            finally
            {
                RenderTexture.active = prevActive;
                if (go != null) Object.DestroyImmediate(go);
                if (rt != null) { rt.Release(); Object.DestroyImmediate(rt); }
                if (tex != null) Object.DestroyImmediate(tex);
                if (hasRot) sky.SetFloat("_Rotation", prevRot);
                if (hasCloud) sky.SetFloat("_CloudOpacity", prevCloud);
                RenderSettings.skybox = prevSky;
                DynamicGI.UpdateEnvironment();
            }
        }
    }
}
