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
            if (!SurfaceDraft || !SurfaceDraft.surfaceShader) return;
            foreach (var renderer in stageRoot.GetComponentsInChildren<Renderer>(true))
            {
                var originals = renderer.sharedMaterials;
                var result = (Material[])originals.Clone(); bool changed = false;
                for (int slot = 0; slot < originals.Length; slot++)
                {
                    JcBossSurfaceProfile.Surface rule = null;
                    foreach (var item in SurfaceDraft.surfaces)
                        // JC | 2026-11-09 | 승인된 내보내기 대응표로 원본 설정을 찾아 석재·책상·카펫·줄눈 연결을 복구합니다.
                        if (item != null && item.source && item.source == SurfaceDraft.ResolveSource(originals[slot]) &&
                            (string.IsNullOrEmpty(item.rendererPath) || stageRoot.Find(item.rendererPath) == renderer.transform)) rule = item;
                    // JC | 2026-11-05 | 금속 창틀/몰딩도 실내에 포함합니다. 조절 규칙이 없는 재질은 기존 표면값을 유지합니다.
                    bool isolated = Environment && Environment.Isolated && Environment.IsInterior(renderer) && originals[slot] &&
                        (originals[slot].shader.name == "Universal Render Pipeline/Lit" || originals[slot].shader == SurfaceDraft.surfaceShader);
                    if (rule == null && !isolated) continue;
                    var copy = new Material(originals[slot]) { shader = SurfaceDraft.surfaceShader, hideFlags = HideFlags.DontSave, name = originals[slot].name + " (보스 표면)" };
                    copy.shaderKeywords = new string[0];
                    copy.SetFloat("_BossLocalOnly", isolated ? 1 : 0);
                    if (rule != null)
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
