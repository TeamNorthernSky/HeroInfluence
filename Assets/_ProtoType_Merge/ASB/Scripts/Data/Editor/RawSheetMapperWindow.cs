using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    public sealed class RawSheetMapperWindow : EditorWindow
    {
        private const string AllWorkbookFolders = "All Workbooks";

        private readonly List<RawExcelSheetSO> _allRawSheets = new List<RawExcelSheetSO>();
        private readonly List<RawExcelSheetSO> _rawSheets = new List<RawExcelSheetSO>();
        private readonly List<string> _workbookFolders = new List<string>();
        private readonly List<ExcelSheetSchemaSO> _duplicateSchemas = new List<ExcelSheetSchemaSO>();
        private readonly List<ExcelSheetSchemaSO> _templateCandidates = new List<ExcelSheetSchemaSO>();
        private readonly List<ExcelSheetSchemaSO> _relinkCandidates = new List<ExcelSheetSchemaSO>();
        private readonly List<RawExcelSheetSO> _logicalKeyCollisions = new List<RawExcelSheetSO>();
        private readonly List<string> _validationErrors = new List<string>();
        private readonly List<string> _validationWarnings = new List<string>();
        private int _selectedWorkbookFolderIndex;
        private int _selectedRawIndex;
        private ExcelSheetSchemaSO _schema;
        private TemplateCompatibilityReport _templateReport;
        private bool _candidateCacheDirty = true;
        private bool _showTemplates;
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

            EditorGUI.BeginChangeCheck();
            _selectedWorkbookFolderIndex = EditorGUILayout.Popup(
                _selectedWorkbookFolderIndex,
                _workbookFolders.ToArray());
            if (EditorGUI.EndChangeCheck())
            {
                _selectedWorkbookFolderIndex = Mathf.Clamp(_selectedWorkbookFolderIndex, 0, Mathf.Max(0, _workbookFolders.Count - 1));
                ApplyWorkbookFolderFilter();
                _selectedRawIndex = 0;
                LoadBestSchemaForSelectedRaw();
            }

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

            if (_candidateCacheDirty)
            {
                RefreshSchemaCandidateCache();
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
                MarkCandidateCacheDirty();
            }

            using (new EditorGUI.DisabledScope(_schema != null))
            {
                if (GUILayout.Button("New Schema", GUILayout.Width(100f)))
                {
                    CreateNewSchemaForRaw(raw);
                }
            }

            using (new EditorGUI.DisabledScope(_schema == null))
            {
                if (GUILayout.Button("Rebuild", GUILayout.Width(80f)))
                {
                    RebuildSchemaForRaw(raw);
                }

                if (GUILayout.Button("Recreate", GUILayout.Width(90f)))
                {
                    RecreateSchemaForRaw(raw);
                }
            }
            EditorGUILayout.EndHorizontal();

            DrawDuplicateSchemaWarning();
            DrawLogicalKeyCollisionWarning();
            DrawRelinkSection(raw);

            if (_schema == null)
            {
                EditorGUILayout.HelpBox("No schema for this sheet. Create one, or copy from a template.", MessageType.Info);
                DrawTemplateSection(raw);
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

        private void DrawDuplicateSchemaWarning()
        {
            if (_duplicateSchemas.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                $"{_duplicateSchemas.Count} duplicate schema(s) match this sheet. The canonical one is selected. Remove extras (auto-delete is disabled).",
                MessageType.Warning);
            for (int i = 0; i < _duplicateSchemas.Count; i++)
            {
                ExcelSheetSchemaSO dup = _duplicateSchemas[i];
                if (dup == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(dup.name, EditorStyles.miniLabel);
                if (GUILayout.Button("Make Canonical", GUILayout.Width(120f)))
                {
                    _schema = dup;
                    MarkCandidateCacheDirty();
                }

                if (GUILayout.Button("Delete", GUILayout.Width(60f)))
                {
                    DeleteDuplicateSchema(dup);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DeleteDuplicateSchema(ExcelSheetSchemaSO schema)
        {
            string path = AssetDatabase.GetAssetPath(schema);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            if (EditorUtility.DisplayDialog("Delete Duplicate Schema", $"Delete duplicate schema?\n{path}", "Delete", "Cancel"))
            {
                if (_schema == schema)
                {
                    _schema = null;
                }

                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                LoadBestSchemaForSelectedRaw();
            }
        }

        private void DrawLogicalKeyCollisionWarning()
        {
            if (_logicalKeyCollisions.Count == 0)
            {
                return;
            }

            var names = new List<string>();
            for (int i = 0; i < _logicalKeyCollisions.Count; i++)
            {
                if (_logicalKeyCollisions[i] != null)
                {
                    names.Add(_logicalKeyCollisions[i].sheetName);
                }
            }

            EditorGUILayout.HelpBox(
                "Logical key collision: other sheet(s) in the same workbook normalize to the same logical key " +
                $"({string.Join(", ", names)}). Identity uses physicalSheetId so they stay separate, but consider renaming to avoid confusion.",
                MessageType.Warning);
        }

        private void DrawRelinkSection(RawExcelSheetSO raw)
        {
            if (_relinkCandidates.Count == 0)
            {
                return;
            }

            EditorGUILayout.HelpBox(
                "Possible renamed sheet: schema(s) with the same workbook and logical key but a different physical id. " +
                "Relink only if this sheet was renamed (manual).",
                MessageType.Info);
            for (int i = 0; i < _relinkCandidates.Count; i++)
            {
                ExcelSheetSchemaSO candidate = _relinkCandidates[i];
                if (candidate == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{candidate.name} (was '{candidate.sourceSheetName}')", EditorStyles.miniLabel);
                if (GUILayout.Button("Relink", GUILayout.Width(70f)))
                {
                    RelinkSchema(raw, candidate);
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void DrawTemplateSection(RawExcelSheetSO raw)
        {
            EditorGUILayout.Space(4f);
            _showTemplates = EditorGUILayout.Foldout(_showTemplates, "Copy from Template (explicit)", true);
            if (!_showTemplates)
            {
                return;
            }

            if (_templateCandidates.Count == 0)
            {
                EditorGUILayout.HelpBox("No other schema available as a template.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                "Templates are NOT auto-applied. Copy creates a NEW schema for THIS sheet from the chosen template.",
                MessageType.Info);
            for (int i = 0; i < _templateCandidates.Count; i++)
            {
                ExcelSheetSchemaSO template = _templateCandidates[i];
                if (template == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{template.name} ({ExcelSchemaAdapter.GetTemplateClassName(template)})", EditorStyles.miniLabel);
                using (new EditorGUI.DisabledScope(_schema != null))
                {
                    if (GUILayout.Button("Copy as Template", GUILayout.Width(140f)))
                    {
                        CopyFromTemplate(raw, template);
                    }
                }
                EditorGUILayout.EndHorizontal();
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
                "Selected schema does not belong to this Raw sheet (identity mismatch). Assign the matching schema, " +
                "create a new one, or relink if the sheet was renamed. Auto-clone is disabled.",
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

            string status = _templateReport == null ? "Unknown" : _templateReport.Status.ToString();
            EditorGUILayout.LabelField("Template Signature", status, EditorStyles.miniLabel);

            if (_templateReport != null && _templateReport.Status == TemplateCompatibilityStatus.Mismatch)
            {
                string conflicts = string.Join(", ", _templateReport.ConflictingSchemas.ConvertAll(x => x.name));
                EditorGUILayout.HelpBox($"Template signature mismatch with: {conflicts}", MessageType.Error);
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

            bool canUpdateData = SelectedRaw != null && _schema != null;
            using (new EditorGUI.DisabledScope(!canUpdateData))
            {
                if (GUILayout.Button("Update Data", GUILayout.Height(26f)))
                {
                    UpdateDataCurrent();
                }
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
                RawExcelSheetSO firstImported = imported.Count > 0 ? imported[0] : null;
                RefreshRawList();
                if (firstImported != null)
                {
                    SelectWorkbookFolderForRaw(firstImported);
                    ApplyWorkbookFolderFilter();
                    _selectedRawIndex = Mathf.Max(0, _rawSheets.IndexOf(firstImported));
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
            RawExcelSheetSO previousRaw = SelectedRaw;
            string previousFolder = SelectedWorkbookFolder;

            _allRawSheets.Clear();
            _rawSheets.Clear();
            ExcelRawImportUtility.EnsureFolder(ExcelImportPaths.RawImportFolder);
            string[] guids = AssetDatabase.FindAssets("t:RawExcelSheetSO", new[] { ExcelImportPaths.RawImportFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                RawExcelSheetSO raw = AssetDatabase.LoadAssetAtPath<RawExcelSheetSO>(path);
                if (raw != null)
                {
                    _allRawSheets.Add(raw);
                }
            }

            _allRawSheets.Sort((a, b) =>
            {
                int workbookCompare = string.Compare(a.workbookFileName, b.workbookFileName, System.StringComparison.Ordinal);
                return workbookCompare != 0
                    ? workbookCompare
                    : string.Compare(a.sheetName, b.sheetName, System.StringComparison.Ordinal);
            });

            RebuildWorkbookFolders(previousFolder);
            ApplyWorkbookFolderFilter();

            if (previousRaw != null)
            {
                int previousIndex = _rawSheets.IndexOf(previousRaw);
                if (previousIndex >= 0)
                {
                    _selectedRawIndex = previousIndex;
                }
            }

            _selectedRawIndex = Mathf.Clamp(_selectedRawIndex, 0, Mathf.Max(0, _rawSheets.Count - 1));
            LoadBestSchemaForSelectedRaw();
            Repaint();
        }

        private void RebuildWorkbookFolders(string preferredFolder)
        {
            _workbookFolders.Clear();
            _workbookFolders.Add(AllWorkbookFolders);

            for (int i = 0; i < _allRawSheets.Count; i++)
            {
                string folder = GetRawWorkbookFolderName(_allRawSheets[i]);
                if (!string.IsNullOrEmpty(folder) && !_workbookFolders.Contains(folder))
                {
                    _workbookFolders.Add(folder);
                }
            }

            _workbookFolders.Sort(1, _workbookFolders.Count - 1, System.StringComparer.Ordinal);

            int preferredIndex = string.IsNullOrEmpty(preferredFolder)
                ? -1
                : _workbookFolders.IndexOf(preferredFolder);
            if (preferredIndex >= 0)
            {
                _selectedWorkbookFolderIndex = preferredIndex;
            }
            else
            {
                _selectedWorkbookFolderIndex = Mathf.Clamp(_selectedWorkbookFolderIndex, 0, Mathf.Max(0, _workbookFolders.Count - 1));
            }
        }

        private void ApplyWorkbookFolderFilter()
        {
            _rawSheets.Clear();
            string selectedFolder = SelectedWorkbookFolder;
            bool showAll = string.IsNullOrEmpty(selectedFolder) ||
                           string.Equals(selectedFolder, AllWorkbookFolders, System.StringComparison.Ordinal);

            for (int i = 0; i < _allRawSheets.Count; i++)
            {
                RawExcelSheetSO raw = _allRawSheets[i];
                if (raw == null)
                {
                    continue;
                }

                if (showAll || string.Equals(GetRawWorkbookFolderName(raw), selectedFolder, System.StringComparison.Ordinal))
                {
                    _rawSheets.Add(raw);
                }
            }
        }

        private void SelectWorkbookFolderForRaw(RawExcelSheetSO raw)
        {
            string folder = GetRawWorkbookFolderName(raw);
            int index = string.IsNullOrEmpty(folder) ? -1 : _workbookFolders.IndexOf(folder);
            if (index >= 0)
            {
                _selectedWorkbookFolderIndex = index;
            }
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

            ExcelSheetSchemaSO existing = ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out _);
            if (existing != null)
            {
                _schema = existing;
                raw.importStatus = RawImportStatus.SchemaApplied;
                EditorUtility.SetDirty(raw);
            }

            RefreshSchemaCandidateCache();
        }

        private void CreateNewSchemaForRaw(RawExcelSheetSO raw)
        {
            if (raw == null)
            {
                return;
            }

            // 안전장치: 이미 스키마가 있으면 절대 새로 만들지 않는다.
            ExcelSheetSchemaSO existing = ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out _);
            if (existing != null)
            {
                _schema = existing;
                SetSchemaApplyMessage("A schema already exists for this sheet. Auto-selected it. Use Rebuild or Recreate.", MessageType.Warning);
                RefreshSchemaCandidateCache();
                return;
            }

            _schema = ExcelSchemaAdapter.CreateDefaultSchema(raw);
            raw.importStatus = RawImportStatus.SchemaApplied;
            EditorUtility.SetDirty(raw);
            ClearSchemaApplyMessage();
            AssetDatabase.SaveAssets();
            RefreshSchemaCandidateCache();
        }

        private void RebuildSchemaForRaw(RawExcelSheetSO raw)
        {
            if (raw == null || _schema == null)
            {
                return;
            }

            if (!ExcelSchemaAdapter.SchemaMatchesRaw(raw, _schema))
            {
                SetSchemaApplyMessage("Current schema does not belong to this raw sheet.", MessageType.Error);
                return;
            }

            ExcelSchemaAdapter.ReconcileSchemaColumns(raw, _schema, false, out List<string> added, out List<string> disabled);
            string message =
                "Rebuild reconciles columns with the current raw sheet.\n\n" +
                $"Added: {(added.Count == 0 ? "(none)" : string.Join(", ", added))}\n" +
                $"Disabled (missing in raw, kept as include=false): {(disabled.Count == 0 ? "(none)" : string.Join(", ", disabled))}\n\n" +
                "Existing column settings are preserved. No column is deleted. Continue?";
            if (!EditorUtility.DisplayDialog("Rebuild Schema", message, "Rebuild", "Cancel"))
            {
                return;
            }

            ExcelSchemaAdapter.ReconcileSchemaColumns(raw, _schema, true, out _, out _);
            SetSchemaApplyMessage("Schema columns rebuilt (merge). schemaGuid preserved.", MessageType.Info);
            MarkCandidateCacheDirty();
        }

        private void RecreateSchemaForRaw(RawExcelSheetSO raw)
        {
            if (raw == null || _schema == null)
            {
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Recreate Schema",
                "This DELETES the current schema and creates a fresh one from the raw sheet.\n" +
                "All manual column edits are lost, and the schemaGuid changes (generated DataTable links may break).\n\n" +
                "Prefer Rebuild unless the schema is corrupted. Continue?",
                "Recreate", "Cancel");
            if (!ok)
            {
                return;
            }

            string path = AssetDatabase.GetAssetPath(_schema);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            _schema = ExcelSchemaAdapter.CreateDefaultSchema(raw);
            raw.importStatus = RawImportStatus.SchemaApplied;
            EditorUtility.SetDirty(raw);
            AssetDatabase.SaveAssets();
            SetSchemaApplyMessage("Schema recreated from raw sheet.", MessageType.Info);
            RefreshSchemaCandidateCache();
        }

        private void CopyFromTemplate(RawExcelSheetSO raw, ExcelSheetSchemaSO template)
        {
            if (raw == null || template == null)
            {
                return;
            }

            ExcelSheetSchemaSO existing = ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out _);
            if (existing != null)
            {
                _schema = existing;
                SetSchemaApplyMessage("A schema already exists for this sheet. Copy is disabled.", MessageType.Warning);
                RefreshSchemaCandidateCache();
                return;
            }

            _schema = ExcelSchemaAdapter.CreateSchemaFromTemplate(raw, template);
            raw.importStatus = RawImportStatus.SchemaApplied;
            EditorUtility.SetDirty(raw);
            AssetDatabase.SaveAssets();
            SetSchemaApplyMessage($"New schema created for this sheet from template '{template.name}'.", MessageType.Info);
            RefreshSchemaCandidateCache();
        }

        private void RelinkSchema(RawExcelSheetSO raw, ExcelSheetSchemaSO candidate)
        {
            if (raw == null || candidate == null)
            {
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Relink Schema",
                $"Relink schema '{candidate.name}' (was sheet '{candidate.sourceSheetName}') to this sheet '{raw.sheetName}'?\n" +
                "Use this only if the sheet was renamed. schemaGuid is preserved.",
                "Relink", "Cancel");
            if (!ok)
            {
                return;
            }

            ExcelSchemaAdapter.RelinkSchemaToRaw(raw, candidate);
            _schema = candidate;
            raw.importStatus = RawImportStatus.SchemaApplied;
            EditorUtility.SetDirty(raw);
            AssetDatabase.SaveAssets();
            SetSchemaApplyMessage("Schema relinked to this (renamed) sheet.", MessageType.Info);
            RefreshSchemaCandidateCache();
        }

        private void ValidateCurrent()
        {
            _validationErrors.Clear();
            _validationWarnings.Clear();
            RawExcelSheetSO raw = SelectedRaw;
            _schema = ExcelSchemaAdapter.ResolveExistingSchemaForRaw(raw, _schema);
            if (_schema == null)
            {
                _validationErrors.Add("No schema exists for this raw sheet. Create one first.");
                RefreshSchemaCandidateCache();
                return;
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

        private void UpdateDataCurrent()
        {
            _validationErrors.Clear();
            _validationWarnings.Clear();
            RawExcelSheetSO raw = SelectedRaw;
            if (!ExcelRawGeneratePipeline.UpdateData(raw, _schema, out ExcelSheetSchemaSO usedSchema, out string error))
            {
                if (usedSchema != null && usedSchema != _schema)
                {
                    _schema = usedSchema;
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    _validationErrors.Add(error);
                    SetSchemaApplyMessage(error, MessageType.Error);
                    EditorUtility.DisplayDialog("Update Data Failed", error, "OK");
                }

                RefreshSchemaCandidateCache();
                return;
            }

            if (usedSchema != null)
            {
                _schema = usedSchema;
            }

            SetSchemaApplyMessage("Data updated.", MessageType.Info);
            Debug.Log("[RawSheetMapper] Data updated.");
            RefreshSchemaCandidateCache();
        }

        private void RefreshSchemaCandidateCache()
        {
            _duplicateSchemas.Clear();
            _templateCandidates.Clear();
            _relinkCandidates.Clear();
            _logicalKeyCollisions.Clear();

            RawExcelSheetSO raw = SelectedRaw;
            if (raw != null)
            {
                ExcelSchemaAdapter.FindExistingSchemaForRaw(raw, out List<ExcelSheetSchemaSO> dups);
                _duplicateSchemas.AddRange(dups);
                _templateCandidates.AddRange(ExcelSchemaAdapter.FindTemplateCandidates(raw));
                _relinkCandidates.AddRange(ExcelSchemaAdapter.FindRelinkCandidates(raw));
                _logicalKeyCollisions.AddRange(ExcelSchemaAdapter.FindLogicalKeyCollisions(raw));
            }

            _templateReport = _schema == null ? null : ExcelSchemaAdapter.GetTemplateCompatibilityReport(_schema);
            _candidateCacheDirty = false;
        }

        private bool CanEditCurrentSchema()
        {
            RawExcelSheetSO raw = SelectedRaw;
            return raw != null &&
                   _schema != null &&
                   ExcelSchemaAdapter.SchemaMatchesRaw(raw, _schema);
        }

        private void DrawSchemaApplyMessage()
        {
            if (!string.IsNullOrEmpty(_schemaApplyMessage))
            {
                EditorGUILayout.HelpBox(_schemaApplyMessage, _schemaApplyMessageType);
            }
        }

        private void SetSchemaApplyMessage(string message, MessageType type)
        {
            _schemaApplyMessage = message;
            _schemaApplyMessageType = type;
        }

        private void ClearSchemaApplyMessage()
        {
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

        private string SelectedWorkbookFolder
        {
            get
            {
                if (_workbookFolders.Count == 0 ||
                    _selectedWorkbookFolderIndex < 0 ||
                    _selectedWorkbookFolderIndex >= _workbookFolders.Count)
                {
                    return AllWorkbookFolders;
                }

                return _workbookFolders[_selectedWorkbookFolderIndex];
            }
        }

        private static string GetRawWorkbookFolderName(RawExcelSheetSO raw)
        {
            if (raw == null)
            {
                return string.Empty;
            }

            string path = AssetDatabase.GetAssetPath(raw).Replace('\\', '/');
            string root = ExcelImportPaths.RawImportFolder.TrimEnd('/') + "/";
            if (path.StartsWith(root, System.StringComparison.OrdinalIgnoreCase))
            {
                string rest = path.Substring(root.Length);
                int slash = rest.IndexOf('/');
                if (slash >= 0)
                {
                    return rest.Substring(0, slash);
                }
            }

            return raw.workbookFileName ?? string.Empty;
        }
    }
}
