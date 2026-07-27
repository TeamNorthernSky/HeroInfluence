using UnityEditor;
        using UnityEngine;
        
        /// <summary>
        /// Editor-only handle tool for Moving Attack Entry/Mid/Exit offsets.
        /// It reads PreviewBattleSceneManager's fixed scene anchors and never participates in runtime combat.
        /// </summary>
        [InitializeOnLoad]
        public static class MovingAttackPathSceneTool
        {
            private const int SampleCount = 64;
            private static SkillPresentationData _editingData;
            private static Vector3[] _samples = new Vector3[SampleCount + 1];
            private static Vector3 _cachedActor;
            private static Vector3 _cachedCenter;
            private static Vector2 _cachedEntry;
            private static Vector2 _cachedMid;
            private static Vector2 _cachedExit;
            private static SkillPresentationData _cachedData;
            private static bool _cacheValid;
        
            static MovingAttackPathSceneTool()
            {
                SceneView.duringSceneGui += OnSceneGui;
                Selection.selectionChanged += OnSelectionChanged;
            }
        
            public static bool IsEditing(SkillPresentationData data)
            {
                return _editingData == data;
            }
        
            public static void DrawInspectorControls(SkillPresentationData data)
            {
                if (data == null || data.MovingAttack == null)
                {
                    return;
                }
        
                EditorGUILayout.Space(4);
                using (new EditorGUI.DisabledScope(!data.MovingAttack.Enabled))
                {
                    string label = IsEditing(data) ? "Stop Editing Moving Attack Path" : "Edit Moving Attack Path";
                    if (GUILayout.Button(label))
                    {
                        if (IsEditing(data))
                        {
                            StopEditing();
                        }
                        else
                        {
                            StartEditing(data);
                        }
                    }
                }
        
                if (data.MovingAttack.Enabled && !HasPreviewAnchors())
                {
                    EditorGUILayout.HelpBox(
                        "Moving Attack path editing requires PreviewBattleSceneManager's Player Place and Enemy Place. Assign both transforms in PreViewsScene.",
                        MessageType.Info);
                }
                else if (IsEditing(data))
                {
                    EditorGUILayout.HelpBox(
                        "Scene View: drag Entry (blue), Mid (purple), Exit (red). These offsets are stored relative to the enemy formation. Player/Enemy Place are editor-only anchors; runtime recalculates the path from the real caster and formation.",
                        MessageType.Info);
                }
            }
        
            private static void StartEditing(SkillPresentationData data)
            {
                _editingData = data;
                _cacheValid = false;
                Selection.activeObject = data;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        
            private static void StopEditing()
            {
                _editingData = null;
                _cacheValid = false;
            }
        
            private static void OnSelectionChanged()
            {
                if (_editingData != null && Selection.activeObject != _editingData)
                {
                    StopEditing();
                }
            }
        
            private static bool HasPreviewAnchors()
            {
                return TryGetPreviewAnchors(out _, out _);
            }
        
            private static bool TryGetPreviewAnchors(out Transform actor, out Transform target)
            {
                actor = null;
                target = null;
        
                PreviewBattleSceneManager manager =
                            Object.FindFirstObjectByType<PreviewBattleSceneManager>();
                        if (manager == null ||
                            manager.PlayerPreviewAnchor == null ||
                            manager.EnemyPreviewAnchor == null)
                        {
                            return false;
                        }
        
                        actor = manager.PlayerPreviewAnchor;
                        target = manager.EnemyPreviewAnchor;
                        return true;
                    }
        
                    private static void OnSceneGui(SceneView sceneView)
            {
                SkillPresentationData data = _editingData;
                if (data == null || data.MovingAttack == null || !data.MovingAttack.Enabled)
                {
                    return;
                }
        
                if (!TryGetPreviewAnchors(out Transform actor, out Transform target))
                {
                    Handles.BeginGUI();
                    GUILayout.BeginArea(new Rect(12, 12, 420, 36), EditorStyles.helpBox);
                    GUILayout.Label("Moving Attack path: PreviewBattleSceneManager의 Player Place와 Enemy Place를 PreViewsScene Inspector에 연결하세요.");
                    GUILayout.EndArea();
                    Handles.EndGUI();
                    return;
                }
        
                Vector3 actorPosition = actor.position;
                Vector3 formationCenter = target.position;
                MovingAttackPath.Points points = MovingAttackPath.BuildPoints(data.MovingAttack, actorPosition, formationCenter);
                UpdateCurveCache(data, actorPosition, formationCenter, points);
        
                Handles.color = Color.yellow;
                Handles.SphereHandleCap(0, actorPosition, Quaternion.identity, 0.16f, EventType.Repaint);
                Handles.DrawLine(actorPosition, points.Entry);
        
                Handles.color = Color.white;
                Handles.SphereHandleCap(0, formationCenter, Quaternion.identity, 0.13f, EventType.Repaint);
                Handles.Label(formationCenter + Vector3.up * 0.2f, "Formation Center");
        
                Handles.color = new Color(0.67f, 0.24f, 1f, 1f);
                for (int i = 1; i <= SampleCount; i++)
                {
                    Handles.DrawLine(_samples[i - 1], _samples[i]);
                }
                Handles.color = Color.yellow;
                Handles.DrawLine(points.Exit, actorPosition);
        
                EditorGUI.BeginChangeCheck();
                Handles.color = new Color(0.2f, 0.55f, 1f, 1f);
                Vector3 entry = Handles.PositionHandle(points.Entry, Quaternion.identity);
                Handles.Label(points.Entry + Vector3.up * 0.18f, "Entry");
        
                Handles.color = new Color(0.72f, 0.3f, 1f, 1f);
                Vector3 mid = Handles.PositionHandle(points.Mid, Quaternion.identity);
                Handles.Label(points.Mid + Vector3.up * 0.18f, "Mid");
        
                Handles.color = new Color(1f, 0.25f, 0.2f, 1f);
                Vector3 exit = Handles.PositionHandle(points.Exit, Quaternion.identity);
                Handles.Label(points.Exit + Vector3.up * 0.18f, "Exit");
        
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(data, "Edit Moving Attack Path");
                    data.MovingAttack.EntryOffset = MovingAttackPath.ToOffset(entry, actorPosition, formationCenter);
                    data.MovingAttack.MidOffset = MovingAttackPath.ToOffset(mid, actorPosition, formationCenter);
                    data.MovingAttack.ExitOffset = MovingAttackPath.ToOffset(exit, actorPosition, formationCenter);
                    data.MovingAttack.PathSchemaVersion = MovingAttackPresentation.CurrentPathSchemaVersion;
                    EditorUtility.SetDirty(data);
                    _cacheValid = false;
                }
            }
        
            private static void UpdateCurveCache(SkillPresentationData data, Vector3 actor, Vector3 center, MovingAttackPath.Points points)
            {
                MovingAttackPresentation sweep = data.MovingAttack;
                if (_cacheValid && _cachedData == data && _cachedActor == actor && _cachedCenter == center
                    && _cachedEntry == sweep.EntryOffset && _cachedMid == sweep.MidOffset && _cachedExit == sweep.ExitOffset)
                {
                    return;
                }
        
                for (int i = 0; i <= SampleCount; i++)
                {
                    _samples[i] = MovingAttackPath.Evaluate(points, i / (float)SampleCount);
                }
        
                _cachedData = data;
                _cachedActor = actor;
                _cachedCenter = center;
                _cachedEntry = sweep.EntryOffset;
                _cachedMid = sweep.MidOffset;
                _cachedExit = sweep.ExitOffset;
                _cacheValid = true;
            }
        }
        