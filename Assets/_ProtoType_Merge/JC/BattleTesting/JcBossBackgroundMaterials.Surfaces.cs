using System.Collections.Generic;
using UnityEngine;

namespace JC.BattleTesting
{
    public sealed partial class JcBossBackgroundMaterials
    {
        // JC | 2026-11-05 | 기존 등록부를 바꾸지 않고 명시된 실내 재질 슬롯의 실행 중 복제본만 보정합니다.
        public JcBossSurfaceProfile SurfaceSource { get; private set; }
        public JcBossSurfaceProfile SurfaceDraft { get; private set; }
        public JcBossEnvironment Environment { get; set; }
        // JC | 2026-11-09 | 원경을 제외한 실내 복제 재질만 공통 접점 음영에 등록합니다.
        public JC.Environment.JcContactShadingController ContactShading { get; set; }
        readonly Dictionary<Renderer, Material[]> surfaceOriginals = new Dictionary<Renderer, Material[]>();
        readonly List<SurfaceBinding> surfaceBindings = new List<SurfaceBinding>();
        sealed class SurfaceBinding { public Material copy; public JcBossSurfaceProfile.Surface settings; }

        void InitializeSurfaces(JcBossSurfaceProfile profile)
        {
            if (!profile) return;
            SurfaceSource = profile; SurfaceDraft = Instantiate(profile);
            SurfaceDraft.hideFlags = HideFlags.DontSave; SurfaceDraft.name = profile.name + " (조절 중)";
            ApplySurfaces();
        }

        public void ApplySurfaces()
        {
            // JC | 2026-11-05 | 사용자 지정 재질은 기본 표면보다 우선하되, 파괴된 이전 복제본을 복원하지 않게 순서를 맞춥니다.
            if (Environment) Environment.SuspendMaterialOverrides();
            RestoreSurfaces();
            // JC | 2026-11-17 | 전투 종료 때 무대가 먼저 파괴되어도 기존 복제본은 정리하고, 사라진 Transform은 조회하지 않습니다.
            if (!stageRoot || !SurfaceDraft || !SurfaceDraft.surfaceShader) return;
            // JC | 2026-11-17 | 미저장 초안에서만 초기 색을 채우며 기존 프로필 에셋을 자동 저장하지 않습니다.
            foreach (var item in SurfaceDraft.surfaces) if (item != null) item.EnsureColor();
            foreach (var renderer in stageRoot.GetComponentsInChildren<Renderer>(true))
            {
                // JC | 2026-11-18 | 유리는 전용 시간대 초안만 적용하며 공통 색상/표면 규칙에서는 제외합니다.
                if (IsGlass(renderer)) continue;
                var originals = renderer.sharedMaterials;
                var result = (Material[])originals.Clone(); bool changed = false;
                for (int slot = 0; slot < originals.Length; slot++)
                {
                    JcBossSurfaceProfile.Surface rule = null;
                    foreach (var item in SurfaceDraft.surfaces)
                        // JC | 2026-11-09 | 승인된 내보내기 대응표로 원본 설정을 찾아 석재·책상·카펫·줄눈 연결을 복구합니다.
                        if (item != null && item.source && item.source == SurfaceDraft.ResolveSource(originals[slot]) &&
                            item.Matches(renderer, stageRoot)) rule = item; // JC | 2026-11-17 | 명시된 연장 바닥도 같은 규칙에 연결합니다.
                    // JC | 2026-11-17 | 창틀 전용 원본은 유지하고 실내 조명용 복제만 만듭니다. 표면 색/줄눈 규칙은 적용하지 않습니다.
                    if (JcBossSurfaceProfile.IsWindowFrame(renderer, stageRoot)) rule = null;
                    // JC | 2026-11-05 | 금속 창틀/몰딩도 실내에 포함합니다. 조절 규칙이 없는 재질은 기존 표면값을 유지합니다.
                    bool isolated = Environment && Environment.Isolated && Environment.IsInterior(renderer) && originals[slot] &&
                        (originals[slot].shader.name == "Universal Render Pipeline/Lit" || originals[slot].shader == SurfaceDraft.surfaceShader);
                    if (rule == null && !isolated) continue;
                    var copy = new Material(originals[slot]) { shader = SurfaceDraft.surfaceShader, hideFlags = HideFlags.DontSave, name = originals[slot].name + " (보스 표면)" };
                    copy.shaderKeywords = new string[0];
                    copy.SetFloat("_BossLocalOnly", isolated ? 1 : 0);
                    // JC | 2026-11-12 | 도시에도 같은 원본 재질이 있을 수 있으므로 실내 렌더러만 IBL 대상으로 지정합니다.
                    if(Environment && Environment.IsInterior(renderer)) copy.EnableKeyword("_JC_INDOOR_IBL_TARGET");
                    // JC | 2026-11-17 | 기본색은 각 슬롯의 기존 색을 보존하며 색 전용 항목은 기존 광택을 유지합니다.
                    if (rule != null && rule.colorInitialized && rule.color != rule.defaultColor) copy.SetColor("_BaseColor", rule.color);
                    if (rule != null && !rule.colorOnly)
                    {
                        copy.SetFloat("_Smoothness", rule.smoothness); copy.SetColor("_SpecColor", new Color(rule.specular,rule.specular,rule.specular,1));
                        copy.SetFloat("_SurfaceVariation", rule.variation); copy.SetFloat("_FloorTiles", rule.floorTiles ? 1 : 0);
                    }
                    else
                    {
                        float metal = originals[slot].HasProperty("_Metallic") ? originals[slot].GetFloat("_Metallic") : 0;
                        copy.SetColor("_SpecColor", Color.Lerp(new Color(.04f,.04f,.04f), copy.GetColor("_BaseColor"), metal));
                        copy.SetFloat("_SurfaceVariation", 0); copy.SetFloat("_FloorTiles", 0);
                    }
                    // JC | 2026-11-18 | 공통 표면 프로필의 타일 오프셋을 모든 시간대 줄눈에 전달합니다.
                    copy.SetVector("_TileOffset", new Vector4(SurfaceDraft.tileOffset.x, SurfaceDraft.tileOffset.y, 0, 0));
                    copy.SetFloat("_TileSize", SurfaceDraft.tileSize); copy.SetFloat("_JointWidth", SurfaceDraft.jointWidth);
                    copy.SetFloat("_JointContrast", SurfaceDraft.jointContrast); copy.SetFloat("_JointNormal", SurfaceDraft.jointNormal);
                    copy.SetFloat("_TileVariation", SurfaceDraft.tileVariation);
                    copy.SetMatrix("_WorldToStage", stageRoot.worldToLocalMatrix);
                    var scale = stageRoot.lossyScale; copy.SetVector("_StageScale", new Vector4(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z),1));
                    if (ContactShading && Environment && Environment.IsInterior(renderer)) ContactShading.RegisterMaterial(copy, SurfaceDraft.ResolveSource(originals[slot]), true);
                    result[slot] = copy; surfaceBindings.Add(new SurfaceBinding { copy = copy, settings = rule }); changed = true;
                }
                if (changed) { surfaceOriginals.Add(renderer, originals); renderer.sharedMaterials = result; }
            }
            if (ContactShading) ContactShading.ApplyDraft();
            if (Environment) Environment.ResumeMaterialOverrides();
        }

        // JC | 2026-11-18 | 공통 표면은 시간대별 선택 없이 하나의 원본에서만 다시 불러옵니다.
        public void ReloadSurfaces()
        {
            if (!SurfaceSource || !SurfaceDraft) return;
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(SurfaceSource), SurfaceDraft); ApplySurfaces();
        }

        void RestoreSurfaces()
        {
            foreach (var entry in surfaceOriginals) if (entry.Key) entry.Key.sharedMaterials = entry.Value;
            surfaceOriginals.Clear();
            foreach (var entry in surfaceBindings)
            {
                // JC | 2026-11-09 | Destroy가 다음 프레임까지 지연되어도 이전 표면을 음영 대상으로 중복 계산하지 않습니다.
                if(ContactShading) ContactShading.UnregisterMaterial(entry.copy);
                Release(entry.copy);
            }
            surfaceBindings.Clear();
        }
    }
}
