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

        private readonly Dictionary<ScriptableObject, HashSet<string>> _dirtyTracker =
            new Dictionary<ScriptableObject, HashSet<string>>();

        // BindProperty가 userData를 덮어쓰므로 경로는 별도 맵에 보관
        private readonly Dictionary<VisualElement, string> _cellPathMap =
            new Dictionary<VisualElement, string>();

        // bindCell에서 BindProperty 재연결 시 ChangeEvent 오발 방지
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

        // ─── Toolbar ──────────────────────────────────────────────────────────

        private void BuildToolbar()
        {
            var toolbar = new Toolbar();

            var importBtn = new ToolbarButton(ExcelEditorWindow.ShowWindow) { text = "📥 Excel Importer 열기" };
            toolbar.Add(importBtn);

            var spacer = new ToolbarSpacer();
            spacer.style.flexGrow = 1f;
            toolbar.Add(spacer);

            _saveButton = new ToolbarButton(OnSaveToExcel) { text = "💾 Save to Excel" };
            _saveButton.style.backgroundColor = new Color(0.2f, 0.5f, 0.2f);
            _saveButton.SetEnabled(false);
            toolbar.Add(_saveButton);

            rootVisualElement.Add(toolbar);
        }

        // ─── Split View ───────────────────────────────────────────────────────

        private void BuildSplitView()
        {
            var splitView = new TwoPaneSplitView(0, 250f, TwoPaneSplitViewOrientation.Horizontal);
            splitView.style.flexGrow = 1f;
            splitView.Add(BuildLeftPanel());
            splitView.Add(BuildRightPanel());
            rootVisualElement.Add(splitView);
        }

        // ─── Left Panel ───────────────────────────────────────────────────────

        private VisualElement BuildLeftPanel()
        {
            var panel = new VisualElement();
            panel.style.paddingTop    = 4f;
            panel.style.paddingBottom = 4f;
            panel.style.paddingLeft   = 4f;
            panel.style.paddingRight  = 4f;
            panel.style.flexDirection = FlexDirection.Column;

            var dropLabel = new Label("Excel 파일");
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

        // ─── Right Panel ──────────────────────────────────────────────────────

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

        // ─── Grid View ────────────────────────────────────────────────────────

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

                // so.targetObject를 캡처 (_selected는 이후 변경될 수 있으므로 사용 금지)
                ScriptableObject capturedAsset = so.targetObject as ScriptableObject;

                column.makeCell = () =>
                {
                    if (isList)
                    {
                        // List 타입은 bindCell에서 콜백 등록/해제 관리
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
                                _cellPathMap.TryGetValue(f, out string path);
                                MarkDirty(capturedAsset, path);
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
                                _cellPathMap.TryGetValue(f, out string path);
                                MarkDirty(capturedAsset, path);
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
                                _cellPathMap.TryGetValue(f, out string path);
                                MarkDirty(capturedAsset, path);
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
                                _cellPathMap.TryGetValue(f, out string path);
                                MarkDirty(capturedAsset, path);
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

                        string capturedPath = prop.propertyPath;

                        EventCallback<ChangeEvent<string>> cb = evt =>
                        {
                            if (EqualityComparer<string>.Default.Equals(evt.previousValue, evt.newValue)) return;
                            Undo.RecordObject(capturedAsset, "Edit Excel Data");
                            DeserializeListProperty(prop, evt.newValue);
                            so.ApplyModifiedProperties();
                            MarkDirty(capturedAsset, capturedPath);
                        };
                        tf.userData = cb;
                        tf.RegisterValueChangedCallback(cb);
                        return;
                    }

                    // 비 List 타입: BindProperty 전후를 _isBinding으로 감싸 오발 방지
                    // userData는 BindProperty가 덮어쓸 수 있으므로 별도 맵에 경로 보관
                    _isBinding                = true;
                    _cellPathMap[element]     = prop.propertyPath;

                    switch (propType)
                    {
                        case SerializedPropertyType.Integer: ((IntegerField)element).BindProperty(prop); break;
                        case SerializedPropertyType.Float:   ((FloatField)element).BindProperty(prop);   break;
                        case SerializedPropertyType.Boolean: ((Toggle)element).BindProperty(prop);       break;
                        default:                             ((TextField)element).BindProperty(prop);    break;
                    }

                    // BindProperty가 동기 이벤트를 발생시킬 수 있으므로 다음 프레임에 해제
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

        // ─── Dirty Tracking ───────────────────────────────────────────────────

        private void MarkDirty(ScriptableObject asset, string propertyPath)
        {
            if (asset == null || string.IsNullOrEmpty(propertyPath)) return;
            if (!_dirtyTracker.ContainsKey(asset))
                _dirtyTracker[asset] = new HashSet<string>();
            _dirtyTracker[asset].Add(propertyPath);
            EditorUtility.SetDirty(asset);
        }

        // 입력 예시: "DataList.Array.data[2].EnemyHp"
        // 출력:      rowIndex = 2, fieldName = "EnemyHp"
        private static bool TryParseDataListPropertyPath(string propertyPath, out int rowIndex, out string fieldName)
        {
            rowIndex  = -1;
            fieldName = string.Empty;

            int bracketOpen  = propertyPath.IndexOf('[');
            int bracketClose = propertyPath.IndexOf(']');
            if (bracketOpen < 0 || bracketClose < 0) return false;

            int dotAfter = propertyPath.IndexOf('.', bracketClose);
            if (dotAfter < 0) return false;

            string indexStr = propertyPath.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
            if (!int.TryParse(indexStr, out rowIndex)) return false;

            fieldName = propertyPath.Substring(dotAfter + 1);
            return !string.IsNullOrEmpty(fieldName);
        }

        // ─── Column Meta (Reflection 기반, Generated 클래스 타입 사용) ──────────

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

        // ─── List Property 직렬화 ─────────────────────────────────────────────

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

        // ─── Excel 폴더 스캔 ──────────────────────────────────────────────────

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

        // ─── Asset Loading ────────────────────────────────────────────────────

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

            if (!string.IsNullOrEmpty(_selectedExcelPath) && File.Exists(_selectedExcelPath))
            {
                List<string> sheetNames = GetDataSheetNames(_selectedExcelPath);
                for (int i = 0; i < sheetNames.Count; i++)
                {
                    string assetPath = $"{ExcelImportPaths.TableAssetFolder}/{sheetNames[i]}DataTable.asset";
                    ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                    if (so != null) _assets.Add(so);
                }
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
                Debug.LogError($"[ExcelDataViewer] Excel 읽기 실패: {ex.Message}");
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

        // ─── Event Handlers ───────────────────────────────────────────────────

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

            _dirtyTracker.TryGetValue(_selected, out HashSet<string> dirtyPaths);

            if (dirtyPaths == null || dirtyPaths.Count == 0)
            {
                Debug.Log("[ExcelDataViewer] 변경된 셀이 없어 Excel 저장을 건너뜁니다.");
                return;
            }

            SOToExcelExporter.OverwriteDirtyCells(_selected, _selectedExcelPath, dirtyPaths);
            AssetDatabase.SaveAssets();
            _dirtyTracker.Remove(_selected);
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string rel = assetPath.Replace('\\', '/');
            if (rel.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                rel = rel.Substring("Assets/".Length);
            return Path.Combine(Application.dataPath, rel);
        }
    }

    // ─── SO → Excel 원본 시트 덮어쓰기 ──────────────────────────────────────

    internal static class SOToExcelExporter
    {
        private struct ColInfo
        {
            public int    index;
            public string excelType;
        }

        private static string ToSheetName(string typeName)
        {
            const string suffix = "DataTable";
            return typeName.EndsWith(suffix, StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - suffix.Length)
                : typeName;
        }

        // ─── 변경된 셀만 저장 ─────────────────────────────────────────────────

        public static void OverwriteDirtyCells(
            ScriptableObject asset,
            string excelFilePath,
            HashSet<string> dirtyPaths)
        {
            if (asset == null || string.IsNullOrEmpty(excelFilePath) || dirtyPaths == null) return;

            FieldInfo dataListField = asset.GetType()
                .GetField("DataList", BindingFlags.Instance | BindingFlags.Public);
            if (dataListField == null)
            {
                Debug.LogError($"[SOToExcelExporter] DataList 필드 없음: {asset.GetType().Name}");
                return;
            }

            IList dataList = dataListField.GetValue(asset) as IList;
            if (dataList == null || dataList.Count == 0)
            {
                Debug.LogWarning($"[SOToExcelExporter] DataList 비어있음: {asset.name}");
                return;
            }

            Type        rowType   = dataList.GetType().GetGenericArguments()[0];
            FieldInfo[] fields    = rowType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            string      sheetName = ToSheetName(asset.GetType().Name);

            IWorkbook workbook;
            using (FileStream fs = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                workbook = new XSSFWorkbook(fs);

            ISheet sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                Debug.LogError($"[SOToExcelExporter] 시트 없음: '{sheetName}' ({Path.GetFileName(excelFilePath)})");
                return;
            }

            Dictionary<string, ColInfo> colMap   = BuildColMap(sheet);
            List<IRow>                  dataRows = CollectDataRows(sheet);
            var styleCache = new Dictionary<string, ICellStyle>();

            // dirtyPaths에서 (rowIndex, fieldName) 쌍 추출 후 해당 셀만 덮어씀
            int savedCount = 0;
            foreach (string propPath in dirtyPaths)
            {
                if (!TryParseDataListPropertyPath(propPath, out int rowIndex, out string fieldName))
                {
                    Debug.LogWarning($"[SOToExcelExporter] propertyPath 파싱 실패: {propPath}");
                    continue;
                }

                if (rowIndex < 0 || rowIndex >= dataList.Count || rowIndex >= dataRows.Count)
                {
                    Debug.LogWarning($"[SOToExcelExporter] rowIndex 범위 초과: {rowIndex} (dataList={dataList.Count}, dataRows={dataRows.Count})");
                    continue;
                }

                if (!colMap.TryGetValue(fieldName, out ColInfo col))
                {
                    Debug.LogWarning($"[SOToExcelExporter] 필드 없음: {fieldName}");
                    continue;
                }

                FieldInfo field = FindField(fields, fieldName);
                if (field == null) continue;

                object rowObj = dataList[rowIndex];
                object val    = field.GetValue(rowObj);
                IRow   excelRow = dataRows[rowIndex];
                ICell  cell     = excelRow.GetCell(col.index) ?? excelRow.CreateCell(col.index);
                WriteCellValue(cell, val, col.excelType, workbook, styleCache);
                savedCount++;
            }

            using (FileStream fs = new FileStream(excelFilePath, FileMode.Create, FileAccess.Write))
                workbook.Write(fs);

            Debug.Log($"[SOToExcelExporter] '{sheetName}' 부분 저장 완료 ({savedCount}셀) → {Path.GetFileName(excelFilePath)}");
        }

        private static FieldInfo FindField(FieldInfo[] fields, string fieldName)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].Name == fieldName) return fields[i];
            }
            return null;
        }

        // propertyPath 파싱 — ExcelDataViewerWindow와 동일한 규칙
        private static bool TryParseDataListPropertyPath(string propertyPath, out int rowIndex, out string fieldName)
        {
            rowIndex  = -1;
            fieldName = string.Empty;

            int bracketOpen  = propertyPath.IndexOf('[');
            int bracketClose = propertyPath.IndexOf(']');
            if (bracketOpen < 0 || bracketClose < 0) return false;

            int dotAfter = propertyPath.IndexOf('.', bracketClose);
            if (dotAfter < 0) return false;

            string indexStr = propertyPath.Substring(bracketOpen + 1, bracketClose - bracketOpen - 1);
            if (!int.TryParse(indexStr, out rowIndex)) return false;

            fieldName = propertyPath.Substring(dotAfter + 1);
            return !string.IsNullOrEmpty(fieldName);
        }

        // ─── 전체 저장 (기존 유지) ────────────────────────────────────────────

        public static void OverwriteSheet(ScriptableObject asset, string excelFilePath)
        {
            if (asset == null || string.IsNullOrEmpty(excelFilePath)) return;

            FieldInfo dataListField = asset.GetType()
                .GetField("DataList", BindingFlags.Instance | BindingFlags.Public);
            if (dataListField == null)
            {
                Debug.LogError($"[SOToExcelExporter] DataList 필드 없음: {asset.GetType().Name}");
                return;
            }

            IList dataList = dataListField.GetValue(asset) as IList;
            if (dataList == null || dataList.Count == 0)
            {
                Debug.LogWarning($"[SOToExcelExporter] DataList 비어있음: {asset.name}");
                return;
            }

            Type        rowType   = dataList.GetType().GetGenericArguments()[0];
            FieldInfo[] fields    = rowType.GetFields(BindingFlags.Instance | BindingFlags.Public);
            string      sheetName = ToSheetName(asset.GetType().Name);

            IWorkbook workbook;
            using (FileStream fs = new FileStream(excelFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                workbook = new XSSFWorkbook(fs);

            ISheet sheet = workbook.GetSheet(sheetName);
            if (sheet == null)
            {
                Debug.LogError($"[SOToExcelExporter] 시트 없음: '{sheetName}' ({Path.GetFileName(excelFilePath)})");
                return;
            }

            Dictionary<string, ColInfo> colMap   = BuildColMap(sheet);
            List<IRow>                  dataRows = CollectDataRows(sheet);
            var styleCache = new Dictionary<string, ICellStyle>();

            if (dataList.Count != dataRows.Count)
            {
                Debug.LogWarning(
                    $"[SOToExcelExporter] '{sheetName}': " +
                    $"SO 데이터 {dataList.Count}행 vs Excel #data구간 {dataRows.Count}행 불일치. " +
                    $"{Math.Min(dataList.Count, dataRows.Count)}행만 저장됩니다.");
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
                    WriteCellValue(cell, val, col.excelType, workbook, styleCache);
                }
            }

            using (FileStream fs = new FileStream(excelFilePath, FileMode.Create, FileAccess.Write))
                workbook.Write(fs);

            Debug.Log($"[SOToExcelExporter] '{sheetName}' 전체 저장 완료 ({rowCount}행) → {Path.GetFileName(excelFilePath)}");
        }

        // ─── 공통 내부 유틸 ───────────────────────────────────────────────────

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
                    map[sanitized] = new ColInfo { index = c, excelType = excelType.ToLowerInvariant() };
            }

            return map;
        }

        private static void WriteCellValue(
            ICell cell, object value, string excelType,
            IWorkbook workbook, Dictionary<string, ICellStyle> styleCache)
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
