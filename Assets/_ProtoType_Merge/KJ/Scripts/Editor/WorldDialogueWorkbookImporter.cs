using System;
using System.Collections.Generic;
using System.IO;
using ASB.ExcelImport.Editor;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEngine;

/// <summary>Converts the designated planning workbook into build-ready dialogue data.</summary>
public sealed class WorldDialogueWorkbookImporter : AssetPostprocessor
{
    public const string SourcePath = "Assets/_ProtoType_Merge/KJ/Effects/Resources/H.I_히어로 월드 대사 기획서_1.0v.xlsx";
    public const string OutputFolder = "Assets/_ProtoType_Merge/KJ/Effects/Resources";
    public const string OutputPath = OutputFolder + "/" + ExplorationHeroDialogue.ResourceName + ".asset";
    private static readonly string[] Headers = {
        "hero_id", "out_of_action_range_dialogue", "funds_acquired_dialogue",
        "crystal_acquired_dialogue", "medal_acquired_dialogue", "materials_acquired_dialogue",
        "gate_opened_dialogue", "idle_5_seconds_dialogue", "base_arrival_dialogue"
    };

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (Array.IndexOf(imported, SourcePath) < 0 && Array.IndexOf(moved, SourcePath) < 0) return;
        try { Import(); }
        catch (Exception e) { Debug.LogError("[HeroDialogue] Workbook import failed: " + e.Message); }
    }

    [MenuItem("Tools/HeroInfluence/월드 대사 테이블 갱신")]
    public static void Import()
    {
        var table = new ExcelSheetParseResult
        {
            SheetName = "월드 대사",
            CustomClassName = "WorldHeroDialogue",
            OutputAssetName = ExplorationHeroDialogue.ResourceName,
            Names = new List<string>(Headers)
        };
        for (int i = 0; i < Headers.Length; i++) table.Types.Add(i == 0 ? "int" : "string");
        using (var stream = File.Open(SourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var workbook = new XSSFWorkbook(stream))
        {
            var sheet = workbook.GetSheet("월드 대사") ?? throw new InvalidDataException("Missing 월드 대사 sheet.");
            var formatter = new DataFormatter();
            var columns = new Dictionary<string, int>();
            int headerRow = -1;
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                for (int c = 0; c < row.LastCellNum; c++)
                    if (formatter.FormatCellValue(row.GetCell(c)).Trim() == "hero_id") { headerRow = r; break; }
                if (headerRow >= 0)
                {
                    for (int c = 0; c < row.LastCellNum; c++)
                        columns[formatter.FormatCellValue(row.GetCell(c)).Trim()] = c;
                    break;
                }
            }
            foreach (string header in Headers)
                if (!columns.ContainsKey(header)) throw new InvalidDataException("Missing column: " + header);
            var ids = new HashSet<int>();
            for (int r = headerRow + 1; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                string idText = formatter.FormatCellValue(row.GetCell(columns[Headers[0]])).Trim();
                if (string.IsNullOrEmpty(idText)) continue;
                if (!int.TryParse(idText, out int id) || id < 0 || id > 3 || !ids.Add(id))
                    throw new InvalidDataException("Invalid or duplicate hero ID at row " + (r + 1));
                var entry = new List<string> { id.ToString(System.Globalization.CultureInfo.InvariantCulture) };
                for (int c = 1; c < Headers.Length; c++)
                {
                    string message = formatter.FormatCellValue(row.GetCell(columns[Headers[c]])).Trim();
                    if (string.IsNullOrWhiteSpace(message))
                        throw new InvalidDataException("Empty dialogue at row " + (r + 1) + ": " + Headers[c]);
                    entry.Add(message);
                }
                table.Rows.Add(entry);
            }
            if (ids.Count != 4) throw new InvalidDataException("Expected four hero rows.");
        }
        ScriptableExporter.ExportAll(new[] { table }, assetFolder: OutputFolder);
        var saved = AssetDatabase.LoadAssetAtPath<WorldHeroDialogueDataTable>(OutputPath);
        if (saved == null || saved.DataList.Count != 4)
            throw new InvalidDataException("World dialogue table export failed.");
    }
}
