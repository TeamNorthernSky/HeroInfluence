using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace JC.BattleTesting
{
    // 원본 대응: DH/Scripts/Manager/Data/DHCsvTemplateCatalog.cs 수치 조회,
    // DH/Scripts/Manager/Persistence/UnitStatCalculator.cs 및 ASB/Scripts/Unit/CharactorScript.Initialize.
    // 공용 카탈로그를 읽기만 하며, 복제한 SkillData/WeaponData/영속 형식 객체를 테스트 인스턴스에만 주입합니다.
    public static class JcTestDataAssembler
    {
        public static SkillData Copy(SkillData data) => JsonUtility.FromJson<SkillData>(JsonUtility.ToJson(data));

        // 원본 대응: ClassSkillTooltipText/WeaponTooltipText. 공용 테이블 재조회 없이 실제 시전 계수를 표시합니다.
        public static string DescribeAppliedSkill(SkillData skill, bool weapon = false) => skill == null ? string.Empty :
            ClassSkillTooltipText.ReplaceBattleCoefficients(skill.description, weapon ? "WeaponSkill" : "ClassSkill",
                skill.skillValue, skill.skillSubValue);

        public sealed class PreparedAlly
        {
            public JcAllySetup Setup;
            public DHPlayerUnitTemplate Template;
            public List<SkillData> Skills;
            public WeaponData Weapon;
            public StatBlock Stats;
            public UnitPersistentData Detached;
        }
        // 적용 전에 순수 데이터로 검증합니다. 실패해도 기존 전투/유닛/설정을 변경하지 않습니다.
        public static PreparedAlly PrepareAlly(JcAllySetup setup, DHCsvTemplateCatalog catalog)
        {
            if (!catalog.TryGetPlayerUnitTemplate(setup.unitKey, out var template))
                throw new InvalidOperationException("히어로 테이블 키가 없습니다: " + setup.unitKey);
            var skills = catalog.GetCurrentClassSkills(template.ClassIndex, setup.level).Select(Copy).ToList();
            foreach (var skill in skills)
            {
                int slot = HeroSkillRules.FamilyId(skill.skillIndex) % 1000 / 10 - 1;
                int numericLevel = setup.skillLevels[slot];
                skill.enhancementLevel = numericLevel;
                skill.skillValue = catalog.GetClassSkillValueAtLevel(skill.skillIndex, numericLevel);
                skill.skillSubValue = catalog.GetClassSkillSubValueAtLevel(skill.skillIndex, numericLevel);
                skill.riskKey = string.Empty; // 원본 SkillData.RiskChance 대응: 폐기된 리스크는 테스트에서 활성화하지 않습니다.
            }
            string weaponKey = setup.weaponKey;
            if (string.IsNullOrWhiteSpace(weaponKey))
            {
                var weapons = catalog.GetWeaponsByClassIndex(template.ClassIndex);
                weaponKey = weapons.FirstOrDefault()?.weaponKey;
            }
            WeaponData weapon = null;
            StatBlock weaponBonus = default;
            if (!string.IsNullOrWhiteSpace(weaponKey))
            {
                if (!catalog.TryGetWeapon(weaponKey, out var shared) || !catalog.TryGetWeaponBonusAtLevel(weaponKey, setup.weaponLevel, out weaponBonus))
                    throw new InvalidOperationException("무기 키가 없습니다: " + weaponKey);
                weapon = JsonUtility.FromJson<WeaponData>(JsonUtility.ToJson(shared));
                weapon.WeaponSkillValue = catalog.GetWeaponSkillValueAtLevel(weaponKey, setup.weaponLevel);
                weapon.WeaponSkillSubValue = catalog.GetWeaponSkillSubValueAtLevel(weaponKey, setup.weaponLevel);
            }
            // 원본 대응: TrainingManager.AtkGainPerLevel/HpGainPerLevel. 구매 실행·시설 제한·영속 저장은 하지 않습니다.
            var training = UnityEngine.Object.FindFirstObjectByType<TrainingManager>();
            IReadOnlyList<float> atk = training != null ? training.AtkGainPerLevel : new float[] { 5, 10, 15 };
            IReadOnlyList<float> hp = training != null ? training.HpGainPerLevel : new float[] { 5, 10, 15 };
            StatBlock stats = UnitStatCalculator.CalculateLevelAdjustedBaseStats(template.BaseStats, template.LevelupStats, setup.level) + weaponBonus;
            for (int i = 0; i < setup.attackTraining; i++) stats.Atk += atk[i];
            for (int i = 0; i < setup.healthTraining; i++) stats.HP += hp[i];
            foreach (var row in catalog.GetUnitGrowthTemplates())
                if (row.Level <= setup.level) stats.Influence += row.AddInfluence;
            if (setup.initialIP > stats.Influence) throw new InvalidOperationException("초기 IP가 최대 IP를 초과합니다: " + template.UnitName);
            // 원본 대응: UnitPersistentData 생성 형식. Repository 등록을 하지 않아 본게임 저장과 분리됩니다.
            var detached = new UnitPersistentData(900000 + setup.slot, setup.unitKey, setup.level,
                template.BaseStats, template.LevelupStats, skills.First().skillIndex,
                weapon != null ? weapon.WeaponIndex : 0, default, stats, stats.HP * setup.hpRatio,
                currentInfluence: setup.initialIP);
            return new PreparedAlly { Setup = setup, Template = template, Skills = skills, Weapon = weapon, Stats = stats, Detached = detached };
        }

        public static void ApplyAlly(BattleCharactor actor, PreparedAlly prepared)
        {
            var setup = prepared.Setup;
            var template = prepared.Template;
            var skills = prepared.Skills;
            var weapon = prepared.Weapon;
            var stats = prepared.Stats;
            actor.IsPlayer = true;
            actor.BindPersistentSourceData(prepared.Detached);
            actor.SetTemplateIndex(setup.unitKey);
            actor.SetUnitNameForSkillMatching(template.ClassName);
            actor.SetDisplayName(template.UnitName);
            actor.ApplyCombatTuning(setup.level, default, default);
            actor.SetBaseStats(stats);
            actor.SetLevelScaling(false);
            actor.availableSkills = skills;
            actor.availableWeapons = weapon == null ? new List<WeaponData>() : new List<WeaponData> { weapon };
            actor.ResolveEquippedWeapon(false);
            actor.SetClassSkillIndex(skills.First().skillIndex);
            actor.ResolveSelectedSkill(false);
            actor.RecalculateStats();
            actor.InitializeCurrentState(stats.HP * setup.hpRatio, setup.initialIP);
            actor.MarkInitializedFromDataPipeline(true);
        }

        // 원본 대응: EnemySpawnPlanBuilder의 이벤트 테이블 → EnemyData/SkillData 변환.
        // 차이: 최신 2구역 SO를 명시적으로 읽고, UnitAI 설명문 대신 기존 실행 가능한 400xx AI 번호를 지정합니다.
        public static EnemySpawnEntry BuildEnemy(EnemyUnit1SectorDataTable table, int id, int level, int slot)
        {
            var row = table.DataList.Find(x => x.EnemyIndex == "FV" + id);
            if (row == null) throw new InvalidOperationException("적 테이블 키가 없습니다: FV" + id);
            var data = new EnemyData {
                Index = id.ToString(), Name = row.EnemyName, UnitAI = id.ToString(), IsEnemyRow = true,
                baseStats = new StatBlock(row.UnitMaxHP + row.LevelGrowthMaxHP * (level - 1),
                    row.UnitATK + row.LevelGrowthAtk * (level - 1), row.UnitDEF + row.LevelGrowthDef * (level - 1),
                    0, row.Speed, row.CriticalRate, 1.5f, row.CounterRate, row.ReduceRate),
                ExperiencePoint = 0
            };
            var skills = new List<SkillData>();
            for (int n = 1; n <= 3; n++)
            {
                string prefix = "EnemySkill" + n;
                string name = (string)typeof(EnemyUnit1SectorData).GetField(prefix + "_Name").GetValue(row);
                if (string.IsNullOrWhiteSpace(name)) continue;
                object Get(string suffix) => typeof(EnemyUnit1SectorData).GetField(prefix + suffix).GetValue(row);
                skills.Add(new SkillData {
                    skillIndex = id * 10 + n, skillKey = "FV" + id + "_" + n, slot = n,
                    category = SkillCategory.Enemy, acquireLevel = 1, skillName = name,
                    description = (string)Get("_Description"), classSkillEffect = (int)Get("Effect"),
                    classSkillRange = (int)Get("Range"), classSkillRangeLine = (int)Get("RangeLine"),
                    classSkillTarget = (int)Get("Target"), boundary = new List<int>((List<int>)Get("MultiTarget") ?? new List<int>()),
                    multiTargetType = (int)Get("_MultiTargetType"), multiTargetCount = (int)Get("_MultiTargetCount"),
                    skillValue = (float)Get("Value"), skillSubValue = (float)Get("SubValue"), AnimationTrigger = "Attack"
                });
            }
            return new EnemySpawnEntry(data, slot, id.ToString(), skills);
        }
    }
}
