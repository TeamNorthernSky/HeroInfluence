using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class HeroEffectUsage
{
    public sealed class Binding
    {
        public string Role;
        public GameObject Prefab;
    }

    public static readonly string[] Heroes = { "Justice", "Lumina", "BlackBullet", "Nekoming" };
    public static readonly string[] HeroLabels = { "저스티스", "루미나", "블랙불릿", "네코밍" };
    public static readonly string[][] Skills = {
        new[] { "EnterTheJustice", "Punch", "Dash", "Crash" },
        new[] { "FlareBomb", "ChainLightning", "SolarPrism", "PrismExplosion" },
        new[] { "AimShot", "BreakBullet", "TwinTrigger", "DeadEnd" },
        new[] { "Heal", "PawForYou", "LetsFightingLove", "Taosenaiyo" }
    };
    public static readonly string[][] SkillLabels = {
        new[] { "저스티스 등장!", "저스티스 펀치", "저스티스 대쉬", "저스티스 크래시" },
        new[] { "플레어 봄", "체인 라이트닝", "솔라 프리즘", "프리즘 익스플로전" },
        new[] { "블랙 데빌 퍼펙트 에임 샷", "프론트 라인 브레이크 불릿", "트윈 트리거 더블 버스트", "데드엔드 라스트 트리거" },
        new[] { "아픈 거 다 날아가라", "널 위해 준비했어", "전체 회복", "쓰러지면 안돼" }
    };

    private static GameObject[] currentPrefabs;
    static HeroEffectUsage() => EditorApplication.projectChanged += Invalidate;
    public static void Invalidate() => currentPrefabs = null;

    public static List<Binding> GetBindings(int skillIndex)
    {
        var result = new List<Binding>();
        var catalog = AssetDatabase.LoadAssetAtPath<SkillPresentationCatalog>(
            "Assets/RenderFX/_Seam/Data/Merged/JC_Merged_SkillPresentationCatalog.asset");
        var registry = AssetDatabase.LoadAssetAtPath<EffectRegistry>(
            "Assets/RenderFX/_Seam/Data/Merged/JC_Merged_EffectRegistry.asset");
        var data = catalog != null ? catalog.Get(skillIndex) : null;
        if (data == null) return result;
        void Add(GameObject prefab, string role)
        {
            if (prefab == null) return;
            if (string.IsNullOrEmpty(role)) role = "effect";
            var existing = result.FirstOrDefault(item => item.Prefab == prefab);
            if (existing != null)
            {
                if (!existing.Role.Split('/').Contains(role)) existing.Role += "/" + role;
                return;
            }
            result.Add(new Binding { Prefab = prefab, Role = role });
        }
        var cues = new List<CueBinding>();
        data.CollectAllCues(cues);
        foreach (var cue in cues)
            foreach (int id in cue.EffectIds) Add(registry != null ? registry.Get(id) : null, cue.CueName);
        Add(data.ProjectileVisual?.Prefab, "projectile");
        Add(data.ChainLightningEffectPrefab, "chain_lightning");
        if (data.EnableHitEffect) Add(registry != null ? registry.Get(data.HitEffectId) : null, "hit");
        var property = new SerializedObject(data).GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.Integer && property.name.EndsWith("EffectId") && property.intValue != 0)
                Add(registry != null ? registry.Get(property.intValue) : null, property.name);
        Add(Resources.Load<GameObject>("BattleEffects/FX_Common_Hit_Basic"), "common_hit");
        if (skillIndex / 1000 == 1)
            Add(Resources.Load<GameObject>(skillIndex % 10 == 1 ? "BattleEffects/FX_Justice_Hit_Enhanced" : "BattleEffects/FX_Justice_Hit_Basic"), "justice_hit");
        return result;
    }

    public static GameObject[] CurrentPrefabs()
    {
        if (currentPrefabs != null) return currentPrefabs;
        var roots = new HashSet<string>();
        for (int hero = 1; hero <= 4; hero++)
            for (int slot = 1; slot <= 4; slot++)
                for (int alt = 0; alt <= 1; alt++)
                    foreach (var binding in GetBindings(hero * 1000 + slot * 10 + alt))
                        roots.Add(AssetDatabase.GetAssetPath(binding.Prefab));
        currentPrefabs = AssetDatabase.GetDependencies(roots.ToArray(), true)
            .Where(path => path.EndsWith(".prefab"))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(prefab => prefab != null).ToArray();
        return currentPrefabs;
    }

    public static string Caption(string role)
    {
        switch (role)
        {
            case "cast": case "charge": case "heal_cast": return "차징·시전";
            case "projectile": return "투사체";
            case "hit": case "HitEffectId": return "피격";
            case "fire": return "공격";
            case "revive": return "부활";
            case "tao_circle": return "시작 마법진";
            case "parts_merge": return "합체";
            case "charge_r": return "오른손 차징";
            case "charge_l": return "왼손 차징";
            case "chain_lightning": return "번개";
            case "common_hit": return "공통 피격";
            case "justice_hit": return "저스티스 추가 피격";
            case "ApproachEffectId": case "fist_trail_on": case "parts_crash_on": return "이동 궤적";
            case "return_dust_on": return "복귀 먼지";
            case "dust_storm": return "돌풍";
            case "crash_charge_on": return "차징 빛";
            case "impact": return "타격";
            case "fire_left": return "최종 타격";
            case "dead_end_aim": return "조준·발사";
            case "hit_fx": return "공격·피격";
            case "effect": return "이펙트";
            default: return role.Replace('_', ' ');
        }
    }
}
