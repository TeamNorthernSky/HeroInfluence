using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.Environment
{
    [DisallowMultipleComponent]
    public sealed class JcContactShadingController : MonoBehaviour
    {
        // JC | 2026-11-06 | 재질 생성·파괴는 기존 소유자가 담당하고 이 조절기는 등록된 복제 재질의 셰이더만 관리합니다.
        [SerializeField, HideInInspector] JcContactShadingProfile source;
        [SerializeField, HideInInspector] JcContactShadingProfile draft;
        [SerializeField, HideInInspector] Transform anchor;
        [SerializeField, HideInInspector] List<Binding> bindings = new List<Binding>();
        readonly Vector4[] starts = new Vector4[JcContactShadingProfile.MaxSegments];
        readonly Vector4[] ends = new Vector4[JcContactShadingProfile.MaxSegments];
        // JC | 2026-11-06 | 접점마다 실내 바닥 방향을 전달하여 벽 쪽의 잘못된 번짐을 차단합니다.
        readonly Vector4[] receivers = new Vector4[JcContactShadingProfile.MaxSegments];
        Matrix4x4 lastMatrix;
        public JcContactShadingProfile Source => source;
        public JcContactShadingProfile Draft => draft;
        public int AppliedMaterialCount { get; private set; }

        [Serializable]
        sealed class Binding
        {
            public Material preview;
            public Material original;
            public Shader originalShader;
            public bool applied;
        }

        public void Initialize(JcContactShadingProfile profile, Transform backgroundAnchor)
        {
            Restore();
            bindings.Clear();
            source = profile;
            anchor = backgroundAnchor;
            ReloadProfile();
        }

        public void ReloadProfile()
        {
            DisposeDraft();
            if (source)
            {
                draft = Instantiate(source);
                draft.name = source.name + " (접점 조절 중)";
                // JC | 2026-11-06 | NotEditable을 사용하지 않아 플레이 중에도 조절할 수 있습니다.
                draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
            }
            ApplyDraft();
        }

        public void RegisterMaterial(Material preview, Material original)
        {
            if (!preview || !original || preview == original)
                throw new ArgumentException("접점 음영에는 원본과 다른 복제 재질을 등록해야 합니다.");
            if (bindings.Exists(b => b.preview == preview)) return;
            bindings.Add(new Binding { preview = preview, original = original, originalShader = preview.shader });
        }

        public void ApplyDraft()
        {
            AppliedMaterialCount = 0;
            if (!draft || !anchor || !isActiveAndEnabled) { Restore(); return; }
            if (draft.segments != null && draft.segments.Length > JcContactShadingProfile.MaxSegments)
            {
                Restore();
                Debug.LogError("[JC 접점 음영] 접점 구간이 64개를 초과했습니다. 프로필을 정리해 주세요.", this);
                return;
            }
            int count = draft.segments?.Length ?? 0;
            for (int i = 0; i < count; i++) { starts[i] = draft.segments[i].start; ends[i] = draft.segments[i].end; receivers[i] = draft.segments[i].receiverDirection.normalized; }
            lastMatrix = anchor.worldToLocalMatrix;
            foreach (var binding in bindings)
            {
                if (!binding.preview) continue;
                bool target = draft.shadingEnabled && draft.targetMaterials != null && Array.IndexOf(draft.targetMaterials, binding.original) >= 0;
                if (!target) { Restore(binding); continue; }
                // JC | 2026-11-06 | 은행 소유자가 접점+툰 셰이더를 미리 지정해도 원본의 URP Lit 호환 여부로 검증합니다.
                if (!draft.contactShader || !binding.original || binding.original.shader.name != "Universal Render Pipeline/Lit")
                {
                    Restore(binding);
                    Debug.LogError("[JC 접점 음영] Contact Lit 셰이더와 URP Lit 원본 재질이 필요합니다: " + binding.original.name, this);
                    continue;
                }
                if (!binding.applied)
                {
                    // JC | 2026-11-06 | 기존 줄눈·노멀·발광 키워드를 보존하여 표면 조절기와 함께 사용합니다.
                    var keywords = binding.preview.shaderKeywords;
                    binding.preview.shader = draft.contactShader;
                    binding.preview.shaderKeywords = keywords;
                    binding.applied = true;
                }
                var material = binding.preview;
                material.SetMatrix("_JcContactWorldToLocal", lastMatrix);
                material.SetVectorArray("_JcContactStarts", starts);
                material.SetVectorArray("_JcContactEnds", ends);
                material.SetVectorArray("_JcContactReceivers", receivers);
                material.SetInt("_JcContactCount", count);
                material.SetColor("_JcContactTint", draft.tint);
                material.SetFloat("_JcContactStrength", Mathf.Clamp01(draft.strength));
                material.SetFloat("_JcContactWidth", Mathf.Clamp(draft.width, .005f, 1));
                material.SetFloat("_JcContactSoftness", Mathf.Clamp(draft.softness, .05f, 1));
                AppliedMaterialCount++;
            }
        }

        // JC | 2026-11-06 | 배경 루트가 이동해도 로컬 접점이 따라가며 카메라 움직임으로 위치가 변하지 않습니다.
        void LateUpdate() { if (anchor && anchor.worldToLocalMatrix != lastMatrix) ApplyDraft(); }
        void OnEnable() => ApplyDraft();
        void OnDisable() => Restore();
        void OnDestroy() { Restore(); DisposeDraft(); }
        // JC | 2026-11-06 | 에디트 카메라 미리보기에서도 재질 소유자가 해제되기 전에 접점 초안을 정리합니다.
        public void ReleaseEditorPreview() => OnDestroy();
        void Restore() { foreach (var binding in bindings) Restore(binding); AppliedMaterialCount = 0; }
        static void Restore(Binding binding)
        {
            if (binding.preview && binding.applied)
            {
                // JC | 2026-11-06 | 복원 대상이 접점+툰 공용 셰이더인 경우에도 접점만 확실히 끕니다.
                binding.preview.SetFloat("_JcContactStrength", 0);
                var keywords = binding.preview.shaderKeywords;
                binding.preview.shader = binding.originalShader;
                binding.preview.shaderKeywords = keywords;
            }
            binding.applied = false;
        }
        void DisposeDraft()
        {
            if (!draft) return;
            if (Application.isPlaying) Destroy(draft); else DestroyImmediate(draft);
            draft = null;
        }
    }
}
