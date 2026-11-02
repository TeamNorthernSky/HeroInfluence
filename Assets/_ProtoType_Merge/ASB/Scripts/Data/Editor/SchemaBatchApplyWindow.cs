using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace ASB.ExcelImport.Editor
{
    internal sealed class SchemaBatchApplyWindow : EditorWindow
    {
        [SerializeField] private string _targetPath;
        [SerializeField] private string _sourcePath;
        [SerializeField] private bool _createTables = true;
        private RawSheetMapperWindow _mapper;
        private List<string> _sources = new List<string>();
        private List<RawExcelSheetSO> _raws = new List<RawExcelSheetSO>();
        private List<string> _partial = new List<string>();
        private SchemaBatchPreview _preview;
        private Vector2 _scroll;
        private string _error;

        public static void Open(string targetPath, RawSheetMapperWindow mapper)
        {
            SchemaBatchApplyWindow window = GetWindow<SchemaBatchApplyWindow>(true, "엑셀 전체 스키마 적용", true);
            window.minSize = new Vector2(700f, 500f);
            window._mapper = mapper;
            window._targetPath = targetPath;
            window.LoadPreview();
            window.Show();
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(_targetPath)) LoadPreview();
        }

        private void LoadPreview()
        {
            try
            {
                _error = string.Empty;
                _sources = ExcelSchemaBatchApplier.SourceCandidates(_targetPath);
                if (!_sources.Contains(_sourcePath)) _sourcePath = ExcelSchemaBatchApplier.DefaultSource(_targetPath, _sources);
                _partial = new List<string>();
                _raws = ExcelRawImportUtility.ImportMarkerlessSheets(
                    ExcelRawImportUtility.ToAbsoluteAssetPath(_targetPath), null, _partial);
                RebuildPreview();
            }
            catch (Exception ex)
            {
                _error = ex.Message;
                _preview = null;
                Debug.LogError($"[ExcelSchemaBatch] Preview failed: {ex}");
            }
        }

        private void RebuildPreview()
        {
            _preview = string.IsNullOrEmpty(_sourcePath) ? null :
                ExcelSchemaBatchApplier.BuildPreview(_targetPath, _sourcePath, _raws, _partial);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("새 엑셀", _targetPath ?? string.Empty);
            if (!string.IsNullOrEmpty(_error)) EditorGUILayout.HelpBox(_error, MessageType.Error);
            if (_sources.Count == 0)
            {
                EditorGUILayout.HelpBox("스키마가 있는 원본 엑셀이 없어요.", MessageType.Warning);
                return;
            }

            int selectedIndex = Mathf.Max(0, _sources.IndexOf(_sourcePath));
            int nextIndex = EditorGUILayout.Popup("원본 엑셀", selectedIndex,
                _sources.Select(path => Path.GetFileNameWithoutExtension(path)).ToArray());
            if (nextIndex != selectedIndex || _preview == null)
            {
                _sourcePath = _sources[nextIndex];
                RebuildPreview();
            }

            EditorGUILayout.HelpBox("헤더 일치는 현재 Validate와 같은 규칙으로 검사해요. 포함된 열 중 저장된 헤더 이름이 있는 열만 비교해요.", MessageType.Info);
            _createTables = EditorGUILayout.ToggleLeft("구조가 같은 시트는 테이블도 생성", _createTables);
            if (_preview == null) return;

            EditorGUILayout.Space(6f);
            EditorGUILayout.LabelField($"마커 없는 시트 {_preview.items.Count}개", EditorStyles.boldLabel);
            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUI.skin.box);
            foreach (SchemaPreviewItem item in _preview.items)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                using (new EditorGUI.DisabledScope(!item.CanApply))
                {
                    item.selected = EditorGUILayout.ToggleLeft($"{item.raw.sheetName}  ·  {StateLabel(item.state)}", item.selected);
                }
                EditorGUILayout.LabelField(item.message, EditorStyles.wordWrappedMiniLabel);
                if (item.state == SchemaPreviewState.BrokenLink && item.targetSchema != null &&
                    GUILayout.Button("기존 스키마 연결", GUILayout.Width(130f)))
                {
                    if (ExcelSchemaBatchApplier.TryReconnectBrokenLink(item.raw, item.targetSchema, out string error))
                    {
                        LoadPreview();
                    }
                    else
                    {
                        _error = error;
                    }
                    GUIUtility.ExitGUI();
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndScrollView();

            List<string> selectedNames = _preview.items.Where(item => item.CanApply && item.selected)
                .Select(item => item.raw.sheetName).ToList();
            Dictionary<string, string> outputConflicts = ExcelSchemaBatchApplier.FindOutputConflicts(_preview, selectedNames);
            if (outputConflicts.Count > 0)
                EditorGUILayout.HelpBox("선택한 시트의 출력 경로 충돌: " +
                    string.Join("\n", outputConflicts.Values.Distinct()), MessageType.Error);

            if (_preview.partialMarkerSheets.Count > 0)
                EditorGUILayout.HelpBox("마커가 일부만 있는 시트: " + string.Join(", ", _preview.partialMarkerSheets), MessageType.Error);
            if (_preview.missingSourceSheets.Count > 0)
                EditorGUILayout.HelpBox("원본에는 있지만 새 엑셀에는 없는 시트: " + string.Join(", ", _preview.missingSourceSheets), MessageType.Warning);

            EditorGUILayout.Space(5f);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("새로 읽기", GUILayout.Width(90f))) LoadPreview();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("취소", GUILayout.Width(90f))) Close();
            using (new EditorGUI.DisabledScope(!_preview.items.Any(item => item.CanApply && item.selected)))
            {
                if (GUILayout.Button("적용", GUILayout.Width(90f)))
                {
                    List<string> selected = _preview.items.Where(item => item.CanApply && item.selected)
                        .Select(item => item.raw.sheetName).ToList();
                    WorkbookBatchReport report = ExcelSchemaBatchApplier.Run(
                        _targetPath, _sourcePath, _createTables, selected);
                    if (_mapper == null) _mapper = GetWindow<RawSheetMapperWindow>();
                    _mapper.AcceptSchemaBatchReport(report);
                    Close();
                    GUIUtility.ExitGUI();
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private static string StateLabel(SchemaPreviewState state)
        {
            switch (state)
            {
                case SchemaPreviewState.Match: return "헤더 일치";
                case SchemaPreviewState.NewColumns: return "헤더 일치 + 새 열";
                case SchemaPreviewState.HeaderMismatch: return "열 위치 또는 헤더 다름";
                case SchemaPreviewState.NoSource: return "짝 없음";
                case SchemaPreviewState.DuplicateSource: return "원본 중복";
                case SchemaPreviewState.AlreadyHasSchema: return "이미 스키마 있음";
                case SchemaPreviewState.BrokenLink: return "연결 끊김";
                default: return "대상 중복";
            }
        }
    }
}
