using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ASB.ExcelImport.Editor
{
    public class ExcelDataViewerWindow : EditorWindow
    {
        private const string ExcelPrefKey = "ASBDataViewer_ExcelFile";

        private readonly List<ScriptableObject> _assets     = new List<ScriptableObject>();
        private readonly List<string>           _excelPaths = new List<string>();

        private readonly HashSet<ScriptableObject> _dirtyAssets =
            new HashSet<ScriptableObject>();

        // BindProperty媛 userData瑜???뼱?곕?濡?寃쎈줈??蹂꾨룄 留듭뿉 蹂닿?

        // bindCell?먯꽌 BindProperty ?ъ뿰寃???ChangeEvent ?ㅻ컻 諛⑹?
        private bool _isBinding = false;

        private ScriptableObject _selected;
        private string           _selectedExcelPath;

        private ListView      _listView;
        private Label         _emptyLabel;
        private VisualElement _rightPanel;
        private ToolbarButton _saveButton;
        private DropdownField _excelDropdown;

        [MenuItem("Tools/ASB/Excel Data Viewer")]
        public static void ShowWindow()
        {
            ExcelDataViewerWindow window = GetWindow<ExcelDataViewerWindow>("Excel Data Viewer");
            window.minSize = new Vector2(700, 450);
            window.Show();
        }

        private void CreateGUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            BuildToolbar();
            BuildSplitView();
            ScanExcelFolder();
        }

        // ??? Toolbar ??????????????????????????????????????????????????????????

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();

            var importBtn = new ToolbarButton(ExcelEditorWindow.ShowWindow) { text = "?뱿 Excel Importer ?닿린" };
            toolbar.Add(importBtn);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            _saveButton = new ToolbarButton(OnSaveToExcel) { text = "?뮶 Save to Excel" };
            _saveButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            _saveButton.SetEnabled(false);
            toolbar.Add(_saveButton);

            rootVisualElement.Add(toolbar);
        }

        // ??? Split View ???????????????????????????????????????????????????????

        private void BuildSplitView()
        {
            var splitView = new TwoPaneSplitView(0, 250f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildLeftPanel());
            splitView.Add(BuildRightPanel());
            rootVisualElement.Add(splitView);
        }

        // ??? Left Panel ???????????????????????????????????????????????????????

        private VisualElement BuildLeftPanel()
        {
            var panel = new VisualElement();
            panel.style.paddingTop    = 4f;
            panel.style.paddingBottom = 4f;
            panel.style.paddingLeft   = 4f;
            panel.style.paddingRight  = 4f;
            panel.style.flexDirection = FlexDirection.Column;

            var dropLabel = new Label("Excel ?뚯씪");
            dropLabel.style.marginBottom = 2f;
            panel.Add(dropLabel);

            _excelDropdown = new DropdownField(new List<string>(), 0);
            _excelDropdown.style.marginBottom = 4f;
            _excelDropdown.RegisterValueChangedCallback(_ => OnExcelFileSelected());
            panel.Add(_excelDropdown);

            var refreshBtn = new Button(OnRefreshClicked) { text = "Refresh" };
            refreshBtn.style.marginBottom = 4f;
            panel.Add(refreshBtn);

            _emptyLabel = new Label("No ScriptableObject found.");
            _emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            _emptyLabel.style.marginTop      = 8f;
            _emptyLabel.style.display        = DisplayStyle.None;
            panel.Add(_emptyLabel);

            _listView = new ListView(_assets, 22, () => new Label(), BindListItem);
            _listView.selectionType    = SelectionType.Single;
            _listView.style.flexGrow   = 1f;
            _listView.selectionChanged += OnSelectionChanged;
            panel.Add(_listView);

            return panel;
        }

        private void BindListItem(VisualElement element, int index)
        {
            if (element is Label label && index >= 0 && index < _assets.Count)
                label.text = _assets[index] != null ? _assets[index].name : "(null)";
        }

        // ??? Right Panel ??????????????????????????????????????????????????????

        private VisualElement BuildRightPanel()
        {
            _rightPanel = new VisualElement();
            _rightPanel.style.paddingTop    = 4f;
            _rightPanel.style.paddingBottom = 4f;
            _rightPanel.style.paddingLeft   = 4f;
            _rightPanel.style.paddingRight  = 4f;
            _rightPanel.style.flexGrow      = 1f;
            ShowRightEmpty();
            return _rightPanel;
        }

        private void ShowRightEmpty()
        {
            _rightPanel.Clear();
            var label = new Label("Select an asset to inspect.");
            label.style.flexGrow       = 1f;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            _rightPanel.Add(label);
        }

        // ??? Grid View ????????????????????????????????????????????????????????

        private struct ColumnMeta
        {
            public string                 fieldName;
            public SerializedPropertyType propType;
            public Type                   fieldType;
        }

        private void ShowInspector(ScriptableObject asset)
        {
            var so = new SerializedObject(asset);

            SerializedProperty listProp = null;
            SerializedProperty iter     = so.GetIterator();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (iter.isArray) { listProp = iter.Copy(); break; }
                }
                while (iter.NextVisible(false));
            }

            if (listProp == null) FallbackInspector(so);
            else                  BuildGridView(so, listProp);
        }

        private void FallbackInspector(SerializedObject so)
        {
            _rightPanel.Clear();
            var scroll = new ScrollView(ScrollViewMode.Vertical);
            scroll.style.flexGrow = 1f;

            SerializedProperty iter = so.GetIterator();
            if (iter.NextVisible(true))
            {
                do
                {
                    if (iter.propertyPath == "m_Script") continue;
                    var field = new PropertyField(iter.Copy());
                    field.Bind(so);
                    scroll.Add(field);
                }
                while (iter.NextVisible(false));
            }

            scroll.TrackSerializedObjectValue(so, s => s.ApplyModifiedProperties());
            _rightPanel.Add(scroll);
        }

        private void BuildGridView(SerializedObject so, SerializedProperty listProp)
        {
            List<ColumnMeta> columns = CollectColumnMeta(so, listProp);

            var grid = new MultiColumnListView
            {
                fixedItemHeight               = 22f,
                showBorder                    = true,
                showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly
            };
            grid.style.flexGrow  = 1f;
            grid.itemsSource     = BuildIndexList(listProp.arraySize);

            string propPath = listProp.propertyPath;

            for (int c = 0; c < columns.Count; c++)
            {
                ColumnMeta            col       = columns[c];
                string                fieldName = col.fieldName;
                SerializedPropertyType propType  = col.propType;
                bool isList = col.fieldType != null
                    && col.fieldType.IsGenericType
                    && col.fieldType.GetGenericTypeDefinition() == typeof(List<>);

                var column = new Column
                {
                    title     = fieldName,
                    width     = GetColumnWidth(col),
                    resizable = true
                };

                // so.targetObject瑜?罹≪쿂 (_selected???댄썑 蹂寃쎈맆 ???덉쑝誘濡??ъ슜 湲덉?)
                ScriptableObject capturedAsset = so.targetObject as ScriptableObject;

                column.makeCell = () =>
                {
                    if (isList)
                    {
                        // List ??낆? bindCell?먯꽌 肄쒕갚 ?깅줉/?댁젣 愿由?
                        return MakeGridCell(new TextField());
                    }

                    switch (propType)
                    {
                        case SerializedPropertyType.Integer:
                        {
                            var f = new IntegerField();
                            f.RegisterValueChangedCallback(evt =>
                            {
                                if (_isBinding) return;
                                if (EqualityComparer<int>.Default.Equals(evt.previousValue, evt.newValue)) return;
                                MarkDirty(capturedAsset);
                            });
                            return MakeGridCell(f);
                        }
                        case SerializedPropertyType.Float:
                        {
                            var f = new FloatField();
                            f.RegisterValueChangedCallback(evt =>
                            {
                                if (_isBinding) return;
                                if (EqualityComparer<float>.Default.Equals(evt.previousValue, evt.newValue)) return;
                                MarkDirty(capturedAsset);
                            });
                            return MakeGridCell(f);
                        }
                        case SerializedPropertyType.Boolean:
                        {
                            var f = new Toggle();
                            f.RegisterValueChangedCallback(evt =>
                            {
                                if (_isBinding) return;
                                if (EqualityComparer<bool>.Default.Equals(evt.previousValue, evt.newValue)) return;
                                MarkDirty(capturedAsset);
                            });
                            return MakeGridCell(f);
                        }
                        default:
                        {
                            var f = new TextField();
                            f.RegisterValueChangedCallback(evt =>
                            {
                                if (_isBinding) return;
                                if (EqualityComparer<string>.Default.Equals(evt.previousValue, evt.newValue)) return;
                                MarkDirty(capturedAsset);
                            });
                            return MakeGridCell(f);
                        }
                    }
                };

                column.bindCell = (element, rowIndex) =>
                {
                    so.Update();
                    string             elemPath = $"{propPath}.Array.data[{rowIndex}].{fieldName}";
                    SerializedProperty prop     = so.FindProperty(elemPath);
                    if (prop == null) return;

                    if (isList)
                    {
                        var tf = element as TextField;
                        if (tf == null) return;

                        var oldCb = tf.userData as EventCallback<ChangeEvent<string>>;
                        if (oldCb != null) tf.UnregisterValueChangedCallback(oldCb);

                        tf.SetValueWithoutNotify(SerializeListProperty(prop));

                        EventCallback<ChangeEvent<string>> cb = evt =>
                        {
                            if (EqualityComparer<string>.Default.Equals(evt.previousValue, evt.newValue)) return;
                            Undo.RecordObject(capturedAsset, "Edit Excel Data");
                            DeserializeListProperty(prop, evt.newValue);
                            so.ApplyModifiedProperties();
                            MarkDirty(capturedAsset);
                        };
                        tf.userData = cb;
                        tf.RegisterValueChangedCallback(cb);
                        return;
                    }

                    // 鍮?List ??? BindProperty ?꾪썑瑜?_isBinding?쇰줈 媛먯떥 ?ㅻ컻 諛⑹?
                    // userData??BindProperty媛 ??뼱?????덉쑝誘濡?蹂꾨룄 留듭뿉 寃쎈줈 蹂닿?
                    _isBinding                = true;

                    switch (propType)
                    {
                        case SerializedPropertyType.Integer: ((IntegerField)element).BindProperty(prop); break;
                        case SerializedPropertyType.Float:   ((FloatField)element).BindProperty(prop);   break;
                        case SerializedPropertyType.Boolean: ((Toggle)element).BindProperty(prop);       break;
                        default:                             ((TextField)element).BindProperty(prop);    break;
                    }

                    // BindProperty媛 ?숆린 ?대깽?몃? 諛쒖깮?쒗궗 ???덉쑝誘濡??ㅼ쓬 ?꾨젅?꾩뿉 ?댁젣
                    element.schedule.Execute(() => _isBinding = false);
                };

                grid.columns.Add(column);
            }

            _rightPanel.Clear();
            _rightPanel.Add(grid);

            grid.TrackSerializedObjectValue(so, s =>
            {
                s.Update();
                grid.itemsSource = BuildIndexList(listProp.arraySize);
                grid.RefreshItems();
            });
        }

        private static VisualElement MakeGridCell(VisualElement field)
        {
            field.style.borderTopWidth    = 0;
            field.style.borderBottomWidth = 0;
            field.style.borderLeftWidth   = 0;
            field.style.borderRightWidth  = 0;
            field.style.backgroundColor   = Color.clear;
            return field;
        }

        // ??? Dirty Tracking ???????????????????????????????????????????????????

        private void MarkDirty(ScriptableObject asset)
        {
            if (asset == null) return;
            _dirtyAssets.Add(asset);
            EditorUtility.SetDirty(asset);
        }

        // ?낅젰 ?덉떆: "DataList.Array.data[2].EnemyHp"
        // 異쒕젰:      rowIndex = 2, fieldName = "EnemyHp"
        private static List<ColumnMeta> CollectColumnMeta(SerializedObject so, SerializedProperty listProp)
        {
            var columns = new List<ColumnMeta>();

            Type rowType = ResolveElementType(so.targetObject, listProp.name);
            if (rowType == null) return columns;

            FieldInfo[] fields = rowType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < fields.Length; i++)
            {
                columns.Add(new ColumnMeta
                {
                    fieldName = fields[i].Name,
                    propType  = ToPropType(fields[i].FieldType),
                    fieldType = fields[i].FieldType
                });
            }
            return columns;
        }

        private static Type ResolveElementType(UnityEngine.Object target, string listFieldName)
        {
            if (target == null) return null;
            FieldInfo f = target.GetType().GetField(listFieldName, BindingFlags.Instance | BindingFlags.Public)
                       ?? target.GetType().GetField("DataList",    BindingFlags.Instance | BindingFlags.Public);
            if (f == null || !f.FieldType.IsGenericType) return null;
            return f.FieldType.GetGenericArguments()[0];
        }

        private static SerializedPropertyType ToPropType(Type t)
        {
            if (t == typeof(int))    return SerializedPropertyType.Integer;
            if (t == typeof(float))  return SerializedPropertyType.Float;
            if (t == typeof(bool))   return SerializedPropertyType.Boolean;
            if (t == typeof(string)) return SerializedPropertyType.String;
            return SerializedPropertyType.Generic;
        }

        private static float GetColumnWidth(ColumnMeta col)
        {
            if (col.fieldType != null && col.fieldType.IsGenericType
                && col.fieldType.GetGenericTypeDefinition() == typeof(List<>)) return 120f;
            switch (col.propType)
            {
                case SerializedPropertyType.Integer:
                case SerializedPropertyType.Float:
                case SerializedPropertyType.Boolean: return 70f;
                default:                             return 150f;
            }
        }

        private static List<int> BuildIndexList(int size)
        {
            var list = new List<int>(size);
            for (int i = 0; i < size; i++) list.Add(i);
            return list;
        }

        // ??? List Property 吏곷젹???????????????????????????????????????????????

        private static string SerializeListProperty(SerializedProperty prop)
        {
            if (!prop.isArray || prop.arraySize == 0) return string.Empty;
            var parts = new string[prop.arraySize];
            for (int i = 0; i < prop.arraySize; i++)
                parts[i] = PropValueToString(prop.GetArrayElementAtIndex(i));
            return "{" + string.Join(",", parts) + "}";
        }

        private static string PropValueToString(SerializedProperty prop)
        {
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: return prop.intValue.ToString();
                case SerializedPropertyType.Float:
                    return prop.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                case SerializedPropertyType.Boolean: return prop.boolValue ? "true" : "false";
                default:                             return prop.stringValue ?? string.Empty;
            }
        }

        private static void DeserializeListProperty(SerializedProperty prop, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw) || raw.Trim() == "{}")
            {
                prop.ClearArray();
                return;
            }

            string trimmed = raw.Trim();
            if (trimmed.StartsWith("{", StringComparison.Ordinal) && trimmed.EndsWith("}", StringComparison.Ordinal))
                trimmed = trimmed.Substring(1, trimmed.Length - 2);

            string[] parts = trimmed.Split(',');
            prop.arraySize = parts.Length;
            if (parts.Length == 0) return;

            SerializedPropertyType elemType = prop.GetArrayElementAtIndex(0).propertyType;
            for (int i = 0; i < parts.Length; i++)
            {
                SerializedProperty elem  = prop.GetArrayElementAtIndex(i);
                string             value = parts[i].Trim();
                switch (elemType)
                {
                    case SerializedPropertyType.Integer:
                        if (int.TryParse(value, out int iv)) elem.intValue = iv;
                        break;
                    case SerializedPropertyType.Float:
                        if (float.TryParse(value, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out float fv))
                            elem.floatValue = fv;
                        break;
                    case SerializedPropertyType.Boolean:
                        if (bool.TryParse(value, out bool bv)) elem.boolValue = bv;
                        break;
                    default:
                        elem.stringValue = value;
                        break;
                }
            }
        }

        // ??? Excel ?대뜑 ?ㅼ틪 ??????????????????????????????????????????????????

        private void ScanExcelFolder()
        {
            _excelPaths.Clear();

            string absDir = ToAbsolutePath(ExcelImportPaths.DefaultExcelFolder);
            if (Directory.Exists(absDir))
            {
                string[] files = Directory.GetFiles(absDir, "*.xlsx");
                for (int i = 0; i < files.Length; i++)
                    _excelPaths.Add(files[i]);
            }

            var names = new List<string>(_excelPaths.Count);
            for (int i = 0; i < _excelPaths.Count; i++)
                names.Add(Path.GetFileName(_excelPaths[i]));

            _excelDropdown.choices = names;

            string saved = EditorPrefs.GetString(ExcelPrefKey, string.Empty);
            int    idx   = names.IndexOf(saved);
            _excelDropdown.SetValueWithoutNotify(idx >= 0 ? names[idx] : (names.Count > 0 ? names[0] : string.Empty));

            OnExcelFileSelected();
        }

        // ??? Asset Loading ????????????????????????????????????????????????????

        private void OnExcelFileSelected()
        {
            _selectedExcelPath = string.Empty;
            string selected = _excelDropdown.value;
            if (!string.IsNullOrEmpty(selected))
            {
                EditorPrefs.SetString(ExcelPrefKey, selected);
                for (int i = 0; i < _excelPaths.Count; i++)
                {
                    if (Path.GetFileName(_excelPaths[i]) == selected)
                    {
                        _selectedExcelPath = _excelPaths[i];
                        break;
                    }
                }
            }
            LoadAssets();
        }

        private void LoadAssets()
        {
            _assets.Clear();

            string scanFolder = null;
            if (!string.IsNullOrEmpty(_selectedExcelPath) && File.Exists(_selectedExcelPath))
            {
                string excelName = Path.GetFileNameWithoutExtension(_selectedExcelPath);
                string subFolder = ExcelImportPaths.GetTableAssetFolder(excelName);
                if (AssetDatabase.IsValidFolder(subFolder))
                    scanFolder = subFolder;
            }

            if (scanFolder == null)
            {
                _emptyLabel.style.display = DisplayStyle.Flex;
                _listView.style.display   = DisplayStyle.None;
                _listView.itemsSource     = _assets;
                _listView.Rebuild();
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { scanFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                if (so != null) _assets.Add(so);
            }

            if (!string.IsNullOrEmpty(_selectedExcelPath) && File.Exists(_selectedExcelPath))
            {
                List<string> sheetNames = GetDataSheetNames(_selectedExcelPath);
                _assets.Sort((a, b) =>
                {
                    int ia = sheetNames.IndexOf(a.name.Replace("DataTable", string.Empty));
                    int ib = sheetNames.IndexOf(b.name.Replace("DataTable", string.Empty));
                    if (ia < 0) ia = int.MaxValue;
                    if (ib < 0) ib = int.MaxValue;
                    return ia.CompareTo(ib);
                });
            }

            bool hasItems = _assets.Count > 0;
            _emptyLabel.style.display = hasItems ? DisplayStyle.None : DisplayStyle.Flex;
            _listView.style.display   = hasItems ? DisplayStyle.Flex  : DisplayStyle.None;
            _listView.itemsSource     = _assets;
            _listView.Rebuild();
        }

        private static List<string> GetDataSheetNames(string excelFilePath)
        {
            var result = new List<string>();
            try
            {
                IWorkbook workbook;
                using (FileStream fs = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    workbook = new XSSFWorkbook(fs);

                for (int i = 0; i < workbook.NumberOfSheets; i++)
                {
                    ISheet sheet = workbook.GetSheetAt(i);
                    if (sheet != null && SheetHasDataRows(sheet))
                        result.Add(sheet.SheetName);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ExcelDataViewer] Excel ?쎄린 ?ㅽ뙣: {ex.Message}");
            }
            return result;
        }

        private static bool SheetHasDataRows(ISheet sheet)
        {
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow   row    = sheet.GetRow(r);
                ICell  cell   = row?.GetCell(0);
                string marker = cell?.ToString()?.Trim().ToLower();
                if (marker == "#data") return true;
                if (marker == "#end")  break;
            }
            return false;
        }

        // ??? Event Handlers ???????????????????????????????????????????????????

        public void RefreshAssets()
        {
            ScanExcelFolder();
        }

        private void OnRefreshClicked()
        {
            AssetDatabase.Refresh();
            ScanExcelFolder();
        }

        private void OnSelectionChanged(IEnumerable<object> selection)
        {
            _selected = null;
            foreach (object item in selection) { _selected = item as ScriptableObject; break; }

            _saveButton.SetEnabled(_selected != null && !string.IsNullOrEmpty(_selectedExcelPath));

            if (_selected == null) ShowRightEmpty();
            else                   ShowInspector(_selected);
        }

        private void OnSaveToExcel()
        {
            if (_selected == null || string.IsNullOrEmpty(_selectedExcelPath)) return;

            if (!_dirtyAssets.Contains(_selected))
            {
                Debug.Log("[ExcelDataViewer] No changed data. Skipped Excel save.");
                return;
            }

            SOToExcelExporter.OverwriteSheet(_selected, _selectedExcelPath);
            AssetDatabase.SaveAssets();
            _dirtyAssets.Remove(_selected);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string rel = assetPath.Replace('\\', '/');
            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, rel);
        }
    }

    // ??? SO ??Excel ?먮낯 ?쒗듃 ??뼱?곌린 ??????????????????????????????????????

    internal static class SOToExcelExporter
    {
        private const string BackupFolderAssetPath = "Assets/ASB_Work/.ExcelBackup";
        private const int MaxBackupsPerWorkbook = 3;

        private struct ColInfo
        {
            public int    index;
            public string excelType;
            public ListDelimiter listDelimiter;
        }

        private static string ToSheetName(string typeName)
        {
            const string suffix = "DataTable";
            return typeName.EndsWith(suffix, StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - suffix.Length)
                : typeName;
        }

        // ??? 蹂寃쎈맂 ?留?????????????????????????????????????????????????????

        public static void OverwriteSheet(ScriptableObject asset, string excelFilePath)
        {
            if (asset == null || string.IsNullOrEmpty(excelFilePath)) return;
            if (IsRawImportAsset(asset)) return;

            FieldInfo dataListField = asset.GetType()
                .GetField("DataList", BindingFlags.Instance | BindingFlags.Public);
            if (dataListField == null)
            {
                Debug.LogError($"[SOToExcelExporter] DataList field was not found: {asset.GetType().Name}");
                return;
            }

            IList dataList = dataListField.GetValue(asset) as IList;
            if (dataList == null || dataList.Count == 0)
            {
                Debug.LogWarning($"[SOToExcelExporter] DataList is empty: {asset.name}");
                return;
            }

            Type        rowType   = dataList.GetType().GetGenericArguments()[0];
            FieldInfo[] fields    = rowType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            ExcelSheetSchemaSO schema = FindSchemaForAsset(asset);
            string      sheetName = schema != null ? schema.sourceSheetName : ToSheetName(asset.GetType().Name);

            IWorkbook workbook;
            using (FileStream fs = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                workbook = new XSSFWorkbook(fs);

            ISheet sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                Debug.LogError($"[SOToExcelExporter] Sheet was not found: '{sheetName}' ({Path.GetFileName(excelFilePath)})");
                return;
            }

            Dictionary<string, ColInfo> colMap   = schema != null ? BuildColMapFromSchema(sheet, schema) : BuildColMap(sheet);
            List<IRow>                  dataRows = schema != null ? CollectDataRowsFromSchema(sheet, schema.dataStartRowIndex) : CollectDataRows(sheet);
            var styleCache = new Dictionary<string, ICellStyle>();

            if (dataList.Count != dataRows.Count)
            {
                Debug.LogWarning(
                    $"[SOToExcelExporter] '{sheetName}': " +
                    $"SO row count {dataList.Count} differs from Excel data row count {dataRows.Count}. " +
                    $"Only {Math.Min(dataList.Count, dataRows.Count)} rows will be written.");
            }

            int rowCount = Math.Min(dataList.Count, dataRows.Count);
            for (int i = 0; i < rowCount; i++)
            {
                IRow   excelRow = dataRows[i];
                object rowObj   = dataList[i];
                for (int f = 0; f < fields.Length; f++)
                {
                    if (!colMap.TryGetValue(fields[f].Name, out ColInfo col)) continue;
                    object val  = fields[f].GetValue(rowObj);
                    ICell  cell = excelRow.GetCell(col.index) ?? excelRow.CreateCell(col.index);
                    WriteCellValue(cell, val, col.excelType, workbook, styleCache, col.listDelimiter);
                }
            }

            SafeSaveWorkbook(workbook, excelFilePath);

            Debug.Log($"[SOToExcelExporter] Saved full sheet '{sheetName}' ({rowCount} rows) -> {Path.GetFileName(excelFilePath)}");
        }

        // ??? 怨듯넻 ?대? ?좏떥 ???????????????????????????????????????????????????

        private static void SafeSaveWorkbook(IWorkbook workbook, string excelFilePath)
        {
            if (workbook == null)
            {
                throw new ArgumentNullException(nameof(workbook));
            }

            if (string.IsNullOrWhiteSpace(excelFilePath))
            {
                throw new ArgumentException("Excel file path is empty.", nameof(excelFilePath));
            }

            string fullExcelPath = Path.GetFullPath(excelFilePath);
            string dir = Path.GetDirectoryName(fullExcelPath);
            if (string.IsNullOrEmpty(dir))
            {
                throw new InvalidDataException($"Invalid Excel path: {excelFilePath}");
            }

            string fileName = Path.GetFileName(fullExcelPath);
            string tempPath = Path.Combine(dir, $".{fileName}.{Guid.NewGuid():N}.tmp");

            try
            {
                using (FileStream fs = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    workbook.Write(fs);
                }

                ValidateWorkbookFile(tempPath);
                if (!File.Exists(fullExcelPath))
                {
                    File.Move(tempPath, fullExcelPath);
                    return;
                }

                string backupPath = CreateBackupPath(fullExcelPath);
                try
                {
                    File.Replace(tempPath, fullExcelPath, backupPath, true);
                }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(fullExcelPath, backupPath, true);
                    File.Copy(tempPath, fullExcelPath, true);
                }
                catch (IOException)
                {
                    File.Copy(fullExcelPath, backupPath, true);
                    File.Copy(tempPath, fullExcelPath, true);
                }

                PruneBackups(fullExcelPath);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        private static void ValidateWorkbookFile(string path)
        {
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                XSSFWorkbook validationWorkbook = new XSSFWorkbook(fs);
                if (validationWorkbook.NumberOfSheets <= 0)
                {
                    throw new InvalidDataException("Saved workbook has no sheets.");
                }
                validationWorkbook.Close();
            }
        }

        private static string CreateBackupPath(string excelFilePath)
        {
            EnsureBackupFolder();
            string safeName = CodeGenerator.ToTypeBaseName(Path.GetFileNameWithoutExtension(excelFilePath));
            string extension = Path.GetExtension(excelFilePath);
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{safeName}_{stamp}_{Guid.NewGuid():N}{extension}";
            return Path.Combine(GetBackupFolderAbsolutePath(), fileName);
        }

        private static void EnsureBackupFolder()
        {
            string path = GetBackupFolderAbsolutePath();
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
        }

        private static string GetBackupFolderAbsolutePath()
        {
            string relative = BackupFolderAssetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar);
            return Path.GetFullPath(Path.Combine(Application.dataPath, relative));
        }

        private static void PruneBackups(string excelFilePath)
        {
            string backupFolder = GetBackupFolderAbsolutePath();
            if (!Directory.Exists(backupFolder))
            {
                return;
            }

            string safeName = CodeGenerator.ToTypeBaseName(Path.GetFileNameWithoutExtension(excelFilePath));
            string extension = Path.GetExtension(excelFilePath);
            FileInfo[] backups = new DirectoryInfo(backupFolder).GetFiles($"{safeName}_*{extension}");
            Array.Sort(backups, (a, b) => b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc));
            for (int i = MaxBackupsPerWorkbook; i < backups.Length; i++)
            {
                backups[i].Delete();
            }
        }

        private static Dictionary<string, ColInfo> BuildColMap(ISheet sheet)
        {
            var map      = new Dictionary<string, ColInfo>(StringComparer.Ordinal);
            int typeRowI = -1;
            int nameRowI = -1;

            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow   row    = sheet.GetRow(r);
                string marker = row?.GetCell(0)?.ToString()?.Trim().ToLower();
                if (marker == "#type") { typeRowI = r; }
                if (marker == "#name") { nameRowI = r; }
                if (typeRowI >= 0 && nameRowI >= 0) break;
            }

            if (typeRowI < 0 || nameRowI < 0) return map;

            IRow typeRow = sheet.GetRow(typeRowI);
            IRow nameRow = sheet.GetRow(nameRowI);
            int  lastCol = Math.Max(typeRow.LastCellNum, nameRow.LastCellNum) - 1;

            for (int c = 1; c <= lastCol; c++)
            {
                string excelType = typeRow.GetCell(c)?.ToString()?.Trim() ?? string.Empty;
                string fieldName = nameRow.GetCell(c)?.ToString()?.Trim() ?? string.Empty;

                if (string.IsNullOrEmpty(fieldName)) continue;
                if (string.Equals(excelType, "#skip", StringComparison.OrdinalIgnoreCase)) continue;

                string sanitized = CodeGenerator.SanitizeFieldName(fieldName);
                if (!map.ContainsKey(sanitized))
                    map[sanitized] = new ColInfo
                    {
                        index = c,
                        excelType = excelType.ToLowerInvariant(),
                        listDelimiter = ListDelimiter.Comma
                    };
            }

            return map;
        }

        private static Dictionary<string, ColInfo> BuildColMapFromSchema(ISheet sheet, ExcelSheetSchemaSO schema)
        {
            var map = new Dictionary<string, ColInfo>(StringComparer.Ordinal);
            if (schema == null)
            {
                return map;
            }

            IRow headerRow = sheet.GetRow(Math.Max(0, schema.headerRowIndex));
            if (headerRow == null)
            {
                Debug.LogError($"[SOToExcelExporter] Schema header row not found: {schema.headerRowIndex}");
                return map;
            }

            int lastCol = Math.Max(headerRow.LastCellNum - 1, 0);
            for (int i = 0; i < schema.Columns.Count; i++)
            {
                ExcelColumnMapping column = schema.Columns[i];
                if (column == null || !column.include)
                {
                    continue;
                }

                int actualIndex = FindSchemaColumn(headerRow, lastCol, column);
                if (actualIndex < 0)
                {
                    Debug.LogError($"[SOToExcelExporter] Schema column not found: {column.fieldName} / {column.sourceHeaderName}");
                    continue;
                }

                string fieldName = CodeGenerator.SanitizeFieldName(column.fieldName);
                if (!map.ContainsKey(fieldName))
                {
                    map[fieldName] = new ColInfo
                    {
                        index = actualIndex,
                        excelType = ExcelSchemaAdapter.ToGeneratorType(column.fieldType),
                        listDelimiter = column.listDelimiter
                    };
                }
            }

            return map;
        }

        private static int FindSchemaColumn(IRow headerRow, int lastCol, ExcelColumnMapping column)
        {
            if (column.sourceColumnIndex >= 0 && column.sourceColumnIndex <= lastCol)
            {
                string hinted = headerRow.GetCell(column.sourceColumnIndex)?.ToString()?.Trim() ?? string.Empty;
                if (HeaderMatches(hinted, column))
                {
                    return column.sourceColumnIndex;
                }
            }

            for (int c = 0; c <= lastCol; c++)
            {
                string header = headerRow.GetCell(c)?.ToString()?.Trim() ?? string.Empty;
                if (HeaderMatches(header, column))
                {
                    return c;
                }
            }

            return -1;
        }

        private static bool HeaderMatches(string header, ExcelColumnMapping column)
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(column.sourceHeaderName) &&
                string.Equals(header, column.sourceHeaderName, StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(CodeGenerator.SanitizeFieldName(header), column.fieldName, StringComparison.Ordinal);
        }

        private static void WriteCellValue(
            ICell cell, object value, string excelType,
            IWorkbook workbook, Dictionary<string, ICellStyle> styleCache, ListDelimiter listDelimiter)
        {
            if (value == null) { cell.SetBlank(); return; }

            switch (excelType)
            {
                case "int":
                    cell.SetCellValue((double)Convert.ToInt32(value));
                    break;

                case "float":
                    cell.SetCellValue(Convert.ToDouble(value,
                        System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case "percent":
                {
                    cell.SetCellValue(Convert.ToDouble(value,
                        System.Globalization.CultureInfo.InvariantCulture));

                    if (!styleCache.TryGetValue("percent", out ICellStyle pctStyle))
                    {
                        pctStyle = workbook.CreateCellStyle();
                        pctStyle.DataFormat = workbook.CreateDataFormat().GetFormat("0.##%");
                        styleCache["percent"] = pctStyle;
                    }
                    cell.CellStyle = pctStyle;
                    break;
                }

                case "bool":
                    cell.SetCellValue(Convert.ToBoolean(value) ? 1.0 : 0.0);
                    break;

                case "string":
                    cell.SetCellValue(value.ToString());
                    break;

                default:
                    if (excelType.StartsWith("list<", StringComparison.Ordinal))
                    {
                        IList list = value as IList;
                        if (list == null || list.Count == 0) { cell.SetBlank(); return; }

                        if (excelType == "list<string>" && listDelimiter == ListDelimiter.Backslash)
                        {
                            cell.SetCellValue(ExcelListParser.FormatBackslashStringList(list));
                            return;
                        }

                        var parts = new string[list.Count];
                        bool isFloatList = excelType == "list<float>";
                        bool isBoolList  = excelType == "list<bool>";

                        for (int i = 0; i < list.Count; i++)
                        {
                            if (list[i] == null) { parts[i] = string.Empty; continue; }
                            if (isFloatList)
                                parts[i] = Convert.ToSingle(list[i])
                                    .ToString("G", System.Globalization.CultureInfo.InvariantCulture);
                            else if (isBoolList)
                                parts[i] = Convert.ToBoolean(list[i]) ? "true" : "false";
                            else
                                parts[i] = list[i].ToString();
                        }
                        cell.SetCellValue("{" + string.Join(",", parts) + "}");
                    }
                    else
                    {
                        cell.SetCellValue(value.ToString());
                    }
                    break;
            }
        }

        private static List<IRow> CollectDataRows(ISheet sheet)
        {
            var  rows        = new List<IRow>();
            bool dataStarted = false;
            int  lastDataCol = GetLastUsedColumnInSheet(sheet);

            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow   row    = sheet.GetRow(r);
                string marker = row?.GetCell(0)?.ToString()?.Trim().ToLower() ?? string.Empty;

                if (!dataStarted)
                {
                    if (marker == "#data") { dataStarted = true; rows.Add(row); }
                    continue;
                }

                if (marker == "#end") break;

                if (row == null || IsRowEmpty(row, lastDataCol)) continue;

                if (marker == string.Empty || marker == "#data")
                    rows.Add(row);
            }
            return rows;
        }

        private static List<IRow> CollectDataRowsFromSchema(ISheet sheet, int dataStartRowIndex)
        {
            var rows = new List<IRow>();
            int lastDataCol = GetLastUsedColumnInSheet(sheet);
            for (int r = Math.Max(sheet.FirstRowNum, dataStartRowIndex); r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row == null || IsRowEmpty(row, lastDataCol))
                {
                    continue;
                }

                rows.Add(row);
            }

            return rows;
        }

        private static bool IsRawImportAsset(ScriptableObject asset)
        {
            if (asset is RawExcelSheetSO || asset is ExcelSheetSchemaSO)
            {
                Debug.LogWarning("[SOToExcelExporter] Raw/Schema import assets cannot be exported to Excel.");
                return true;
            }

            string path = AssetDatabase.GetAssetPath(asset);
            if (!string.IsNullOrEmpty(path) &&
                path.Replace('\\', '/').StartsWith(ExcelImportPaths.RawImportFolder, StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[SOToExcelExporter] RawImports folder is excluded from Excel export.");
                return true;
            }

            return false;
        }

        private static ExcelSheetSchemaSO FindSchemaForAsset(ScriptableObject asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            AssetImporter importer = AssetImporter.GetAtPath(assetPath);
            string schemaGuid = importer?.userData;
            if (string.IsNullOrWhiteSpace(schemaGuid))
            {
                return null;
            }

            string schemaPath = AssetDatabase.GUIDToAssetPath(schemaGuid);
            ExcelSheetSchemaSO schema = string.IsNullOrEmpty(schemaPath)
                ? null
                : AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(schemaPath);
            if (schema != null)
            {
                return schema;
            }

            if (!AssetDatabase.IsValidFolder(ExcelImportPaths.SchemaFolder))
            {
                Debug.LogError($"[SOToExcelExporter] Schema folder was not found: {ExcelImportPaths.SchemaFolder}");
                return null;
            }

            string[] schemaGuids = AssetDatabase.FindAssets("t:ExcelSheetSchemaSO", new[] { ExcelImportPaths.SchemaFolder });
            for (int i = 0; i < schemaGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(schemaGuids[i]);
                schema = AssetDatabase.LoadAssetAtPath<ExcelSheetSchemaSO>(path);
                if (schema != null && (schema.schemaGuid == schemaGuid || schemaGuids[i] == schemaGuid))
                {
                    return schema;
                }
            }

            Debug.LogError($"[SOToExcelExporter] Schema metadata exists but schema asset was not found: {schemaGuid}");
            return null;
        }

        private static bool IsRowEmpty(IRow row, int lastCol)
        {
            for (int c = 0; c <= lastCol; c++)
            {
                ICell cell = row.GetCell(c);
                if (cell != null && !string.IsNullOrWhiteSpace(cell.ToString()))
                    return false;
            }
            return true;
        }

        private static int GetLastUsedColumnInSheet(ISheet sheet)
        {
            int last = 1;
            for (int r = sheet.FirstRowNum; r <= sheet.LastRowNum; r++)
            {
                IRow row = sheet.GetRow(r);
                if (row != null && row.LastCellNum - 1 > last)
                    last = row.LastCellNum - 1;
            }
            return last;
        }
    }
}
