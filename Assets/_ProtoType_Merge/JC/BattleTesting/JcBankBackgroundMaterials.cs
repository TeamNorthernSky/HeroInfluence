using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    [DisallowMultipleComponent]
    public sealed class JcBankBackgroundMaterials : MonoBehaviour
    {
        // JC | 2026-11-05 | 초안과 재질 복제본만 변경합니다. 비활성화/종료하면 원래 슬롯을 복원합니다.
        [SerializeField, HideInInspector] JcBankMaterialProfile source;
        [SerializeField, HideInInspector] JcBankMaterialProfile draft;
        [SerializeField, HideInInspector] List<Binding> bindings = new List<Binding>();
        public JcBankMaterialProfile Source => source;
        public JcBankMaterialProfile Draft => draft;
        public int MaterialCount => bindings.Count;

        [Serializable]
        sealed class Binding
        {
            public Renderer renderer;
            public int slot;
            public Material original;
            public Material preview;
            public Texture2D groutTexture;
            public float lastGroutContrast = -1;
        }

        // JC | 2026-11-06 | 접점 조절기는 이 클래스가 소유하는 미리보기 재질을 공유하고 원본 슬롯에는 접근하지 않습니다.
        public void Initialize(GameObject bankRoot, JcBankMaterialProfile profile, JC.Environment.JcContactShadingController contactShading = null, Shader surfaceShader = null)
        {
            Release();
            source = profile;
            if (!bankRoot || !source) return;
            draft = Instantiate(source);
            draft.name = source.name + " (조절 중)";
            // JC | 2026-11-05 | HideAndDontSave의 NotEditable이 조절 필드를 잠그므로 저장 제외·계층 숨김만 사용합니다.
            draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            foreach (var renderer in bankRoot.GetComponentsInChildren<Renderer>(true))
            {
                var materials = renderer.sharedMaterials;
                for (int slot = 0; slot < materials.Length; slot++)
                {
                    var original = materials[slot];
                    if (!original || FindSurface(original) == null) continue;
                    bindings.Add(new Binding { renderer = renderer, slot = slot, original = original,
                        preview = new Material(original) { name = original.name + " (은행 미리보기)", hideFlags = HideFlags.HideAndDontSave } });
                }
            }
            // JC | 2026-11-06 | 하나의 복제 재질 소유자가 접점+툰 호환 셰이더를 지정하여 두 조절기의 교체 충돌을 막습니다.
            if (surfaceShader) foreach (var binding in bindings)
            {
                if (binding.original.shader.name != "Universal Render Pipeline/Lit") continue;
                var keywords = binding.preview.shaderKeywords;
                binding.preview.shader = surfaceShader;
                binding.preview.shaderKeywords = keywords;
            }
            ApplyDraft();
            if (contactShading)
            {
                foreach (var binding in bindings) contactShading.RegisterMaterial(binding.preview, binding.original);
                contactShading.ApplyDraft();
            }
        }

        JcBankMaterialProfile.Surface FindSurface(Material material) =>
            draft ? draft.surfaces.Find(s => s != null && s.source == material) : null;

        public void ApplyDraft()
        {
            if (!draft || !isActiveAndEnabled) return;
            foreach (var binding in bindings)
            {
                if (!binding.renderer || !binding.preview) continue;
                var rule = FindSurface(binding.original);
                if (rule == null) continue;
                var material = binding.preview;
                material.SetColor("_BaseColor", binding.original.GetColor("_BaseColor") * rule.tint);
                material.SetFloat("_Metallic", Mathf.Clamp01(rule.metallic));
                material.SetFloat("_Smoothness", Mathf.Clamp01(rule.smoothness));
                material.SetFloat("_SpecularHighlights", rule.specularHighlights ? 1 : 0);
                material.SetFloat("_EnvironmentReflections", rule.environmentReflections ? 1 : 0);
                SetKeyword(material, "_SPECULARHIGHLIGHTS_OFF", !rule.specularHighlights);
                SetKeyword(material, "_ENVIRONMENTREFLECTIONS_OFF", !rule.environmentReflections);
                // JC | 2026-11-05 | 램프 내부만 자체 발광하며 원본 재질/공용 색상표에는 기록하지 않습니다.
                float emission = Mathf.Clamp(rule.emissionIntensity, 0, 8);
                material.SetColor("_EmissionColor", rule.emissionColor * emission);
                SetKeyword(material, "_EMISSION", emission > 0);
                // JC | 2026-11-05 | 노멀맵이 있는 은행 바닥에만 줄눈 설정을 적용합니다. 벽/문양의 팔레트 좌표는 유지합니다.
                if (binding.original.GetTexture("_BumpMap"))
                {
                    material.SetFloat("_BumpScale", Mathf.Clamp(rule.normalStrength, 0, 3));
                    material.SetTextureScale("_BaseMap", Vector2.one / Mathf.Clamp(rule.tileSize, 2, 16));
                    SetKeyword(material, "_NORMALMAP", rule.normalStrength > 0);
                    ApplyGrout(binding, rule.groutContrast);
                }
                var materials = binding.renderer.sharedMaterials;
                if (binding.slot >= materials.Length) continue;
                materials[binding.slot] = material;
                binding.renderer.sharedMaterials = materials;
            }
        }

        // JC | 2026-11-05 | 기존 URP Lit/노멀맵을 유지합니다. 명암 변경 시에만 작은 반복 텍스처를 갱신하며 원본 흰 텍스처는 보존합니다.
        static void ApplyGrout(Binding binding, float contrast)
        {
            contrast = Mathf.Clamp01(contrast);
            if (Mathf.Approximately(binding.lastGroutContrast, contrast)) return;
            binding.lastGroutContrast = contrast;
            if (contrast <= 0) { binding.preview.SetTexture("_BaseMap", binding.original.GetTexture("_BaseMap")); return; }
            const int size = 512;
            if (!binding.groutTexture) binding.groutTexture = new Texture2D(size, size, TextureFormat.RGBA32, true, false) {
                name = "은행 줄눈 명암 (조절 중)", hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            var curve = new float[size];
            for (int i = 0; i < size; i++) {
                float u = (i + .5f) / size; float q = Mathf.Min(u, 1-u) / .009f;
                curve[i] = Mathf.Exp(-q*q); // 기존 Bank_FloorGroutNormal의 주기·폭과 일치합니다.
            }
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) {
                byte value = (byte)Mathf.RoundToInt(255 * (1 - contrast * Mathf.Max(curve[x], curve[y])));
                pixels[y * size + x] = new Color32(value, value, value, 255);
            }
            binding.groutTexture.SetPixels32(pixels); binding.groutTexture.Apply(true, false);
            binding.preview.SetTexture("_BaseMap", binding.groutTexture);
        }

        static void SetKeyword(Material material, string keyword, bool enabled)
        { if (enabled) material.EnableKeyword(keyword); else material.DisableKeyword(keyword); }

        public void ReloadProfile()
        {
            if (!source || !draft) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), draft);
            ApplyDraft();
        }

        void OnEnable() => ApplyDraft();
        void OnDisable() => Restore();
        void OnDestroy() => Release();

        // JC | 2026-11-06 | 에디트 카메라 도구에서도 MonoBehaviour 수명 콜백에 의존하지 않고 임시 재질을 정리합니다.
        public void ReleaseEditorPreview() => Release();

        void Restore()
        {
            foreach (var binding in bindings)
            {
                if (!binding.renderer) continue;
                var materials = binding.renderer.sharedMaterials;
                // JC | 2026-11-05 | 다른 조절기가 나중에 교체한 슬롯은 덮어쓰지 않습니다.
                if (binding.slot >= materials.Length || materials[binding.slot] != binding.preview) continue;
                materials[binding.slot] = binding.original;
                binding.renderer.sharedMaterials = materials;
            }
        }

        void Release()
        {
            Restore();
            foreach (var binding in bindings) { Dispose(binding.preview); Dispose(binding.groutTexture); }
            bindings.Clear();
            Dispose(draft); draft = null;
        }

        static void Dispose(UnityEngine.Object value)
        { if (value) { if (Application.isPlaying) Destroy(value); else DestroyImmediate(value); } }
    }
}
