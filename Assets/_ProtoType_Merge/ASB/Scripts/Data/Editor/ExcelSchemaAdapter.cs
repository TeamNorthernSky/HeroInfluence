using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal enum TemplateCompatibilityStatus
    {
        NoSharedTemplate,
        Match,
        Mismatch
    }

    internal sealed class TemplateCompatibilityReport
    {
        public TemplateCompatibilityStatus Status = TemplateCompatibilityStatus.NoSharedTemplate;
        public string TemplateClassName = string.Empty;
        public string CurrentSignature = string.Empty;
        public readonly List<ExcelSheetSchemaSO> MatchingSchemas = new List<ExcelSheetSchemaSO>();
        public readonly List<ExcelSheetSchemaSO> ConflictingSchemas = new List<ExcelSheetSchemaSO>();
    }

    internal enum SchemaCandidateKind
    {
        ExactPhysical,
        SameWorkbookLogical,
        Logical,
        SameStructure
    }

    internal readonly struct SchemaCandidateInfo
    {
        public readonly ExcelSheetSchemaSO Schema;
        public readonly SchemaCandidateKind Kind;
        public readonly int Priority;

        public SchemaCandidateInfo(ExcelSheetSchemaSO schema, SchemaCandidateKind kind, int priority)
        {
            Schema = schema;
            Kind = kind;
            Priority = priority;
        }
    }

    internal static class ExcelSchemaAdapter
    {
        public static ExcelSheetSchemaSO CreateDefaultSchema(RawExcelSheetSO raw)
        {
            string className = CodeGenerator.ToTypeBaseName(raw.sheetName);
            string schemaFolder = ExcelImportPaths.GetSchemaFolder(raw.workbookFileName);
            ExcelRawImportUtility.EnsureFolder(schemaFolder);
            string schemaPath = $"{schemaFolder}/{ExcelRawImportUtility.SanitizeAssetFileName(className)}_Schema.asset";
            schemaPath = AssetDatabase.GenerateUniqueAssetPath(schemaPath);

            ExcelSheetSchemaSO schema = ScriptableObject.CreateInstance<ExcelSheetSchemaSO>();
            schema.schemaGuid = Guid.NewGuid().ToString("N");
            schema.sourceWorkbookGuid = raw.sourceWorkbookGuid;
            schema.sourceWorkbookPath = raw.sourceWorkbookPath;
            schema.workbookFileName = raw.workbookFileName;
            schema.sourceSheetName = raw.sheetName;
            schema.physicalSheetId = raw.physicalSheetId;
            schema.logicalSheetKey = raw.logicalSheetKey;
            schema.templateClassName = className;
            schema.className = className;
            schema.outputAssetName = className + "DataTable";
            schema.headerRowIndex = 0;
            schema.dataStartRowIndex = raw.Rows.Count > 1 ? 1 : 0;
            schema.useDictionary = false;
            schema.requireUniqueKey = false;
            schema.duplicateKeyPolicy = DuplicateKeyPolicy.Error;
            schema.Columns = BuildDefaultColumns(raw);
            schema.templateSignature = BuildTemplateSignature(schema);

            AssetDatabase.CreateAsset(schema, schemaPath);
            AssetDatabase.SaveAssets();
            return schema;
        }

        public static ExcelSheetSchemaSO CreateSchemaFromTemplate(RawExcelSheetSO raw, ExcelSheetSchemaSO template)
        {
            if (raw == null)
            {
                throw new ArgumentNullException(nameof(raw));
            }

            if (template == null)
            {
                throw new ArgumentNullException(nameof(template));
            }

            string templateClassName = GetTemplateClassName(template);
            string schemaFolder = ExcelImportPaths.GetSchemaFolder(raw.workbookFileName);
            ExcelRawImportUtility.EnsureFolder(schemaFolder);
            string schemaPath = $"{schemaFolder}/{ExcelRawImportUtility.SanitizeAssetFileName(raw.sheetName)}_{ExcelRawImportUtility.SanitizeAssetFileName(templateClassName)}_Schema.asset";
            schemaPath = AssetDatabase.GenerateUniqueAssetPath(schemaPath);

            ExcelSheetSchemaSO schema = ScriptableObject.CreateInstance<ExcelSheetSchemaSO>();
            schema.schemaGuid = Guid.NewGuid().ToString("N");
            schema.sourceWorkbookGuid = raw.sourceWorkbookGuid;
            schema.sourceWorkbookPath = raw.sourceWorkbookPath;
            schema.workbookFileName = raw.workbookFileName;
            schema.sourceSheetName = raw.sheetName;
            schema.physicalSheetId = raw.physicalSheetId;
            schema.logicalSheetKey = raw.logicalSheetKey;
            schema.templateClassName = templateClassName;
            schema.className = templateClassName;
            schema.outputAssetName = CodeGenerator.ToTypeBaseName(raw.sheetName) + "DataTable";
            schema.headerRowIndex = template.headerRowIndex;
            schema.dataStartRowIndex = template.dataStartRowIndex;
            schema.useDictionary = template.useDictionary;
            schema.requireUniqueKey = template.requireUniqueKey;
            schema.duplicateKeyPolicy = template.duplicateKeyPolicy;
            schema.targetTableAssetGuid = string.Empty;
            schema.targetTableAssetPath = string.Empty;
            schema.Columns = template.Columns
                .Where(c => c != null)
                .Select(CloneColumn)
                .ToList();
            schema.templateSignature = BuildTemplateSignature(schema);

            AssetDatabase.CreateAsset(schema, schemaPath);
            AssetDatabase.SaveAssets();
            return schema;
        }

        public static bool CanReuseSchemaForRaw(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            return raw != null &&
                   schema != null &&
                   schema.physicalSheetId == raw.physicalSheetId &&
                   string.Equals(GetSchemaWorkbookName(schema), raw.workbookFileName, StringComparison.Ordinal);
        }

        public static bool RequiresSchemaClone(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            return raw != null && schema != null && !CanReuseSchemaForRaw(raw, schema);
        }

        public static ExcelSheetSchemaSO EnsureSchemaForRaw(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            if (raw == null || schema == null)
            {
                return null;
            }

            return CanReuseSchemaForRaw(raw, schema)
                ? schema
                : CreateSchemaFromTemplate(raw, schema);
        }

        public static List<ExcelSheetSchemaSO> FindSchemaCandidates(RawExcelSheetSO raw)
        {
            return FindSchemaCandidateInfos(raw)
                .Select(x => x.Schema)
                .ToList();
        }

        public static List<SchemaCandidateInfo> FindSchemaCandidateInfos(RawExcelSheetSO raw, ExcelSheetSchemaSO currentSchema = null)
        {
            var result = new List<SchemaCandidateInfo>();
            if (raw == null)
            {
                return result;
            }

            ExcelRawImportUtility.EnsureFolder(ExcelImportPaths.SchemaFolder);
            string[] guids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            string currentSchemaPath = currentSchema == null ? string.Empty : AssetDatabase.GetAssetPath(currentSchema);
            string currentSignature = currentSchema == null ? string.Empty : BuildTemplateSignature(currentSchema);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(currentSchemaPath) && string.Equals(path, currentSchemaPath, StringComparison.Ordinal))
                {
                    continue;
                }

                ExcelSheetSchemaSO schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(path);
                if (schema == null)
                {
                    continue;
                }

                if (schema.physicalSheetId == raw.physicalSheetId)
                {
                    result.Add(new SchemaCandidateInfo(schema, SchemaCandidateKind.ExactPhysical, 0));
                    continue;
                }

                if (schema.logicalSheetKey == raw.logicalSheetKey)
                {
                    bool sameWorkbookFolder = string.Equals(
                        GetSchemaWorkbookName(schema),
                        raw.workbookFileName,
                        StringComparison.Ordinal);
                    result.Add(new SchemaCandidateInfo(
                        schema,
                        sameWorkbookFolder ? SchemaCandidateKind.SameWorkbookLogical : SchemaCandidateKind.Logical,
                        sameWorkbookFolder ? 1 : 2));
                }

                if (!string.IsNullOrEmpty(currentSignature) &&
                    string.Equals(BuildTemplateSignature(schema), currentSignature, StringComparison.Ordinal))
                {
                    result.Add(new SchemaCandidateInfo(schema, SchemaCandidateKind.SameStructure, 3));
                }
            }

            return result
                .GroupBy(x => AssetDatabase.GetAssetPath(x.Schema), StringComparer.Ordinal)
                .Select(g => g.OrderBy(x => x.Priority).First())
                .OrderBy(x => x.Priority)
                .ThenBy(x => x.Schema.name)
                .ToList();
        }

        public static bool Validate(RawExcelSheetSO raw, ExcelSheetSchemaSO schema, out List<string> errors, out List<string> warnings)
        {
            errors = new List<string>();
            warnings = new List<string>();

            if (raw == null)
            {
                errors.Add("Raw sheet is missing.");
                return false;
            }

            if (schema == null)
            {
                errors.Add("Schema is missing.");
                return false;
            }

            if (!CanReuseSchemaForRaw(raw, schema))
            {
                errors.Add("Schema belongs to another raw sheet or workbook. Apply it as a candidate to create a raw-specific schema copy.");
            }

            string templateClassName = GetTemplateClassName(schema);
            if (string.IsNullOrWhiteSpace(templateClassName))
            {
                errors.Add("Template class name is required.");
            }
            else if (CodeGenerator.ToTypeBaseName(templateClassName) != templateClassName)
            {
                errors.Add("Template class name must be a safe C# type name.");
            }

            string outputAssetName = GetOutputAssetName(schema);
            if (string.IsNullOrWhiteSpace(outputAssetName))
            {
                errors.Add("Output asset name is required.");
            }

            if (schema.dataStartRowIndex <= schema.headerRowIndex)
            {
                errors.Add("Data start row must be after the header row.");
            }

            List<ExcelColumnMapping> included = schema.Columns.Where(c => c != null && c.include).ToList();
            if (included.Count == 0)
            {
                errors.Add("At least one column must be included.");
            }

            var fieldNames = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < included.Count; i++)
            {
                ExcelColumnMapping column = included[i];
                if (string.IsNullOrWhiteSpace(column.fieldName))
                {
                    errors.Add($"Column {column.sourceColumnIndex}: field name is required.");
                }
                else if (CodeGenerator.SanitizeFieldName(column.fieldName) != column.fieldName)
                {
                    errors.Add($"Column '{column.fieldName}' is not a safe C# field name.");
                }
                else if (!fieldNames.Add(column.fieldName))
                {
                    errors.Add($"Duplicate field name: {column.fieldName}");
                }

                if (column.sourceColumnIndex < 0)
                {
                    errors.Add($"Column '{column.fieldName}' has invalid source index.");
                }

                if (!string.IsNullOrWhiteSpace(column.defaultValue) && !CanParse(column.defaultValue, column.fieldType, column.listDelimiter))
                {
                    errors.Add($"Default value for '{column.fieldName}' cannot be parsed as {column.fieldType}.");
                }
            }

            ExcelColumnMapping keyColumn = schema.Columns.FirstOrDefault(c => c != null && c.isKey);
            if (schema.useDictionary)
            {
                if (keyColumn == null)
                {
                    errors.Add("Dictionary mode requires a key column.");
                }
                else if (!keyColumn.include)
                {
                    errors.Add("Dictionary key column must be included.");
                }
            }

            ValidateRows(raw, schema, included, keyColumn, errors, warnings);
            ValidateTemplateCompatibility(schema, included, errors);
            ValidateOutputAssetPathUnique(schema, errors);

            bool valid = errors.Count == 0;
            raw.importStatus = valid ? RawImportStatus.Validated : RawImportStatus.ValidationFailed;
            raw.lastError = valid ? string.Empty : string.Join("\n", errors);
            schema.templateClassName = templateClassName;
            schema.className = templateClassName;
            schema.outputAssetName = outputAssetName;
            schema.templateSignature = BuildTemplateSignature(schema);
            EditorUtility.SetDirty(schema);
            EditorUtility.SetDirty(raw);
            return valid;
        }

        public static ExcelSheetParseResult ToParseResult(RawExcelSheetSO raw, ExcelSheetSchemaSO schema)
        {
            var included = schema.Columns
                .Where(c => c != null && c.include)
                .ToList();

            if (schema.useDictionary)
            {
                int keyIndex = included.FindIndex(c => c.isKey);
                if (keyIndex > 0)
                {
                    ExcelColumnMapping key = included[keyIndex];
                    included.RemoveAt(keyIndex);
                    included.Insert(0, key);
                }
            }

            var result = new ExcelSheetParseResult
            {
                SheetName = raw.sheetName,
                CustomClassName = GetTemplateClassName(schema),
                OutputAssetName = GetOutputAssetName(schema),
                TemplateSignature = BuildTemplateSignature(schema)
            };

            for (int i = 0; i < included.Count; i++)
            {
                result.Types.Add(ToGeneratorType(included[i].fieldType));
                result.Names.Add(included[i].fieldName);
                if (IsListFieldType(included[i].fieldType))
                {
                    result.ListDelimiters[included[i].fieldName] = included[i].listDelimiter;
                }
            }

            for (int r = schema.dataStartRowIndex; r < raw.Rows.Count; r++)
            {
                RawExcelRow row = raw.Rows[r];
                if (row == null || IsRawRowEmpty(row))
                {
                    continue;
                }

                var values = new List<string>(included.Count);
                for (int c = 0; c < included.Count; c++)
                {
                    ExcelColumnMapping column = included[c];
                    string value = column.sourceColumnIndex >= 0 && column.sourceColumnIndex < row.Cells.Count
                        ? row.Cells[column.sourceColumnIndex]
                        : string.Empty;
                    values.Add(string.IsNullOrWhiteSpace(value) ? column.defaultValue ?? string.Empty : value);
                }

                result.Rows.Add(values);
            }

            return result;
        }

        public static string ToGeneratorType(ExcelSchemaFieldType type)
        {
            switch (type)
            {
                case ExcelSchemaFieldType.Int: return "int";
                case ExcelSchemaFieldType.Float: return "float";
                case ExcelSchemaFieldType.Bool: return "bool";
                case ExcelSchemaFieldType.Percent: return "percent";
                case ExcelSchemaFieldType.ListString: return "list<string>";
                case ExcelSchemaFieldType.ListInt: return "list<int>";
                case ExcelSchemaFieldType.ListFloat: return "list<float>";
                case ExcelSchemaFieldType.ListBool: return "list<bool>";
                default: return "string";
            }
        }

        public static string GetTargetAssetPath(ExcelSheetSchemaSO schema)
        {
            string excelName = !string.IsNullOrWhiteSpace(schema.workbookFileName)
                ? schema.workbookFileName
                : string.IsNullOrWhiteSpace(schema.sourceWorkbookPath)
                    ? "SchemaGenerated"
                    : System.IO.Path.GetFileNameWithoutExtension(schema.sourceWorkbookPath);
            string folder = ExcelImportPaths.GetTableAssetFolder(excelName);
            string tableName = GetOutputAssetName(schema);
            return $"{folder}/{tableName}.asset";
        }

        public static string GetTemplateClassName(ExcelSheetSchemaSO schema)
        {
            if (schema == null)
            {
                return "Sheet";
            }

            string raw = !string.IsNullOrWhiteSpace(schema.templateClassName)
                ? schema.templateClassName
                : schema.className;
            return CodeGenerator.ToTypeBaseName(raw);
        }

        public static string GetOutputAssetName(ExcelSheetSchemaSO schema)
        {
            if (schema == null)
            {
                return "SheetDataTable";
            }

            string raw = !string.IsNullOrWhiteSpace(schema.outputAssetName)
                ? schema.outputAssetName
                : GetTemplateClassName(schema) + "DataTable";
            return SanitizeOutputAssetName(raw, GetTemplateClassName(schema) + "DataTable");
        }

        public static string SanitizeOutputAssetName(string raw, string fallback)
        {
            string safe = CodeGenerator.ToTypeBaseName(raw);
            if (string.IsNullOrWhiteSpace(safe))
            {
                safe = string.IsNullOrWhiteSpace(fallback) ? "SheetDataTable" : CodeGenerator.ToTypeBaseName(fallback);
            }

            return safe;
        }

        public static string BuildTemplateSignature(ExcelSheetSchemaSO schema)
        {
            if (schema == null || schema.Columns == null)
            {
                return string.Empty;
            }

            return BuildTemplateSignature(
                GetGenerationColumns(schema, schema.Columns.Where(c => c != null && c.include)),
                schema.useDictionary);
        }

        private static string BuildTemplateSignature(IEnumerable<ExcelColumnMapping> columns, bool useDictionary)
        {
            var sb = new StringBuilder();
            sb.Append("dict=");
            sb.Append(useDictionary ? "1" : "0");
            foreach (ExcelColumnMapping column in columns)
            {
                sb.Append("|");
                sb.Append(CodeGenerator.SanitizeFieldName(column.fieldName));
                sb.Append(":");
                sb.Append(column.fieldType);
                sb.Append(":");
                sb.Append(IsListFieldType(column.fieldType) ? column.listDelimiter.ToString() : ListDelimiter.Comma.ToString());
            }

            return sb.ToString();
        }

        public static TemplateCompatibilityReport GetTemplateCompatibilityReport(ExcelSheetSchemaSO schema)
        {
            var report = new TemplateCompatibilityReport();
            if (schema == null || string.IsNullOrWhiteSpace(GetTemplateClassName(schema)))
            {
                return report;
            }

            report.TemplateClassName = GetTemplateClassName(schema);
            report.CurrentSignature = BuildTemplateSignature(schema);
            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder))
            {
                return report;
            }

            string currentPath = AssetDatabase.GetAssetPath(schema);
            string[] guids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(path, currentPath, StringComparison.Ordinal))
                {
                    continue;
                }

                ExcelSheetSchemaSO other = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(path);
                if (other == null || GetTemplateClassName(other) != report.TemplateClassName)
                {
                    continue;
                }

                if (string.Equals(report.CurrentSignature, BuildTemplateSignature(other), StringComparison.Ordinal))
                {
                    report.MatchingSchemas.Add(other);
                }
                else
                {
                    report.ConflictingSchemas.Add(other);
                }
            }

            if (report.ConflictingSchemas.Count > 0)
            {
                report.Status = TemplateCompatibilityStatus.Mismatch;
            }
            else if (report.MatchingSchemas.Count > 0)
            {
                report.Status = TemplateCompatibilityStatus.Match;
            }

            return report;
        }

        private static string GetSchemaWorkbookName(ExcelSheetSchemaSO schema)
        {
            if (!string.IsNullOrWhiteSpace(schema.workbookFileName))
            {
                return schema.workbookFileName;
            }

            if (!string.IsNullOrWhiteSpace(schema.sourceWorkbookPath))
            {
                return System.IO.Path.GetFileNameWithoutExtension(schema.sourceWorkbookPath);
            }

            string path = AssetDatabase.GetAssetPath(schema).Replace('\\', '/');
            string prefix = ExcelImportPaths.SchemaFolder + "/";
            if (path.StartsWith(prefix, StringComparison.Ordinal))
            {
                string rest = path.Substring(prefix.Length);
                int slash = rest.IndexOf('/');
                if (slash > 0)
                {
                    return rest.Substring(0, slash);
                }
            }

            return string.Empty;
        }

        private static void ValidateTemplateCompatibility(
            ExcelSheetSchemaSO schema,
            List<ExcelColumnMapping> included,
            List<string> errors)
        {
            TemplateCompatibilityReport report = GetTemplateCompatibilityReport(schema);
            if (report.Status != TemplateCompatibilityStatus.Mismatch)
            {
                return;
            }

            for (int i = 0; i < report.ConflictingSchemas.Count; i++)
            {
                errors.Add(
                    $"Template '{report.TemplateClassName}' is already used by incompatible schema '{report.ConflictingSchemas[i].name}'. " +
                    "Included field order/name/type/list delimiter must match before sharing a generated script.");
            }
        }

        private static void ValidateOutputAssetPathUnique(ExcelSheetSchemaSO schema, List<string> errors)
        {
            if (schema == null || !AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder))
            {
                return;
            }

            string currentPath = AssetDatabase.GetAssetPath(schema);
            string targetAssetPath = GetTargetAssetPath(schema);
            string currentSchemaGuid = schema.schemaGuid ?? string.Empty;
            string[] guids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.Equals(path, currentPath, StringComparison.Ordinal))
                {
                    continue;
                }

                ExcelSheetSchemaSO other = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(path);
                if (other == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(currentSchemaGuid) &&
                    string.Equals(other.schemaGuid, currentSchemaGuid, StringComparison.Ordinal))
                {
                    errors.Add($"Schema GUID is duplicated with '{other.name}'. Create a new schemaGuid before generating.");
                    continue;
                }

                if (string.Equals(GetTargetAssetPath(other), targetAssetPath, StringComparison.Ordinal))
                {
                    errors.Add(
                        $"Output asset path is already used by schema '{other.name}': {targetAssetPath}. " +
                        "Use a unique Output Asset name for this sheet.");
                }
            }
        }

        private static List<ExcelColumnMapping> GetGenerationColumns(ExcelSheetSchemaSO schema, IEnumerable<ExcelColumnMapping> included)
        {
            var columns = included.ToList();
            if (schema != null && schema.useDictionary)
            {
                int keyIndex = columns.FindIndex(c => c != null && c.isKey);
                if (keyIndex > 0)
                {
                    ExcelColumnMapping key = columns[keyIndex];
                    columns.RemoveAt(keyIndex);
                    columns.Insert(0, key);
                }
            }

            return columns;
        }

        private static ExcelColumnMapping CloneColumn(ExcelColumnMapping source)
        {
            return new ExcelColumnMapping
            {
                sourceColumnIndex = source.sourceColumnIndex,
                sourceHeaderName = source.sourceHeaderName,
                fieldName = source.fieldName,
                displayName = source.displayName,
                fieldType = source.fieldType,
                listDelimiter = source.listDelimiter,
                include = source.include,
                defaultValue = source.defaultValue,
                isKey = source.isKey
            };
        }

        private static List<ExcelColumnMapping> BuildDefaultColumns(RawExcelSheetSO raw)
        {
            var columns = new List<ExcelColumnMapping>();
            RawExcelRow header = raw.Rows.Count > 0 ? raw.Rows[0] : null;
            int count = header != null ? header.Cells.Count : 0;
            var usedNames = new Dictionary<string, int>(StringComparer.Ordinal);

            for (int i = 0; i < count; i++)
            {
                string headerName = header.Cells[i];
                string fieldName = MakeFieldName(headerName, i, usedNames);
                columns.Add(new ExcelColumnMapping
                {
                    sourceColumnIndex = i,
                    sourceHeaderName = headerName,
                    fieldName = fieldName,
                    displayName = headerName,
                    fieldType = ExcelSchemaFieldType.String,
                    include = !string.IsNullOrWhiteSpace(headerName),
                    defaultValue = string.Empty,
                    isKey = i == 0
                });
            }

            return columns;
        }

        private static string MakeFieldName(string header, int index, Dictionary<string, int> usedNames)
        {
            string sanitized = CodeGenerator.SanitizeFieldName(header);
            bool headerLooksUsable = !string.IsNullOrWhiteSpace(header) &&
                                     header.All(c => char.IsLetterOrDigit(c) || c == '_') &&
                                     !char.IsDigit(header.Trim()[0]);

            string baseName = headerLooksUsable ? sanitized : $"Field_{index + 1:00}";
            if (!usedNames.TryGetValue(baseName, out int count))
            {
                usedNames[baseName] = 1;
                return baseName;
            }

            count++;
            usedNames[baseName] = count;
            return $"{baseName}_{count}";
        }

        private static void ValidateRows(
            RawExcelSheetSO raw,
            ExcelSheetSchemaSO schema,
            List<ExcelColumnMapping> included,
            ExcelColumnMapping keyColumn,
            List<string> errors,
            List<string> warnings)
        {
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int r = Math.Max(0, schema.dataStartRowIndex); r < raw.Rows.Count; r++)
            {
                RawExcelRow row = raw.Rows[r];
                if (row == null || IsRawRowEmpty(row))
                {
                    continue;
                }

                for (int i = 0; i < included.Count; i++)
                {
                    ExcelColumnMapping column = included[i];
                    string value = GetCellValue(row, column);
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        value = column.defaultValue;
                    }

                    if (!string.IsNullOrWhiteSpace(value) && !CanParse(value, column.fieldType, column.listDelimiter))
                    {
                        errors.Add($"Row {r + 1}, column '{column.fieldName}' cannot parse '{value}' as {column.fieldType}.");
                    }
                }

                if (keyColumn != null)
                {
                    string key = GetCellValue(row, keyColumn);
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        if (schema.useDictionary || schema.requireUniqueKey)
                        {
                            errors.Add($"Row {r + 1}: key column '{keyColumn.fieldName}' is empty.");
                        }
                        continue;
                    }

                    if (!keys.Add(key))
                    {
                        if (schema.useDictionary || schema.requireUniqueKey)
                        {
                            errors.Add($"Duplicate key '{key}' at row {r + 1}.");
                        }
                        else
                        {
                            warnings.Add($"Duplicate key '{key}' at row {r + 1}.");
                        }
                    }
                }
            }
        }

        private static string GetCellValue(RawExcelRow row, ExcelColumnMapping column)
        {
            return column.sourceColumnIndex >= 0 && column.sourceColumnIndex < row.Cells.Count
                ? row.Cells[column.sourceColumnIndex]
                : string.Empty;
        }

        private static bool CanParse(string raw, ExcelSchemaFieldType type, ListDelimiter listDelimiter)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return true;
            }

            string value = raw.Trim();
            switch (type)
            {
                case ExcelSchemaFieldType.Int:
                    return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _) ||
                           float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case ExcelSchemaFieldType.Float:
                    return float.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case ExcelSchemaFieldType.Percent:
                    return float.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);
                case ExcelSchemaFieldType.Bool:
                    return bool.TryParse(value, out _) || value == "0" || value == "1" ||
                           value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("no", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("y", StringComparison.OrdinalIgnoreCase) ||
                           value.Equals("n", StringComparison.OrdinalIgnoreCase);
                case ExcelSchemaFieldType.ListString:
                case ExcelSchemaFieldType.ListInt:
                case ExcelSchemaFieldType.ListFloat:
                case ExcelSchemaFieldType.ListBool:
                    return ExcelListParser.CanParseList(value, type, listDelimiter);
                default:
                    return true;
            }
        }

        private static bool IsListFieldType(ExcelSchemaFieldType type)
        {
            return type == ExcelSchemaFieldType.ListString ||
                   type == ExcelSchemaFieldType.ListInt ||
                   type == ExcelSchemaFieldType.ListFloat ||
                   type == ExcelSchemaFieldType.ListBool;
        }

        private static bool IsRawRowEmpty(RawExcelRow row)
        {
            return row.Cells == null || row.Cells.All(string.IsNullOrWhiteSpace);
        }
    }
}
