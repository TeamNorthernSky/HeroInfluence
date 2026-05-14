using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

/// <summary>적 CSV 한 번 파싱 결과: 본체 행과 추출된 적 스킬(SkillData) 목록.</summary>
public sealed class EnemyCsvLoadResult
{
    public List<EnemyData> Enemies = new List<EnemyData>();
    public List<SkillData> Skills = new List<SkillData>();
}

public class CSVDataLoad : MonoBehaviour
{
    [Tooltip("레거시: playerUnitCsv가 비어 있을 때 플레이어 유닛 CSV로 사용")]
    [SerializeField] private TextAsset unitCsv;

    [SerializeField] private TextAsset playerUnitCsv;
    [SerializeField] private TextAsset enemyUnitCsv;
    [SerializeField] private TextAsset classSkillSheetCsv;
    [SerializeField] private TextAsset weaponSheetCsv;
    [SerializeField] private TextAsset levelUpSheetCsv;

    /// <summary>레거시 호환: 플레이어 유닛 목록과 동일.</summary>
    public List<UnitData> LoadUnits()
    {
        return LoadPlayerUnits();
    }

    public List<UnitData> LoadPlayerUnits()
    {
        TextAsset ta = playerUnitCsv != null ? playerUnitCsv : unitCsv;
        if (ta == null)
        {
            Debug.LogError("[CSVDataLoad] playerUnitCsv 또는 unitCsv가 할당되지 않았습니다.");
            return new List<UnitData>();
        }

        var all = CSVLoader.LoadUnitData(ta.text);

        if (enemyUnitCsv != null)
        {
            Debug.Log($"[CSVDataLoad] 플레이어 전용 CSV에서 유닛 {all.Count}행 로드.");
            return all;
        }

        var players = new List<UnitData>();
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null) continue;
            if (!CSVLoader.IsEnemyUnitRow(u))
            {
                players.Add(u);
            }
        }

        Debug.Log($"[CSVDataLoad] 단일 CSV에서 플레이어 유닛 {players.Count}행 (적 행 제외).");
        return players;
    }

    /// <summary>적 전용 CSV 또는 단일 유닛 CSV에서 적 본체 + 적 스킬 목록을 함께 로드합니다.</summary>
    public EnemyCsvLoadResult LoadEnemyCsv()
    {
        if (enemyUnitCsv != null)
        {
            var result = CSVLoader.LoadEnemyDataAndSkills(enemyUnitCsv.text);
            Debug.Log($"[CSVDataLoad] 적 전용 CSV: 적 {result.Enemies.Count}행, 추출 스킬 {result.Skills.Count}건.");
            return result;
        }

        TextAsset ta = playerUnitCsv != null ? playerUnitCsv : unitCsv;
        if (ta == null)
        {
            Debug.LogWarning("[CSVDataLoad] 적 CSV가 없고 플레이어 단일 CSV도 없어 적 데이터가 비어 있습니다.");
            return new EnemyCsvLoadResult();
        }

        var all = CSVLoader.LoadUnitData(ta.text);
        var enemies = new List<EnemyData>();
        for (int i = 0; i < all.Count; i++)
        {
            var u = all[i];
            if (u == null)
            {
                continue;
            }

            if (CSVLoader.IsEnemyUnitRow(u))
            {
                enemies.Add(CSVLoader.UnitDataToEnemyData(u, string.Empty));
            }
        }

        Debug.Log($"[CSVDataLoad] 단일 CSV에서 적 유닛 {enemies.Count}행 (IsEnemy / ID 20000~29999). 적 스킬은 EnemyDataSheet 형식에서만 추출됩니다.");
        return new EnemyCsvLoadResult { Enemies = enemies, Skills = new List<SkillData>() };
    }

    public List<EnemyData> LoadEnemyUnits()
    {
        return LoadEnemyCsv().Enemies;
    }

    /// <summary>ClassSkillSheet CSV에서 직업 스킬 행을 로드합니다.</summary>
    public List<SkillData> LoadClassSkills()
    {
        if (classSkillSheetCsv == null)
        {
            Debug.LogWarning("[CSVDataLoad] classSkillSheetCsv가 할당되지 않았습니다.");
            return new List<SkillData>();
        }

        return CSVLoader.LoadClassSkillData(classSkillSheetCsv.text);
    }

    /// <summary>WeaponSheet CSV에서 무기 행을 로드합니다.</summary>
    public List<WeaponData> LoadWeapons()
    {
        if (weaponSheetCsv == null)
        {
            Debug.LogWarning("[CSVDataLoad] weaponSheetCsv가 할당되지 않았습니다.");
            return new List<WeaponData>();
        }

        return CSVLoader.LoadWeaponData(weaponSheetCsv.text);
    }

    public List<LevelUpData> LoadLevelUpData()
    {
        if (levelUpSheetCsv == null)
        {
            Debug.LogWarning("[CSVDataLoad] levelUpSheetCsv is not assigned.");
            return new List<LevelUpData>();
        }

        return CSVLoader.LoadLevelUpData(levelUpSheetCsv.text);
    }
}

public static class CSVLoader
{
    // CSV: 헤더 1줄 + 설명 16줄(총 17줄) 스킵, 18번째 줄부터 데이터 파싱
    private const int DataStartLinePlayerIndex0Based = 4;
    private const int DataStartLineEnemyIndex0Based = 12;
    /// <summary>ClassSkillSheet: 헤더·설명 14줄 스킵 후 데이터(15번째 줄부터).</summary>
    private const int DataStartLineClassSkillIndex0Based = 14;

    /// <summary>
    /// WeaponSheet: 헤더(3줄) + 설명(10줄) 스킵 후 데이터(14번째 줄부터, 0-based index 13).
    /// 시트 레이아웃이 바뀌면 이 상수를 조정하세요.
    /// </summary>
    private const int DataStartLineWeaponIndex0Based = 13;

    /// <summary>EnemyDataSheet: 헤더·설명 43줄 스킵 후 데이터(44번째 줄부터, 0-based index 43).</summary>
    private const int DataStartLineEnemySheetIndex0Based = 42;

    /// <summary>Index가 20000~29999이면 적으로 간주 (플레이어 10000~19999와 분리).</summary>
    public static bool IsEnemyUnitIndex(string index)
    {
        if (string.IsNullOrWhiteSpace(index)) return false;
        if (!int.TryParse(index.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id))
        {
            return false;
        }

        return id >= 20000 && id < 30000;
    }

    /// <summary>CSV IsEnemy 열 또는 Index 규칙으로 적 행 여부.</summary>
    public static bool IsEnemyUnitRow(UnitData u)
    {
        if (u == null) return false;
        if (u.IsEnemyRow) return true;
        return IsEnemyUnitIndex(u.Index);
    }

    public static EnemyData UnitDataToEnemyData(UnitData u, string aiType)
    {
        if (u == null) return null;
        return new EnemyData
        {
            Index = u.Index,
            UnitType = u.UnitType,
            Name = u.Name,
            baseStats = u.baseStats,
            IsEnemyRow = u.IsEnemyRow,
            //aiType = aiType ?? string.Empty
        };
    }

    public static List<UnitData> LoadUnitData(string csvText)
    {
        var result = new List<UnitData>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return result;
        }

        csvText = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = csvText.Split('\n');
        if (lines.Length <= DataStartLinePlayerIndex0Based)
        {
            return result;
        }






        // 1. 파일 전체 텍스트(엔터 포함)를 한 번에 파싱합니다.
        // csvText는 Resources.Load 등으로 읽어온 파일 전체 string입니다.
        List<List<string>> allParsedData = ParseCsvTotal(csvText);

        // 2. 파싱이 안전하게 끝난 2차원 리스트에서 0번 줄과 2번 줄을 가져옵니다.
        // (이때는 이미 따옴표나 줄바꿈 처리가 완벽하게 끝난 상태입니다)
        var headerFields = allParsedData[0];
        var headerFields1 = allParsedData[2];

        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        // 1. 두 리스트를 순서대로 이어붙여 하나의 리스트로 만듭니다.
        var combinedHeaders = new List<string>();
        combinedHeaders.AddRange(headerFields);
        combinedHeaders.AddRange(headerFields1);

        // 2. 합쳐진 전체 리스트를 기준으로 0번부터 끝까지 인덱스를 매깁니다.
        for (int i = 0; i < combinedHeaders.Count; i++)
        {
            var key = NormalizeHeader(combinedHeaders[i]);

            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!headerIndex.ContainsKey(key))
            {
                headerIndex.Add(key, i);
            }
        }

        int TryGetCol(string headerName)
        {
            return headerIndex.TryGetValue(headerName, out var col) ? col : -1;
        }



        int hpCol = TryGetCol("UnitMaxHP");
        int atkCol = TryGetCol("UnitATK");
        int defCol = TryGetCol("UnitDEF");
        if (defCol < 0) defCol = TryGetCol("Def");

        int luckCol = TryGetCol("Luck");

        int speedCol = TryGetCol("Speed");
        int criticalRateCol = TryGetCol("CriticalRate");
        int counterRateCol = TryGetCol("CounterRate");
        int avoidRateCol = TryGetCol("AvoidRate");

        int indexCol = TryGetCol("Index");
        int unitTypeCol = TryGetCol("UnitType");
        int nameCol = TryGetCol("Name");

        int levelGrowthMaxHPCol = TryGetCol("LevelGrowthMaxHP");
        int levelGrowthMaxAtkCol = TryGetCol("LevelGrowthMaxAtk");
        int levelGrowthMaxDefCol = TryGetCol("LevelGrowthMaxDef");

        if (nameCol < 0) nameCol = TryGetCol("UnitName");
        if (nameCol < 0) nameCol = TryGetCol("DisplayName");

        int isEnemyCol = TryGetCol("IsEnemy");

        for (int lineIndex = DataStartLinePlayerIndex0Based; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 2)
            {
                continue;
            }

            string index = GetField(fields, indexCol >= 0 ? indexCol : 0);
             if (string.IsNullOrWhiteSpace(index))
            {
                continue;
            }

            string unitType = GetField(fields, unitTypeCol >= 0 ? unitTypeCol : 1);
            string name = nameCol >= 0 ? GetField(fields, nameCol) : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = unitType;
            }

            bool isEnemyRow;
            if (isEnemyCol >= 0)
            {
                var raw = GetField(fields, isEnemyCol);
                isEnemyRow = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                isEnemyRow = IsEnemyUnitIndex(index);
            }

            int hp = ParseIntOrDefault(GetField(fields, hpCol), 1);
            int atk = ParseIntOrDefault(GetField(fields, atkCol), 0);
            int def = ParseIntOrDefault(GetField(fields, defCol), 0);
            int luck = ParseIntOrDefault(GetField(fields, luckCol), 0);

            float speed = ParseFloatPercentOrFloat(GetField(fields, speedCol), 0f);
            float criticalRate = ParseFloatPercentOrFloat(GetField(fields, criticalRateCol), 0f);
            float counterRate = ParseFloatPercentOrFloat(GetField(fields, counterRateCol), 0f);
            float avoidRate = ParseFloatPercentOrFloat(GetField(fields, avoidRateCol), 0f);

            float levelGrowthMaxHP = ParseFloatSafe(GetField(fields, levelGrowthMaxHPCol), 0f);
            float levelGrowthMaxAtk = ParseFloatSafe(GetField(fields, levelGrowthMaxAtkCol), 0f);
            float levelGrowthMaxDef = ParseFloatSafe(GetField(fields, levelGrowthMaxDefCol), 0f);


            var unit = new UnitData
            {
                Index = index,
                UnitType = unitType,
                Name = name,
                baseStats = new StatBlock(hp, atk, def, luck, speed, criticalRate,1.5f, counterRate, avoidRate, 100.0f),
                levelupStats = new StatBlock(levelGrowthMaxHP, levelGrowthMaxAtk, levelGrowthMaxDef, 0, 0f, 0f, 0f, 0f, 0f),
                IsEnemyRow = isEnemyRow
            };

            result.Add(unit);
        }

        return result;
    }

    /// <summary>적 CSV를 파싱해 본체 행과 적 전용 SkillData 목록을 반환합니다. EnemyDataSheet 형식이면 스킬을 추출합니다.</summary>
    public static EnemyCsvLoadResult LoadEnemyDataAndSkills(string csvText)
    {
        var empty = new EnemyCsvLoadResult();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return empty;
        }

        csvText = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = csvText.Split('\n');
        if (lines.Length == 0)
        {
            return empty;
        }

        string head = lines[0];
        if (head.IndexOf("EnemyIndex", StringComparison.OrdinalIgnoreCase) >= 0 &&
            head.IndexOf("EnemyName", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return LoadEnemyDataSheetWithSkills(lines);
        }

        return new EnemyCsvLoadResult
        {
            Enemies = LoadEnemyDataLegacy(csvText),
            Skills = new List<SkillData>()
        };
    }

    /// <summary>레거시 Index/UnitType/HP 형식 적 CSV.</summary>
    private static List<EnemyData> LoadEnemyDataLegacy(string csvText)
    {
        var result = new List<EnemyData>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return result;
        }

        csvText = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = csvText.Split('\n');
        if (lines.Length <= DataStartLineEnemyIndex0Based)
        {
            return result;
        }

        var headerFields = ParseCsvLine(lines[0]);
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < headerFields.Count; i++)
        {
            var key = NormalizeHeader(headerFields[i]);
            if (string.IsNullOrEmpty(key))
            {
                continue;
            }

            if (!headerIndex.ContainsKey(key))
            {
                headerIndex.Add(key, i);
            }
        }

        int TryGetCol(string headerName)
        {
            return headerIndex.TryGetValue(headerName, out var col) ? col : -1;
        }

        int hpCol = TryGetCol("UnitMaxHP");
        int atkCol = TryGetCol("UnitATK");
        int defCol = TryGetCol("UnitDEF");
        if (defCol < 0)
        {
            defCol = TryGetCol("Def");
        }

        int luckCol = TryGetCol("Luck");

        int speedCol = TryGetCol("Speed");
        int criticalRateCol = TryGetCol("CriticalRate");
        int counterRateCol = TryGetCol("CounterRate");
        int avoidRateCol = TryGetCol("ReduceRate");

        int indexCol = TryGetCol("Index");
        int unitTypeCol = TryGetCol("UnitType");
        int nameCol = TryGetCol("Name");
        if (nameCol < 0)
        {
            nameCol = TryGetCol("UnitName");
        }

        if (nameCol < 0)
        {
            nameCol = TryGetCol("DisplayName");
        }

        int aiTypeCol = TryGetCol("AiType");
        if (aiTypeCol < 0)
        {
            aiTypeCol = TryGetCol("AI");
        }

        if (aiTypeCol < 0)
        {
            aiTypeCol = TryGetCol("AIType");
        }

        int isEnemyCol = TryGetCol("IsEnemy");

        for (int lineIndex = DataStartLineEnemyIndex0Based; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 2)
            {
                continue;
            }

            string index = GetField(fields, indexCol >= 0 ? indexCol : 0);
            if (string.IsNullOrWhiteSpace(index))
            {
                continue;
            }

            string unitType = GetField(fields, unitTypeCol >= 0 ? unitTypeCol : 1);
            string name = nameCol >= 0 ? GetField(fields, nameCol) : string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = unitType;
            }

            bool isEnemyRow;
            if (isEnemyCol >= 0)
            {
                var raw = GetField(fields, isEnemyCol);
                isEnemyRow = raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                             raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                isEnemyRow = IsEnemyUnitIndex(index);
            }

            int hp = ParseIntOrDefault(GetField(fields, hpCol), 1);
            int atk = ParseIntOrDefault(GetField(fields, atkCol), 0);
            int def = ParseIntOrDefault(GetField(fields, defCol), 0);
            int luck = ParseIntOrDefault(GetField(fields, luckCol), 0);

            float speed = ParseFloatPercentOrFloat(GetField(fields, speedCol), 0f);
            float criticalRate = ParseFloatPercentOrFloat(GetField(fields, criticalRateCol), 0f);
            float counterRate = ParseFloatPercentOrFloat(GetField(fields, counterRateCol), 0f);
            float avoidRate = ParseFloatPercentOrFloat(GetField(fields, avoidRateCol), 0f);

            var enemy = new EnemyData
            {
                Index = index,
                UnitType = unitType,
                Name = name,
                baseStats = new StatBlock(hp, atk, def, luck, speed, criticalRate, counterRate, avoidRate),
                IsEnemyRow = isEnemyRow
            };

            result.Add(enemy);
        }

        return result;
    }

    private static EnemyCsvLoadResult LoadEnemyDataSheetWithSkills(string[] lines)
    {
        var enemies = new List<EnemyData>();
        var skills = new List<SkillData>();
        var result = new EnemyCsvLoadResult { Enemies = enemies, Skills = skills };

        if (lines.Length <= DataStartLineEnemySheetIndex0Based)
        {
            return result;
        }

        for (int lineIndex = DataStartLineEnemySheetIndex0Based; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 14)
            {
                continue;
            }

            string idxRaw = GetField(fields, 0);
            if (string.IsNullOrWhiteSpace(idxRaw))
            {
                continue;
            }

            if (!int.TryParse(idxRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int enemyIndexNum))
            {
                continue;
            }

            string enemyName = GetField(fields, 1);
            if (string.IsNullOrWhiteSpace(enemyName))
            {
                continue;
            }

            float hp = ParseFloatSafe(GetField(fields, 3), 1f);
            float atk = ParseFloatSafe(GetField(fields, 4), 0f);
            float def = ParseFloatSafe(GetField(fields, 5), 0f);
            float luck = 0f;
            float speed = ParseFloatPercentOrFloatSafe(GetField(fields, 9), 0f);
            float criticalRate = ParseFloatPercentOrFloatSafe(GetField(fields, 6), 0f);
            float counterRate = ParseFloatPercentOrFloatSafe(GetField(fields, 7), 0f);
            float avoidRate = ParseFloatPercentOrFloatSafe(GetField(fields, 8), 0f);

            var enemy = new EnemyData
            {
                Index = idxRaw.Trim(),
                Name = enemyName,
                UnitType = GetField(fields, 2),
                baseStats = new StatBlock(hp, atk, def, luck, speed, criticalRate, counterRate, avoidRate),
                IsEnemyRow = true,
                UnitAI = fields.Count > 32 ? GetField(fields, 32) : string.Empty,
                ExperiencePoint = fields.Count > 33 ? ParseFloatSafe(GetField(fields, 33), 0f) : 0f,
                DropItem1Index = ParseIntListField(fields.Count > 34 ? GetField(fields, 34) : string.Empty),
                DropItem1Count = ParseIntListField(fields.Count > 35 ? GetField(fields, 35) : string.Empty),
                DropItem1Rate = ParseFloatListField(fields.Count > 36 ? GetField(fields, 36) : string.Empty)
            };

            enemies.Add(enemy);

            TryAppendEnemySkill(skills, fields, enemyIndexNum, enemyName, 1);
            TryAppendEnemySkill(skills, fields, enemyIndexNum, enemyName, 2);
        }

        return result;
    }

    private static void TryAppendEnemySkill(List<SkillData> skills, List<string> fields, int enemyIndexNum, string enemyName, int slot)
    {
        int baseCol = slot == 1 ? 10 : 20;
        if (fields.Count <= baseCol + 8)
        {
            return;
        }

        string skillName = GetField(fields, baseCol);
        if (string.IsNullOrWhiteSpace(skillName))
        {
            return;
        }

        string description = GetField(fields, baseCol + 1);
        int effect = ParseIntOrDefault(GetField(fields, baseCol + 2), 0);
        int range = ParseIntOrDefault(GetField(fields, baseCol + 3), 0);
        int rangeTarget = ParseIntOrDefault(GetField(fields, baseCol + 4), 0);
        int target = ParseIntOrDefault(GetField(fields, baseCol + 5), 0);
        string multiRaw = GetField(fields, baseCol + 6);
       
        int mtCount = ParseIntOrDefault(GetField(fields, baseCol + 8), 0);
        float skillVal = ParseFloatSafe(GetField(fields, baseCol + 9), 0f);

        var sd = new SkillData
        {
            // 적 고유 인덱스 + 슬롯(1/2) 조합 규칙: 20001 + 1 => 200011
            skillIndex = (enemyIndexNum * 10) + slot,
            skillClass = enemyName,
            acquireLevel = 1,
            skillName = skillName,
            description = description,
            // EnemyDataSheet 스킬 슬롯에는 IPCost 컬럼이 없어 기본값 0을 사용합니다.
            ipCost = 0,
            classSkillEffect = effect,
            classSkillRange = range,
            EnemySkill1Range = slot == 1 ? range : -1,
            EnemySkill2Range = slot == 2 ? range : -1,
            // EnemyDataSheet에는 별도 우선순위 컬럼이 없으므로 기본값(Any=1)을 사용합니다.
            classSkillRangeLine = 1,
            classSkillTarget = target,
            boundary = BuildEnemySkillBoundaryPatterns(multiRaw),
            multiTargetCount = mtCount,
            skillValue = skillVal,
            skillSubValue = 0f
        };

        skills.Add(sd);
    }

    /// <summary>적 스킬 멀티타겟 원문을 SkillTargetingMapper 패턴 인덱스(List&lt;int&gt;)로 변환합니다.</summary>
    private static List<int> BuildEnemySkillBoundaryPatterns(string multiTargetRaw)
    {
        return ParseIntListField(multiTargetRaw);
    }

    private static List<float> ParseFloatListField(string raw)
    {
        var list = new List<float>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return list;
        }

        raw = raw.Trim().Trim('"');
        if (raw.Length >= 2 && raw[0] == '{' && raw[raw.Length - 1] == '}')
        {
            raw = raw.Substring(1, raw.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return list;
        }

        var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            list.Add(ParseFloatPercentOrFloatSafe(parts[i], 0f));
        }

        return list;
    }

    public static List<EnemyData> LoadEnemyData(string csvText)
    {
        return LoadEnemyDataAndSkills(csvText).Enemies;
    }

    /// <summary>
    /// ClassSkillSheet CSV 파싱. 컬럼 순서는 시트 고정(헤더가 줄바꿈으로 깨져 있어 인덱스 기준).
    /// </summary>
    public static List<SkillData> LoadClassSkillData(string csvText)
    {
        var result = new List<SkillData>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return result;
        }

        csvText = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = csvText.Split('\n');
        if (lines.Length <= DataStartLineClassSkillIndex0Based)
        {
            return result;
        }

        for (int lineIndex = DataStartLineClassSkillIndex0Based; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            if (fields.Count < 4)
            {
                continue;
            }

            string indexRaw = GetField(fields, 0);
            if (string.IsNullOrWhiteSpace(indexRaw))
            {
                continue;
            }

            if (!int.TryParse(indexRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int skillIndex))
            {
                continue;
            }

            var row = new SkillData
            {
                skillIndex = skillIndex,
                skillClass = GetField(fields, 1),
                acquireLevel = ParseIntOrDefault(GetField(fields, 2), 0),
                skillName = GetField(fields, 3),
                description = GetField(fields, 4),
                // 새 CSV 스키마: 5=IPCost, 6=Effect, 7=Range, 8=RangeLine ...
                ipCost = ParseIntOrDefault(GetField(fields, 5), 0),
                classSkillEffect = ParseIntOrDefault(GetField(fields, 6), 0),
                classSkillRange = ParseIntOrDefault(GetField(fields, 7), 0),
                // classSkillRangeLine: 우선순위 전용 필드(0=FrontFirst, 1=Any, 2=BackFirst)
                // 파싱 실패/빈 값은 안전 기본값 1(Any)로 폴백합니다.
                classSkillRangeLine = ParseIntOrDefault(GetField(fields, 8), 1),
                classSkillTarget = ParseIntOrDefault(GetField(fields, 9), 0),
                // boundary: ClassSkillMultiTarget 컬럼 — 패턴 인덱스(0~9). 전체 진영은 classSkillTarget==2.
                boundary = ParseIntListField(GetField(fields, 10)),
                multiTargetCount = ParseIntOrDefault(GetField(fields, 12), 0),
                skillValue = ParseFloatSafe(GetField(fields, 13), 1f),
                skillSubValue = ParseFloatSafe(GetField(fields, 14), 0f)
            };

            result.Add(row);
        }

        return result;
    }

    public static List<LevelUpData> LoadLevelUpData(string csvText)
    {
        var result = new List<LevelUpData>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return result;
        }

        List<List<string>> rows = ParseCsvRowsPreserveQuotedNewlines(csvText);
        if (!TryFindHeaderRow(
                rows,
                new[]
                {
                    "레벨",
                    "랭크 표시",
                    "유닛당 랭크업 필요 경험치",
                    "스킬",
                    "최대 I.P 추가",
                    "신규 스킬 획득까지 필요한 전투 수",
                    "무기 획득 추천 타이밍"
                },
                out int headerRowIndex,
                out Dictionary<string, int> headerIndex))
        {
            return result;
        }

        for (int rowIndex = headerRowIndex + 1; rowIndex < rows.Count; rowIndex++)
        {
            if (TryParseLevelUpDataRow(rows[rowIndex], headerIndex, out LevelUpData row))
            {
                result.Add(row);
            }
        }

        return result;
    }

    private static bool TryParseLevelUpDataRow(
        IReadOnlyList<string> fields,
        IReadOnlyDictionary<string, int> headerIndex,
        out LevelUpData row)
    {
        row = null;
        string levelRaw = GetFieldByHeader(fields, headerIndex, "레벨");
        string rankRaw = GetFieldByHeader(fields, headerIndex, "랭크 표시");
        string expRaw = GetFieldByHeader(fields, headerIndex, "유닛당 랭크업 필요 경험치");

        if (string.IsNullOrWhiteSpace(levelRaw) &&
            string.IsNullOrWhiteSpace(rankRaw) &&
            string.IsNullOrWhiteSpace(expRaw))
        {
            return false;
        }

        row = new LevelUpData
        {
            level = ParseFloatSafe(levelRaw, 0f),
            Rank = ParseRankChar(rankRaw),
            expPerLevel = ParseIntOrDefault(expRaw, 0),
            skill = ParseBoolMarker(GetFieldByHeader(fields, headerIndex, "스킬")),
            MaxIP = ParseIntOrDefault(GetFieldByHeader(fields, headerIndex, "최대 I.P 추가"), 0),
            newSkillneedNum = ParseIntOrDefault(GetFieldByHeader(fields, headerIndex, "신규 스킬 획득까지 필요한 전투 수"), 0),
            weaponTiming = ParseBoolMarker(GetFieldByHeader(fields, headerIndex, "무기 획득 추천 타이밍"))
        };

        return true;
    }

    /// <summary>
    /// WeaponSheet CSV 파싱. 컬럼 순서는 시트 고정(헤더가 여러 줄로 나뉘어 인덱스 기준).
    /// </summary>
    public static List<WeaponData> LoadWeaponData(string csvText)
    {
        var result = new List<WeaponData>();
        if (string.IsNullOrWhiteSpace(csvText))
        {
            return result;
        }

        csvText = csvText.Replace("\r\n", "\n").Replace("\r", "\n");
        var lines = csvText.Split('\n');
        if (lines.Length <= DataStartLineWeaponIndex0Based)
        {
            return result;
        }

        for (int lineIndex = DataStartLineWeaponIndex0Based; lineIndex < lines.Length; lineIndex++)
        {
            var line = lines[lineIndex];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = ParseCsvLine(line);
            // WeaponSheet: 0~23 컬럼 고정(IPCost, WeaponSkillRangeLine 포함).
            if (fields.Count < 24)
            {
                continue;
            }

            string indexRaw = GetField(fields, 0);
            if (string.IsNullOrWhiteSpace(indexRaw))
            {
                continue;
            }

            if (!int.TryParse(indexRaw.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int weaponIndex))
            {
                continue;
            }

            var row = new WeaponData
            {
                WeaponIndex = weaponIndex,
                weaponClass = GetField(fields, 1),
                WeaponName = GetField(fields, 2),
                WeaponDescription = GetField(fields, 3),
                BonusHP = ParseFloatPercentOrFloatSafe(GetField(fields, 4), 0f),
                BonusATK = ParseFloatPercentOrFloatSafe(GetField(fields, 5), 0f),
                BonusDEF = ParseFloatPercentOrFloatSafe(GetField(fields, 6), 0f),
                BonusCriticalRate = ParseFloatPercentOrFloatSafe(GetField(fields, 7), 0f),
                BonusCounterRate = ParseFloatPercentOrFloatSafe(GetField(fields, 8), 0f),
                BonusReduceRate = ParseFloatPercentOrFloatSafe(GetField(fields, 9), 0f),
                BonusSpeed = ParseFloatPercentOrFloatSafe(GetField(fields, 10), 0f),
                WeaponSkillIndex = ParseIntOrDefault(GetField(fields, 11), 0),
                WeaponSkillName = GetField(fields, 12),
                WeaponSkillDescription = GetField(fields, 13),
                IPCost = ParseIntOrDefault(GetField(fields, 14), 0),
                WeaponSkillEffect = ParseIntOrDefault(GetField(fields, 15), 0),
                WeaponSkillRange = ParseIntOrDefault(GetField(fields, 16), 0),
                WeaponSkillRangeLine = ParseIntOrDefault(GetField(fields, 17), 1),
                WeaponSkillTarget = ParseIntOrDefault(GetField(fields, 18), 0),
                WeaponSkillMultiTarget = ParseIntListField(GetField(fields, 19)),
                WeaponSkillMultiTargetType = ParseIntOrDefault(GetField(fields, 20), 0),
                WeaponSkillMultiTargetCount = ParseIntOrDefault(GetField(fields, 21), 0),
                WeaponSkillValue = ParseFloatSafe(GetField(fields, 22), 0f),
                WeaponSkillSubValue = ParseFloatSafe(GetField(fields, 23), 0f)
            };

            result.Add(row);
        }

        return result;
    }

    /// <summary>
    /// CSV 셀을 정수 리스트로 변환. "1,2,3", "{1,2,3}" 형태 지원. 파싱 실패 항목은 건너뜀.
    /// TODO: 무기 스킬 멀티타겟 실행 로직은 전투 측에서 연결.
    /// </summary>
    private static List<int> ParseIntListField(string raw)
    {
        var list = new List<int>();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return list;
        }

        raw = raw.Trim().Trim('"').Trim();
        if (raw.Length >= 2 && raw[0] == '{' && raw[raw.Length - 1] == '}')
        {
            raw = raw.Substring(1, raw.Length - 2).Trim();
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return list;
        }

        var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            string t = parts[i].Trim();
            if (string.IsNullOrWhiteSpace(t))
            {
                continue;
            }

            if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v))
            {
                list.Add(v);
            }
        }

        return list;
    }

    private static float ParseFloatPercentOrFloatSafe(string raw, float defaultValue)
    {
        raw = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        bool isPercent = raw.Contains("%");
        raw = raw.Replace("%", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (!float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float value))
        {
            return defaultValue;
        }

        return isPercent ? value / 100f : value;
    }

    private static float ParseFloatSafe(string raw, float defaultValue)
    {
        raw = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (float.TryParse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture, out float f))
        {
            return f;
        }

        return defaultValue;
    }

    private static char ParseRankChar(string raw)
    {
        raw = raw?.Trim() ?? string.Empty;
        return string.IsNullOrEmpty(raw) ? '\0' : raw[0];
    }

    private static bool ParseBoolMarker(string raw)
    {
        raw = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        string normalized = raw.ToLowerInvariant();
        if (normalized == "0" ||
            normalized == "false" ||
            normalized == "no" ||
            normalized == "n" ||
            normalized == "x" ||
            normalized == "-" ||
            normalized == "없음")
        {
            return false;
        }

        return true;
    }

    private static string NormalizeHeader(string raw)
    {
        if (raw == null) return string.Empty;
        raw = raw.Trim().Trim('\"');

        if (!string.IsNullOrEmpty(raw) && raw[0] == '\uFEFF')
        {
            raw = raw.Substring(1);
        }

        return raw;
    }

    private static string GetField(List<string> fields, int colIndex)
    {
        if (colIndex < 0 || colIndex >= fields.Count) return string.Empty;
        return fields[colIndex]?.Trim() ?? string.Empty;
    }

    private static int ParseIntOrDefault(string raw, int defaultValue)
    {
        raw = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatValue))
        {
            return Mathf.RoundToInt(floatValue);
        }

        return defaultValue;
    }

    private static float ParseFloatPercentOrFloat(string raw, float defaultValue)
    {
        raw = raw?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        bool isPercent = raw.Contains("%");

        raw = raw.Replace("%", string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return defaultValue;
        }

        float value = float.Parse(raw, NumberStyles.Float | NumberStyles.AllowThousands, CultureInfo.InvariantCulture);
        return isPercent ? value / 100f : value;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        if (line == null)
        {
            return result;
        }

        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (c == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Length = 0;
                continue;
            }

            sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }

    public static List<List<string>> ParseCsvRowsPreserveQuotedNewlines(string csvText)
    {
        return ParseCsvTotal(csvText);
    }

    public static string NormalizeHeaderKey(string raw)
    {
        if (raw == null)
        {
            return string.Empty;
        }

        raw = raw.Trim().Trim('"');
        if (!string.IsNullOrEmpty(raw) && raw[0] == '\uFEFF')
        {
            raw = raw.Substring(1);
        }

        return raw
            .Replace("\r", string.Empty)
            .Replace("\n", string.Empty)
            .Replace("\t", string.Empty)
            .Replace(" ", string.Empty)
            .Trim();
    }

    public static Dictionary<string, int> BuildHeaderIndex(IReadOnlyList<string> headerRow)
    {
        var headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (headerRow == null)
        {
            return headerIndex;
        }

        for (int i = 0; i < headerRow.Count; i++)
        {
            string key = NormalizeHeaderKey(headerRow[i]);
            if (string.IsNullOrEmpty(key) || headerIndex.ContainsKey(key))
            {
                continue;
            }

            headerIndex.Add(key, i);
        }

        return headerIndex;
    }

    public static bool TryFindHeaderRow(
        IReadOnlyList<List<string>> rows,
        IReadOnlyList<string> requiredHeaderNames,
        out int headerRowIndex,
        out Dictionary<string, int> headerIndex)
    {
        headerRowIndex = -1;
        headerIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (rows == null || requiredHeaderNames == null || requiredHeaderNames.Count == 0)
        {
            return false;
        }

        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            Dictionary<string, int> candidateIndex = BuildHeaderIndex(rows[rowIndex]);
            bool hasAllRequiredHeaders = true;

            for (int i = 0; i < requiredHeaderNames.Count; i++)
            {
                string requiredKey = NormalizeHeaderKey(requiredHeaderNames[i]);
                if (string.IsNullOrEmpty(requiredKey) || !candidateIndex.ContainsKey(requiredKey))
                {
                    hasAllRequiredHeaders = false;
                    break;
                }
            }

            if (!hasAllRequiredHeaders)
            {
                continue;
            }

            headerRowIndex = rowIndex;
            headerIndex = candidateIndex;
            return true;
        }

        return false;
    }

    public static string GetFieldByHeader(
        IReadOnlyList<string> row,
        IReadOnlyDictionary<string, int> headerIndex,
        string headerName)
    {
        if (row == null || headerIndex == null)
        {
            return string.Empty;
        }

        string key = NormalizeHeaderKey(headerName);
        if (string.IsNullOrEmpty(key) || !headerIndex.TryGetValue(key, out int colIndex))
        {
            return string.Empty;
        }

        if (colIndex < 0 || colIndex >= row.Count)
        {
            return string.Empty;
        }

        return row[colIndex]?.Trim() ?? string.Empty;
    }



        public static List<List<string>> ParseCsvTotal(string csvText)
        {
            var grid = new List<List<string>>(); // 전체 표 (행들의 모음)
            var row = new List<string>();        // 현재 읽고 있는 한 줄 (칸들의 모음)

            if (string.IsNullOrEmpty(csvText))
            {
                return grid;
            }

            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < csvText.Length; i++)
            {
                char c = csvText[i];

                // 1. 큰따옴표 처리
                if (c == '"')
                {
                    // 따옴표 안에서 따옴표 2개가 연속으로("") 나오면 하나의 따옴표(")로 취급
                    if (inQuotes && i + 1 < csvText.Length && csvText[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++; // 두 번째 따옴표는 건너뜀
                    }
                    else
                    {
                        // 따옴표 열림/닫힘 상태 전환
                        inQuotes = !inQuotes;
                    }
                    continue;
                }

                // 2. 쉼표(,) 처리 - 칸 나누기
                // 따옴표 밖일 때만 쉼표를 다음 칸으로 인식
                if (c == ',' && !inQuotes)
                {
                    row.Add(sb.ToString());
                    sb.Length = 0;
                    continue;
                }

                // 3. 줄바꿈(\r, \n) 처리 - 행 나누기
                // 따옴표 밖일 때만 엔터를 다음 줄로 인식
                if ((c == '\r' || c == '\n') && !inQuotes)
                {
                    // 윈도우 환경의 줄바꿈(\r\n)이 연속으로 올 경우 한 번에 처리
                    if (c == '\r' && i + 1 < csvText.Length && csvText[i + 1] == '\n')
                    {
                        i++;
                    }

                    // 빈 줄이 아닐 경우에만 표(grid)에 추가
                    if (row.Count > 0 || sb.Length > 0)
                    {
                        row.Add(sb.ToString()); // 마지막 칸 데이터 추가
                        grid.Add(row);          // 완성된 한 줄을 전체 표에 추가

                        row = new List<string>(); // 다음 줄을 위해 리스트 초기화
                        sb.Length = 0;
                    }
                    continue;
                }

                // 4. 일반 문자나 "따옴표 안의 엔터/쉼표"는 그대로 글자로 추가
                sb.Append(c);
            }

            // 반복문이 끝난 후, 미처 들어가지 못한 마지막 줄 데이터가 있다면 추가
            if (row.Count > 0 || sb.Length > 0)
            {
                row.Add(sb.ToString());
                grid.Add(row);
            }

            return grid;
        }
}
