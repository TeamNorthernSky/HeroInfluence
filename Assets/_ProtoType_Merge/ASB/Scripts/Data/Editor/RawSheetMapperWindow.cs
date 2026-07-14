using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public sealed class RawSheetMapperWindow : EditorWindow
    {
        private readonly List<RawExcelSheetSO> _rawSheets = new List<RawExcelSheetSO>();
        private readonly List<SchemaCandidateInfo> _schemaCandidates = new List<SchemaCandidateInfo>();
        private readonly List<string> _validationErrors = new List<string>();
        private readonly List<string> _validationWarnings = new List<string>();
        private int _selectedRawIndex;
        private ExcelSheetSchemaSO _schema;
        private TemplateCompatibilityReport _templateReport;
        private bool _candidateCacheDirty = true;
        private bool _schemaCloneRequired;
        private string _schemaApplyMessage;
        private MessageType _schemaApplyMessageType = MessageType.Info;
        private Vector2 _rawListScroll;
        private Vector2 _columnScroll;
        private Vector2 _previewScroll;

        [MenuItem("Tools/Excel Importer/Raw Sheet Mapper")]
        public static void ShowWindow()
        {
            RawSheetMapperWindow window = GetWindow<RawSheetMapperWindow>("Raw Sheet Mapper");
            window.minSize = new Vector2(760f, 520f);
            window.RefreshRawList();
            window.Show();
        }

        private void OnEnable()
        {
            RefreshRawList();
        }

        private void OnGUI()
        {
            DrawToolbar();
            EditorGUILayout.Space(6f);

            EditorGUILayout.BeginHorizontal();
            DrawRawList();
            DrawMapper();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if (GUILayout.Button("Import Markerless Excel", EditorStyles.toolbarButton, GUILayout.Width(160f)))
            {
                ImportMarkerlessExcel();
            }

            if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            {
                RefreshRawList();
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawRawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(230f));
            EditorGUILayout.LabelField("Raw Sheets", EditorStyles.boldLabel);
            _rawListScroll = EditorGUILayout.BeginScrollView(_rawListScroll, GUI.skin.box);
            for (int i = 0; i < _rawSheets.Count; i++)
            {
                RawExcelSheetSO raw = _rawSheets[i];
                GUIStyle style = i == _selectedRawIndex ? EditorStyles.toolbarButton : EditorStyles.miniButton;
                if (GUILayout.Button($"{raw.workbookFileName}\n{raw.sheetName}\n{raw.importStatus}", style, GUILayout.Height(64f)))
                {
                    _selectedRawIndex = i;
                    LoadBestSchemaForSelectedRaw();
                }
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawMapper()
        {
            RawExcelSheetSO raw = SelectedRaw;
            if (raw == null)
            {
                EditorGUILayout.HelpBox("No Raw SO found. Import a markerless Excel sheet first.", MessageType.Info);
                return;
            }

            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"{raw.workbookFileName} / {raw.sheetName}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Physical ID", raw.physicalSheetId, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Logical Key", raw.logicalSheetKey, EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            _schema = (ExcelSheetSchemaSO)EditorGUILayout.ObjectField("Schema", _schema, typeof(ExcelSheetSchemaSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                UpdateSchemaCloneRequirementMessage();
                MarkCandidateCacheDirty();
            }
            if (GUILayout.Button("New Schema", GUILayout.Width(100f)))
            {
                _schema = ExcelSchemaAdapter.CreateDefaultSchema(raw);
                raw.importStatus = RawImportStatus.SchemaApplied;
                EditorUtility.SetDirty(raw);
                ClearSchemaApplyMessage();
                AssetDatabase.SaveAssets();
                RefreshSchemaCandidateCache();
            }
            if (GUILayout.Button("Apply Candidate", GUILayout.Width(120f)))
            {
                ApplyBestSchemaCandidate();
            }
            EditorGUILayout.EndHorizontal();

            if (_schema == null)
            {
                DrawSchemaCandidates(raw);
                DrawPreview(raw);
                EditorGUILayout.EndVertical();
                return;
            }

            bool canEditSchema = CanEditCurrentSchema();
            DrawSchemaApplyMessage();
            if (canEditSchema)
            {
                DrawSchemaFields();
            }
            else
            {
                DrawSchemaFieldsReadOnly();
            }
            DrawTemplateStatus();
            if (canEditSchema)
            {
                DrawColumns();
            }
            else
            {
                DrawColumnsReadOnly();
            }
            DrawValidation();
            DrawPreview(raw);
            EditorGUILayout.EndVertical();
        }

        private void DrawSchemaCandidates(RawExcelSheetSO raw)
        {
            if (_candidateCacheDirty)
            {
                EditorGUILayout.HelpBox("Candidate cache is stale. Validate or Refresh to update.", MessageType.Info);
            }

            if (_schemaCandidates.Count == 0)
            {
                EditorGUILayout.HelpBox("No schema candidate found. Create a new schema.", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Schema Candidates", EditorStyles.boldLabel);
            for (int i = 0; i < _schemaCandidates.Count; i++)
            {
                SchemaCandidateInfo candidate = _schemaCandidates[i];
                if (candidate.Schema == null)
                {
                    continue;
                }

                if (GUILayout.Button($"{candidate.Schema.name} ({ExcelSchemaAdapter.GetTemplateClassName(candidate.Schema)}) [{candidate.Kind}]"))
                {
                    _schema = ApplyCandidate(raw, candidate.Schema);
                }
            }
        }

        private void DrawSchemaFields()
        {
            EditorGUI.BeginChangeCheck();
            string templateClassName = string.IsNullOrWhiteSpace(_schema.templateClassName)
                ? _schema.className
                : _schema.templateClassName;
            templateClassName = EditorGUILayout.TextField("Template Class Name", templateClassName);
            _schema.templateClassName = CodeGenerator.ToTypeBaseName(templateClassName);
            _schema.className = _schema.templateClassName;
            _schema.outputAssetName = EditorGUILayout.TextField("Output Asset", _schema.outputAssetName);
            _schema.headerRowIndex = EditorGUILayout.IntField("Header Row", _schema.headerRowIndex);
            _schema.dataStartRowIndex = EditorGUILayout.IntField("Data Start Row", _schema.dataStartRowIndex);
            _schema.useDictionary = EditorGUILayout.Toggle("Use Dictionary", _schema.useDictionary);
            _schema.requireUniqueKey = EditorGUILayout.Toggle("Require Unique Key", _schema.requireUniqueKey);
            EditorGUI.BeginDisabledGroup(true);
            _schema.duplicateKeyPolicy = (DuplicateKeyPolicy)EditorGUILayout.EnumPopup("Duplicate Policy", _schema.duplicateKeyPolicy);
            EditorGUI.EndDisabledGroup();
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(_schema);
                MarkCandidateCacheDirty();
            }
        }

        private void DrawSchemaFieldsReadOnly()
        {
            EditorGUILayout.HelpBox(
                "Selected schema belongs to another Raw sheet or workbook. It will be cloned for this Raw when you Apply, Validate, or Generate. Create a Raw-specific schema copy before editing Template Class Name, Output Asset, or Columns.",
                MessageType.Info);

            EditorGUILayout.LabelField("Template Class Name", ExcelSchemaAdapter.GetTemplateClassName(_schema));
            EditorGUILayout.LabelField("Output Asset", ExcelSchemaAdapter.GetOutputAssetName(_schema));
            EditorGUILayout.LabelField("Header Row", _schema.headerRowIndex.ToString());
            EditorGUILayout.LabelField("Data Start Row", _schema.dataStartRowIndex.ToString());
            EditorGUILayout.LabelField("Use Dictionary", _schema.useDictionary.ToString());
            EditorGUILayout.LabelField("Require Unique Key", _schema.requireUniqueKey.ToString());
            EditorGUILayout.LabelField("Duplicate Policy", _schema.duplicateKeyPolicy.ToString());
        }

        private void DrawTemplateStatus()
        {
            if (_schema == null)
            {
                return;
            }

            if (_candidateCacheDirty)
            {
                EditorGUILayout.HelpBox("Template candidate state is stale. Validate or Refresh to update.", MessageType.Info);
            }

            string status = _templateReport == null ? "Unknown" : _templateReport.Status.ToString();
            EditorGUILayout.LabelField("Template Signature", status, EditorStyles.miniLabel);

            int sameStructureCount = 0;
            for (int i = 0; i < _schemaCandidates.Count; i++)
            {
                if (_schemaCandidates[i].Kind == SchemaCandidateKind.SameStructure)
                {
                    sameStructureCount++;
                }
            }
            EditorGUILayout.LabelField("Same Structure Candidates", sameStructureCount.ToString(), EditorStyles.miniLabel);

            if (_templateReport != null && _templateReport.Status == TemplateCompatibilityStatus.Mismatch)
            {
                string conflicts = string.Join(", ", _templateReport.ConflictingSchemas.ConvertAll(x => x.name));
                EditorGUILayout.HelpBox($"Template signature mismatch with: {conflicts}", MessageType.Error);
            }
            else if (sameStructureCount > 0)
            {
                EditorGUILayout.HelpBox(
                    "Same structure schema found. Apply candidate to share its Template Class Name, or keep current name to generate a separate script.",
                    MessageType.Info);
            }
        }

        private void DrawColumns()
        {
            const float FieldTypeWidth = 100f;
            const float DelimiterWidth = 110f;
            const float DelimiterColumnOffset = 20f + 50f + 44f + 130f + 150f + FieldTypeWidth;

            EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
            _columnScroll = EditorGUILayout.BeginScrollView(_columnScroll, GUI.skin.box, GUILayout.Height(210f));
            for (int i = 0; i < _schema.Columns.Count; i++)
            {
                ExcelColumnMapping column = _schema.Columns[i];
                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.BeginHorizontal();
                column.include = EditorGUILayout.Toggle(column.include, GUILayout.Width(20f));
                column.isKey = EditorGUILayout.ToggleLeft("Key", column.isKey, GUILayout.Width(50f));
                column.sourceColumnIndex = EditorGUILayout.IntField(column.sourceColumnIndex, GUILayout.Width(44f));
                EditorGUILayout.LabelField(column.sourceHeaderName, GUILayout.Width(130f));
                column.fieldName = EditorGUILayout.TextField(column.fieldName, GUILayout.Width(150f));
                column.fieldType = (ExcelSchemaFieldType)EditorGUILayout.EnumPopup(column.fieldType, GUILayout.Width(FieldTypeWidth));
                using (new EditorGUI.DisabledScope(column.fieldType != ExcelSchemaFieldType.ListString))
                {
                    column.listDelimiter = column.fieldType == ExcelSchemaFieldType.ListString
                        ? (ListDelimiter)EditorGUILayout.EnumPopup(column.listDelimiter, GUILayout.Width(DelimiterWidth))
                        : ListDelimiter.Comma;
                }
                column.defaultValue = EditorGUILayout.TextField(column.defaultValue);
                EditorGUILayout.EndHorizontal();
                if (column.fieldType == ExcelSchemaFieldType.ListString && column.listDelimiter == ListDelimiter.Backslash)
                {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(DelimiterColumnOffset);
                    EditorGUILayout.LabelField(@"Use \\ for literal \, \n for newline, \t for tab.", EditorStyles.miniLabel);
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(_schema);
                MarkCandidateCacheDirty();
            }
        }

        private void DrawColumnsReadOnly()
        {
            EditorGUILayout.LabelField("Columns", EditorStyles.boldLabel);
            _columnScroll = EditorGUILayout.BeginScrollView(_columnScroll, GUI.skin.box, GUILayout.Height(210f));
            for (int i = 0; i < _schema.Columns.Count; i++)
            {
                ExcelColumnMapping column = _schema.Columns[i];
                if (column == null)
                {
                    continue;
                }

                EditorGUILayout.BeginVertical(GUI.skin.box);
                EditorGUILayout.LabelField("Include", column.include.ToString());
                EditorGUILayout.LabelField("Key", column.isKey.ToString());
                EditorGUILayout.LabelField("Source Index", column.sourceColumnIndex.ToString());
                EditorGUILayout.LabelField("Source Header", column.sourceHeaderName);
                EditorGUILayout.LabelField("Field Name", column.fieldName);
                EditorGUILayout.LabelField("Field Type", column.fieldType.ToString());
                if (column.fieldType == ExcelSchemaFieldType.ListString)
                {
                    EditorGUILayout.LabelField("List Delimiter", column.listDelimiter.ToString());
                }
                EditorGUILayout.LabelField("Default Value", column.defaultValue ?? string.Empty);
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();
        }

        private void DrawValidation()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Validate", GUILayout.Height(26f)))
            {
                ValidateCurrent();
            }

            bool canGenerate = SelectedRaw != null && _schema != null && SelectedRaw.importStatus == RawImportStatus.Validated;
            using (new EditorGUI.DisabledScope(!canGenerate))
            {
                if (GUILayout.Button("Generate / Complete", GUILayout.Height(26f)))
                {
                    ExcelRawGeneratePipeline.StartGenerate(SelectedRaw, _schema);
                    RefreshRawList();
                }
            }

            if (GUILayout.Button("Delete Raw", GUILayout.Width(100f), GUILayout.Height(26f)))
            {
                DeleteSelectedRaw();
            }
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < _validationErrors.Count; i++)
            {
                EditorGUILayout.HelpBox(_validationErrors[i], MessageType.Error);
            }

            for (int i = 0; i < _validationWarnings.Count; i++)
            {
                EditorGUILayout.HelpBox(_validationWarnings[i], MessageType.Warning);
            }
        }

        private void DrawPreview(RawExcelSheetSO raw)
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);
            _previewScroll = EditorGUILayout.BeginScrollView(_previewScroll, GUI.skin.box, GUILayout.Height(120f));
            int rowCount = Mathf.Min(raw.Rows.Count, 12);
            for (int r = 0; r < rowCount; r++)
            {
                RawExcelRow row = raw.Rows[r];
                string text = row == null || row.Cells == null ? string.Empty : string.Join(" | ", row.Cells);
                EditorGUILayout.LabelField($"{r}: {text}", EditorStyles.miniLabel);
            }
            EditorGUILayout.EndScrollView();
        }

        private void ImportMarkerlessExcel()
        {
            string defaultFolder = ExcelRawImportUtility.ToAbsoluteAssetPath(ExcelImportPaths.DefaultExcelFolder);
            string path = EditorUtility.OpenFilePanel("Import markerless Excel", defaultFolder, "xlsx");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                List<RawExcelSheetSO> imported = ExcelRawImportUtility.ImportMarkerlessSheets(path);
                RefreshRawList();
                if (imported.Count > 0)
                {
                    _selectedRawIndex = Mathf.Max(0, _rawSheets.IndexOf(imported[0]));
                    LoadBestSchemaForSelectedRaw();
                }
                Debug.Log($"[RawSheetMapper] Imported {imported.Count} raw sheet(s) from {Path.GetFileName(path)}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[RawSheetMapper] Import failed: {ex.Message}\n{ex.StackTrace}");
            }
        }

        private void RefreshRawList()
        {
            _rawSheets.Clear();
            ExcelRawImportUtility.EnsureFolder(ExcelImportPaths.RawImportFolder);
            string[] guids = AssetDatabase.FindAssets("t:RawExcelSheetSO", new[] { ExcelImportPaths.RawImportFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RawExcelSheetSO raw = AssetDatabase.LoadAssetAtPath<RawExcelSheetSO>(path);
                if (raw != null)
                {
                    _rawSheets.Add(raw);
                }
            }

            _rawSheets.Sort((a, b) =>
            {
                int workbookCompare = string.Compare(a.workbookFileName, b.workbookFileName, System.StringComparison.Ordinal);
                return workbookCompare != 0
                    ? workbookCompare
                    : string.Compare(a.sheetName, b.sheetName, System.StringComparison.Ordinal);
            });

            _selectedRawIndex = Mathf.Clamp(_selectedRawIndex, 0, Mathf.Max(0, _rawSheets.Count - 1));
            LoadBestSchemaForSelectedRaw();
            Repaint();
        }

        private void LoadBestSchemaForSelectedRaw()
        {
            RawExcelSheetSO raw = SelectedRaw;
            _validationErrors.Clear();
            _validationWarnings.Clear();
            _schema = null;
            ClearSchemaApplyMessage();
            if (raw == null)
            {
                RefreshSchemaCandidateCache();
                return;
            }

            List<SchemaCandidateInfo> candidates = ExcelSchemaAdapter.FindSchemaCandidateInfos(raw);
            ExcelSheetSchemaSO reusableSchema = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Schema != null && ExcelSchemaAdapter.CanReuseSchemaForRaw(raw, candidates[i].Schema))
                {
                    reusableSchema = candidates[i].Schema;
                    break;
                }
            }

            if (reusableSchema != null)
            {
                _schema = reusableSchema;
                raw.importStatus = RawImportStatus.SchemaApplied;
                EditorUtility.SetDirty(raw);
            }

            UpdateSchemaCloneRequirementMessage();
            RefreshSchemaCandidateCache();
        }

        private void ApplyBestSchemaCandidate()
        {
            RawExcelSheetSO raw = SelectedRaw;
            if (raw == null)
            {
                return;
            }

            if (_candidateCacheDirty)
            {
                RefreshSchemaCandidateCache();
            }

            if (_schemaCandidates.Count == 0)
            {
                return;
            }

            for (int i = 0; i < _schemaCandidates.Count; i++)
            {
                if (_schemaCandidates[i].Schema != null)
                {
                    _schema = ApplyCandidate(raw, _schemaCandidates[i].Schema);
                    return;
                }
            }
        }

        private ExcelSheetSchemaSO ApplyCandidate(RawExcelSheetSO raw, ExcelSheetSchemaSO candidate)
        {
            if (raw == null || candidate == null)
            {
                return null;
            }

            bool cloned = ExcelSchemaAdapter.RequiresSchemaClone(raw, candidate);
            ExcelSheetSchemaSO schema = cloned
                ? ExcelSchemaAdapter.CreateSchemaFromTemplate(raw, candidate)
                : candidate;

            raw.importStatus = RawImportStatus.SchemaApplied;
            EditorUtility.SetDirty(raw);
            _schemaCloneRequired = false;
            _schemaApplyMessage = cloned
                ? "Schema was cloned for the current Raw sheet."
                : string.Empty;
            _schemaApplyMessageType = MessageType.Info;
            AssetDatabase.SaveAssets();
            RefreshSchemaCandidateCache();
            return schema;
        }

        private void ValidateCurrent()
        {
            _validationErrors.Clear();
            _validationWarnings.Clear();
            RawExcelSheetSO raw = SelectedRaw;
            bool cloned = ExcelSchemaAdapter.RequiresSchemaClone(raw, _schema);
            _schema = ExcelSchemaAdapter.EnsureSchemaForRaw(raw, _schema);
            if (cloned && _schema != null)
            {
                raw.importStatus = RawImportStatus.SchemaApplied;
                EditorUtility.SetDirty(raw);
                _schemaCloneRequired = false;
                _schemaApplyMessage = "Schema was cloned for the current Raw sheet before validation.";
                _schemaApplyMessageType = MessageType.Info;
            }

            if (ExcelSchemaAdapter.Validate(raw, _schema, out List<string> errors, out List<string> warnings))
            {
                _validationWarnings.AddRange(warnings);
                Debug.Log("[RawSheetMapper] Validation succeeded.");
            }
            else
            {
                _validationErrors.AddRange(errors);
                _validationWarnings.AddRange(warnings);
            }

            AssetDatabase.SaveAssets();
            RefreshSchemaCandidateCache();
        }

        private void RefreshSchemaCandidateCache()
        {
            _schemaCandidates.Clear();
            RawExcelSheetSO raw = SelectedRaw;
            if (raw != null)
            {
                List<SchemaCandidateInfo> candidates = ExcelSchemaAdapter.FindSchemaCandidateInfos(raw, _schema);
                for (int i = 0; i < candidates.Count; i++)
                {
                    if (candidates[i].Schema != null)
                    {
                        _schemaCandidates.Add(candidates[i]);
                    }
                }
            }

            _templateReport = _schema == null ? null : ExcelSchemaAdapter.GetTemplateCompatibilityReport(_schema);
            _candidateCacheDirty = false;
        }

        private bool CanEditCurrentSchema()
        {
            RawExcelSheetSO raw = SelectedRaw;
            return raw != null &&
                   _schema != null &&
                   ExcelSchemaAdapter.CanReuseSchemaForRaw(raw, _schema);
        }

        private void UpdateSchemaCloneRequirementMessage()
        {
            _schemaCloneRequired = ExcelSchemaAdapter.RequiresSchemaClone(SelectedRaw, _schema);
            if (_schemaCloneRequired)
            {
                _schemaApplyMessage = "Selected schema belongs to another Raw sheet or workbook. It will be cloned for this Raw when you Apply, Validate, or Generate.";
                _schemaApplyMessageType = MessageType.Info;
            }
            else
            {
                ClearSchemaApplyMessage();
            }
        }

        private void DrawSchemaApplyMessage()
        {
            if (!string.IsNullOrEmpty(_schemaApplyMessage))
            {
                EditorGUILayout.HelpBox(_schemaApplyMessage, _schemaApplyMessageType);
            }
        }

        private void ClearSchemaApplyMessage()
        {
            _schemaCloneRequired = false;
            _schemaApplyMessage = string.Empty;
            _schemaApplyMessageType = MessageType.Info;
        }

        private void MarkCandidateCacheDirty()
        {
            _candidateCacheDirty = true;
        }

        private void DeleteSelectedRaw()
        {
            RawExcelSheetSO raw = SelectedRaw;
            if (raw == null)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(raw);
            if (EditorUtility.DisplayDialog("Delete Raw SO", $"Delete temporary Raw SO?\n{path}", "Delete", "Cancel"))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                RefreshRawList();
            }
        }

        private RawExcelSheetSO SelectedRaw
        {
            get
            {
                if (_rawSheets.Count == 0 || _selectedRawIndex < 0 || _selectedRawIndex >= _rawSheets.Count)
                {
                    return null;
                }

                return _rawSheets[_selectedRawIndex];
            }
        }
    }
}
