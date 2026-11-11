using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting.Editor
{
    // JC | 2026-11-11 | 기존 은행 촬영 기준 무대를 보존하며 저장된 정본1은 모든 전투에서 공유합니다.
    // 임시 편집 씬만 소유하며 원본 씬·카탈로그·재질·조명 프로필은 저장하지 않습니다.
    public sealed class JcBankSkillCameraWindow : EditorWindow
    {
        const string BattleScene = "Assets/_ProtoType_Merge/Scenes/TmpBattleScene.unity";
        static readonly GUIContent[] Slots = {
            new GUIContent("1 · 블랙 데빌 퍼펙트 에임 샷", "기본·강화 3010 계열 구도입니다."),
            new GUIContent("2 · 프론트 라인 브레이크 불릿", "기본·강화 3020 계열 구도입니다."),
            new GUIContent("3 · 트윈 트리거 더블 버스트", "기본·강화 3030 계열 구도입니다."),
            new GUIContent("4 · 데드엔드 라스트 트리거", "기본·강화 3040 계열 구도입니다. 적 6칸별로 별도 구도를 사용합니다.")
        };
        // JC | 2026-11-06 | 화면 방향에 따라 좌우가 바뀌므로 기존 칸 번호를 표시하고 선택 마커를 강조합니다.
        static readonly GUIContent[] Targets = Enumerable.Range(0, 6).Select(i => new GUIContent(
            (i < 3 ? "전열 " : "후열 ") + (i % 3 + 1) + " · Grid_" + (i / 3 + 2) + "_" + (i % 3),
            "적 " + (i < 3 ? "전열" : "후열") + "의 " + (i % 3 + 1) + "번째 칸입니다. 열 번호는 월드 X 증가 방향이며 노란 마커로 위치를 확인하세요.")).ToArray();
        readonly Renderer[] targetMarkers = new Renderer[6];
        Scene previewScene, previousScene;
        Camera editCamera;
        GameObject root, markers;
        RenderTexture image;
        Material allyMaterial, enemyMaterial;
        JcBankBattleBackground bank;
        JcBankSkillCameraProfile source;
        Vector3 framingCenter;
        int slot, targetSlot;
        // JC | 2026-11-06 | 가져올 원본과 현재 저장 대상을 분리하여 다른 구도를 참고해도 목적지가 바뀌지 않게 합니다.
        int copyFromSkill, copyFromTarget, copyDestinations = 14;
        bool showCopyTools = true;
        // JC | 2026-11-06 | 저장 프로필과 별개인 조절값입니다. Transform 수동 이동도 저장 시 역변환합니다.
        float editPerspective = 1f;
        Vector3 editPerspectiveCenter;
        bool guides = true, closing;
        Vector2 scroll;
        string error;

        [MenuItem("JC/전투/블랙불릿 정본1 촬영 툴")]
        public static void Open() => GetWindow<JcBankSkillCameraWindow>("블랙불릿 정본1");

        void OnEnable()
        {
            EditorApplication.playModeStateChanged += PlayState;
            AssemblyReloadEvents.beforeAssemblyReload += ClosePreview;
            EditorSceneManager.sceneClosing += SceneClosing;
        }
        void OnDisable()
        {
            EditorApplication.playModeStateChanged -= PlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= ClosePreview;
            EditorSceneManager.sceneClosing -= SceneClosing;
            ClosePreview();
        }
        void PlayState(PlayModeStateChange state)
        {
            // JC | 2026-11-06 | 사용자가 Play를 시작할 때 편집용 카메라/배경이 본게임에 섞이지 않게 정리합니다.
            if (state == PlayModeStateChange.ExitingEditMode) ClosePreview();
        }
        void SceneClosing(Scene scene, bool removing)
        {
            if (scene == previewScene && !closing) ClosePreview(false);
        }

        static GUIContent Hint(string label, string tooltip) => new GUIContent(label, tooltip);
        void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.HelpBox("은행 전용 4스킬 × 6타겟(24개) 구도 편집입니다. 스킬과 적 칸을 선택하고 카메라 Transform 또는 Scene 뷰로 조절한 뒤 저장하세요. 기본·강화는 같은 스킬 슬롯을 사용합니다. 임시 씬은 저장할 필요가 없습니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (!editCamera)
                {
                    if (GUILayout.Button(Hint("정본1 촬영 무대 열기", "은행 배경과 실제 전투 그리드 위치의 크기 참고용 마커를 별도 임시 씬에 표시합니다. 원본 씬은 보존합니다.")))
                    {
                        try { BuildPreview(); error = null; }
                        catch (Exception e) { error = e.Message; ClosePreview(); Debug.LogException(e); }
                    }
                }
                else DrawControls();
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                EditorGUILayout.HelpBox("이 도구는 에디트 모드에서 사용합니다. 플레이를 종료한 뒤 미리보기를 여세요.", MessageType.Info);
            if (!string.IsNullOrEmpty(error)) EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.EndScrollView();
        }

        void DrawControls()
        {
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(Hint("공유 저장 프로필", "모든 전투가 정본1 선택 시 공유하는 촬영 프로필입니다. 미리보기는 기존 은행 기준 무대를 사용합니다."), source, typeof(JcBankSkillCameraProfile), false);
            int next = EditorGUILayout.Popup(Hint("편집할 스킬", "선택을 바꾸면 해당 스킬의 저장 구도를 불러옵니다. 저장 전 카메라 조정값은 버려집니다."), slot, Slots);
            if (next != slot) { slot = next; LoadShot(); }
            int nextTarget = EditorGUILayout.Popup(Hint("공격할 타겟 칸", "노란색으로 강조한 적 칸의 구도를 편집합니다. 선택 변경 전에 조정값을 저장하세요."), targetSlot, Targets);
            if (nextTarget != targetSlot) { targetSlot = nextTarget; LoadShot(); }
            var saved = source.GetSlot(slot, targetSlot);
            EditorGUILayout.LabelField("저장 대상", Slots[slot].text + " / " + Targets[targetSlot].text);
            EditorGUILayout.LabelField("적용 범위", "정본1을 선택한 모든 전투 · 공유 구도");
            if (GUILayout.Button(Hint("기본 카메라 구도 복원", "공통 기본 A뷰와 은행 X 이동량으로 복원합니다. 스킬 저장값은 변경하지 않습니다."))) RestoreBase();
            if (GUILayout.Button(Hint("선택 스킬·타겟 저장 구도 다시 불러오기", "현재 임시 조정을 버리고 선택 스킬·타겟의 저장 위치·회전·시야각으로 돌아갑니다."))) LoadShot();
            DrawCopyControls();
            if (GUILayout.Button(Hint("편집 카메라 선택 · Transform으로 이동", "Hierarchy의 임시 편집 카메라를 선택합니다. 이동/회전 도구와 Inspector로 직접 조정하세요."))) Selection.activeGameObject = editCamera.gameObject;
            if (GUILayout.Button(Hint("Scene 뷰 → 편집 카메라 가져오기", "마지막 Scene 뷰의 실제 카메라 위치·회전·시야각을 가져오고 원근감 배율은 1로 초기화합니다. 저장은 별도 버튼으로 수행합니다.")))
            {
                var view = SceneView.lastActiveSceneView;
                if (!view || view.orthographic) error = "원근(Perspective) Scene 뷰에서 구도를 잡아 주세요.";
                else
                {
                    Undo.RecordObjects(new UnityEngine.Object[] { editCamera, editCamera.transform }, "은행 Scene 뷰 구도 가져오기");
                    editCamera.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
                    editCamera.fieldOfView = view.camera.fieldOfView;
                    // Scene 뷰는 실제 포즈이므로 배율 1에서 가져와 사용자가 원근감을 다시 선택합니다.
                    editPerspective = 1f; editPerspectiveCenter = framingCenter;
                    error = null;
                }
            }
            // JC | 2026-11-06 | 전열·후열 모두 같은 원근감 조절을 허용하고 선택한 조합만 저장합니다.
            {
                float perspective = EditorGUILayout.Slider(Hint("원근감 배율", "1은 현재 원근감, 낮을수록 원경 적의 상대 크기가 커집니다. 기준면 크기를 유지하도록 카메라를 뒤로 이동하고 FOV를 줄입니다. 전열·후열 구분 없이 선택한 스킬·타겟 조합에만 저장합니다."), editPerspective, .2f, 1f);
                if (perspective != editPerspective)
                {
                    try { ChangePerspective(perspective); error = null; }
                    catch (Exception e) { error = e.Message; }
                }
            }
            EditorGUI.BeginChangeCheck();
            var position = EditorGUILayout.Vector3Field(Hint("카메라 위치", "실제 월드 위치입니다. 미리보기에 즉시 반영하며 저장 전에는 프로필에 쓰지 않습니다."), editCamera.transform.position);
            var rotation = EditorGUILayout.Vector3Field(Hint("카메라 회전", "월드 X/Y/Z 회전(도)입니다. 자동 표적 추적은 적용하지 않습니다."), editCamera.transform.eulerAngles);
            float fov = EditorGUILayout.Slider(Hint("수직 시야각", "실제 FOV(도)입니다. 원근감 배율에 맞는 범위 안에서 조절합니다. 저장 시 기준 화각으로 역변환하여 재적용 오차를 막습니다."), editCamera.fieldOfView, ActualFov(10), ActualFov(100));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObjects(new UnityEngine.Object[] { editCamera, editCamera.transform }, "은행 카메라 조절");
                editCamera.transform.SetPositionAndRotation(position, Quaternion.Euler(rotation));
                editCamera.fieldOfView = fov;
            }
            if (GUILayout.Button(Hint("현재 카메라 → 선택 스킬·타겟에 저장", "선택한 스킬과 적 칸의 조합 한 개만 저장합니다. 나머지 23개 구도와 기본뷰·재질·조명은 유지합니다. 다음 공격 시작부터 반영합니다.")))
            {
                try { SaveShot(); error = null; }
                catch (Exception e) { error = e.Message; }
            }
            guides = EditorGUILayout.Toggle(Hint("HUD 참고 영역 표시", "좌측 약 21%, 우측 약 12%의 HUD 위치 참고 표시입니다. 카메라를 강제로 이동하거나 저장값을 보정하지 않습니다."), guides);
            bool show = EditorGUILayout.Toggle(Hint("전투진형 크기 마커 표시", "본게임 그리드의 모든 배치 가능 칸을 표시합니다. 실제 캐릭터·이펙트·현재 적 편성을 재생하는 기능은 아닙니다."), markers.activeSelf);
            if (show != markers.activeSelf) markers.SetActive(show);
            var rect = GUILayoutUtility.GetAspectRect(16f / 9f);
            if (image) GUI.DrawTexture(rect, image, ScaleMode.ScaleToFit, false);
            DrawTargetLabels(rect);
            if (guides)
            {
                EditorGUI.DrawRect(new Rect(rect.x, rect.y, rect.width * .21f, rect.height), new Color(.2f, .4f, 1, .14f));
                EditorGUI.DrawRect(new Rect(rect.xMax - rect.width * .12f, rect.y, rect.width * .12f, rect.height), new Color(1, .3f, .2f, .2f));
            }
            EditorGUILayout.HelpBox("파랑은 아군 칸, 빨강은 적 칸, 노랑은 현재 선택 타겟입니다. 마커는 신체 크기 참고용이며 상단 이펙트 범위는 실제 플레이에서 확인하세요. 창을 닫거나 Play/컴파일을 시작하면 임시 씬을 정리합니다. 저장 전 값은 사라집니다.", MessageType.None);
            if (GUILayout.Button(Hint("편집 미리보기 닫기", "저장 프로필은 유지하고 임시 씬만 제거합니다. 처음 열려 있던 씬으로 돌아갑니다."))) ClosePreview();
        }

        public void RestoreBase()
        {
            var profile = Resources.Load<JcBattleCameraProfile>("JC_BattleCamera_Default");
            if (!profile) throw new InvalidOperationException("공통 기본 카메라 프로필이 없습니다.");
            profile.viewA.Resolve(framingCenter, out var p, out var q, out var f);
            p.x += bank.viewAHorizontalOffset;
            editCamera.transform.SetPositionAndRotation(p, q); editCamera.fieldOfView = f;
            editPerspective = 1f; editPerspectiveCenter = framingCenter;
        }
        void LoadShot()
        {
            ApplyEditingPose(source.GetSlot(slot, targetSlot));
        }
        void ApplyEditingPose(JcBankSkillCameraProfile.Shot shot)
        {
            shot.Apply(editCamera);
            editPerspective = shot.perspectiveStrength;
            editPerspectiveCenter = editPerspective == 1f ? framingCenter : shot.perspectiveCenter;
            HighlightTarget();
        }
        void DrawCopyControls()
        {
            showCopyTools = EditorGUILayout.Foldout(showCopyTools, Hint("저장값 가져오기 · 다른 스킬로 복사", "현재 편집으로 구도 하나를 가져오거나, 원본 스킬의 6칸을 다른 스킬에 한꺼번에 저장합니다."), true);
            if (!showCopyTools) return;
            copyFromSkill = EditorGUILayout.Popup(Hint("원본 스킬", "이미 저장된 값을 읽을 스킬입니다. 저장 전 미리보기 값은 복사하지 않습니다."), copyFromSkill, Slots);
            copyFromTarget = EditorGUILayout.Popup(Hint("가져올 타겟 칸", "단일 구도 가져오기에만 사용합니다. 아래 6칸 일괄 복사는 이 선택과 무관하게 모든 칸을 대응 복사합니다."), copyFromTarget, Targets);
            if (GUILayout.Button(Hint("선택 원본 구도 → 현재 편집에 불러오기", "위 원본 스킬·타겟의 위치/회전/FOV/원근감을 현재 편집 카메라에 적용합니다. 저장 대상 스킬·타겟은 유지하고 프로필은 변경하지 않습니다. 적용하려면 기존 저장 버튼을 누르세요.")))
            {
                try { ImportSavedShot(copyFromSkill, copyFromTarget); error = null; }
                catch (Exception e) { error = e.Message; }
            }
            copyDestinations = EditorGUILayout.MaskField(Hint("6칸을 덮어쓸 스킬", "하나 또는 여러 스킬을 선택하세요. 원본 스킬은 자동 제외합니다. 각 전열/후열 칸에 대응하는 저장값과 사용 여부를 복사합니다."), copyDestinations, Slots.Select(s => s.text).ToArray());
            int mask = copyDestinations & 15 & ~(1 << copyFromSkill);
            int count = Enumerable.Range(0, 4).Count(i => (mask & (1 << i)) != 0);
            if (count > 0)
                EditorGUILayout.HelpBox("원본: " + Slots[copyFromSkill].text + "\n덮어쓸 대상: " + string.Join(", ", Enumerable.Range(0, 4).Where(i => (mask & (1 << i)) != 0).Select(i => (i + 1) + "번 스킬")) + "\n아래 버튼은 대상의 6칸 전체를 즉시 저장합니다. Unity 실행 취소(Ctrl+Z)를 지원합니다.", MessageType.Info);
            using (new EditorGUI.DisabledScope(count == 0))
                if (GUILayout.Button(Hint("선택한 " + count + "개 스킬의 6칸 덮어쓰기 · 저장", "원본의 6칸을 같은 타겟 칸끼리 복사하고 즉시 저장합니다. 위치·회전·FOV·원근감 배율/기준점가 포함됩니다. 선택하지 않은 스킬과 원본은 보존합니다.")))
                {
                    try { CopySkillTargets(copyFromSkill, mask); error = null; }
                    catch (Exception e) { error = e.Message; }
                }
        }
        public void ImportSavedShot(int fromSkill, int fromTarget)
        {
            // JC | 2026-11-06 | 단일 가져오기는 미리보기만 바꾸고 현재 저장 대상과 모든 프로필 값은 유지합니다.
            var shot = source.GetSlot(fromSkill, fromTarget);
            if (shot == null) throw new ArgumentException("가져올 스킬·타겟 저장값이 없습니다.");
            ApplyEditingPose(shot);
        }
        public void CopySkillTargets(int fromSkill, int destinations)
        {
            // JC | 2026-11-06 | 저장된 원본 6칸을 먼저 복제한 뒤 명시적으로 선택한 목적지에만 대응 복사합니다.
            // 전체 프로필 복사나 카메라 재캡처를 하지 않아 다른 스킬 및 원근감 수치가 변하지 않습니다.
            if (fromSkill < 0 || fromSkill > 3) throw new ArgumentOutOfRangeException(nameof(fromSkill));
            int mask = destinations & 15 & ~(1 << fromSkill);
            if (mask == 0) return;
            var copies = new JcBankSkillCameraProfile.Shot[6];
            for (int i = 0; i < 6; i++)
            {
                copies[i] = source.GetSlot(fromSkill, i)?.Copy();
                if (copies[i] == null) throw new InvalidOperationException("원본 스킬의 6칸 저장값을 확인하세요.");
                for (int destination = 0; destination < 4; destination++)
                    if ((mask & (1 << destination)) != 0 && source.GetSlot(destination, i) == null)
                        throw new InvalidOperationException("대상 스킬의 6칸 저장값을 확인하세요.");
            }
            Undo.IncrementCurrentGroup();
            Undo.RegisterCompleteObjectUndo(source, "은행 스킬 6타겟 카메라 복사");
            for (int destination = 0; destination < 4; destination++)
            {
                if ((mask & (1 << destination)) == 0) continue;
                for (int i = 0; i < 6; i++)
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(copies[i]), source.GetSlot(destination, i));
            }
            SaveProfile();
            if ((mask & (1 << slot)) != 0) LoadShot();
        }
        float ActualFov(float baseFov) => 2f * Mathf.Atan(Mathf.Tan(baseFov * Mathf.Deg2Rad * .5f) * editPerspective) * Mathf.Rad2Deg;
        JcBankSkillCameraProfile.Shot CaptureDraft()
        {
            var draft = new JcBankSkillCameraProfile.Shot { perspectiveStrength = editPerspective };
            draft.Capture(editCamera, editPerspectiveCenter);
            return draft;
        }
        public void ChangePerspective(float strength)
        {
            // JC | 2026-11-06 | 현재 카메라를 이전 배율로 역변환한 뒤 새 배율을 적용하므로 드래그/왕복 조절이 누적되지 않습니다.
            var draft = CaptureDraft();
            draft.perspectiveStrength = Mathf.Clamp(strength, .2f, 1f);
            draft.Apply(editCamera); editPerspective = draft.perspectiveStrength;
        }
        void HighlightTarget()
        {
            // JC | 2026-11-06 | 원본/공유 재질은 변경하지 않고 선택 적 칸에만 노란색 표시를 덧씁니다.
            for (int i = 0; i < targetMarkers.Length; i++)
            {
                if (!targetMarkers[i]) continue;
                var block = new MaterialPropertyBlock();
                if (i == targetSlot) block.SetColor("_BaseColor", new Color(1f, .75f, .05f));
                targetMarkers[i].SetPropertyBlock(block);
            }
        }
        void DrawTargetLabels(Rect rect)
        {
            if (!markers.activeSelf) return;
            for (int i = 0; i < targetMarkers.Length; i++)
            {
                if (!targetMarkers[i]) continue;
                var p = editCamera.WorldToViewportPoint(targetMarkers[i].bounds.center + Vector3.up * 1.2f);
                if (p.z <= 0 || p.x < 0 || p.x > 1 || p.y < 0 || p.y > 1) continue;
                var label = new Rect(rect.x + rect.width * p.x - 30, rect.y + rect.height * (1 - p.y) - 9, 60, 18);
                GUI.Label(label, (i < 3 ? "전열 " : "후열 ") + (i % 3 + 1), EditorStyles.whiteMiniLabel);
            }
        }
        public void SaveShot()
        {
            // JC | 2026-11-06 | 명시 저장만 원본에 반영하며 선택한 스킬×타겟 조합만 저장하고 다른 23개 구도는 덮어쓰지 않습니다.
            var draft = CaptureDraft();
            Undo.RecordObject(source, "은행 블랙불릿 스킬 구도 저장");
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(draft), source.GetSlot(slot, targetSlot));
            SaveProfile();
        }
        void SaveProfile() { EditorUtility.SetDirty(source); AssetDatabase.SaveAssetIfDirty(source); }

        public void BuildPreview()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("에디트 모드에서만 열 수 있습니다.");
            ClosePreview();
            // JC | 2026-11-11 | 촬영 무대만 은행 기준을 유지하고 저장 연결은 전전투 공통 프로필에서 가져옵니다.
            bank = JcBankBattleBackground.Load();
            var shared = Resources.Load<JcBlackBulletCameraProfile>("JC_BlackBulletCamera_Default");
            source = shared ? shared.authoredProfile : null;
            if (!bank || !bank.visualPrefab || !source || !bank.materialProfile || !bank.environmentProfile)
                throw new InvalidOperationException("은행 배경·카메라·재질·조명 프로필 연결을 확인해 주세요.");
            // JC | 2026-11-06 | 원본 전투씬은 읽기용으로 추가 열어 진형 좌표만 복사합니다.
            // 카탈로그/전투 매니저를 실행하거나 별도 진형 데이터를 하드코딩하지 않습니다.
            var cells = new List<(string name, Vector3 position, bool ally, int target)>();
            var beforeLayout = SceneManager.GetActiveScene();
            var layout = SceneManager.GetSceneByPath(BattleScene);
            bool openedLayout = !layout.IsValid() || !layout.isLoaded;
            if (openedLayout) layout = EditorSceneManager.OpenScene(BattleScene, OpenSceneMode.Additive);
            try
            {
                var roots = layout.GetRootGameObjects();
                var player = roots.Single(r => r.name == "PlayerPlace");
                var enemy = roots.Single(r => r.name == "EnemyPlace");
                framingCenter = (player.transform.position + enemy.transform.position) * .5f;
                var playerCells = player.GetComponentsInChildren<ASB.Work.BattleGrid.GridCell>(true);
                var shift = (playerCells.Single(c => c.name == "Grid_0_1").transform.position -
                    playerCells.Single(c => c.name == "Grid_0_0").transform.position) * .5f;
                foreach (var side in new[] { player, enemy })
                foreach (var cell in side.GetComponentsInChildren<ASB.Work.BattleGrid.GridCell>(true))
                    cells.Add((side.name + "_" + cell.name, cell.transform.position + shift, side == player,
                        side == enemy ? JcBankSkillCameraProfile.TargetIndex(cell.Coords) : -1));
            }
            finally
            {
                if (openedLayout) EditorSceneManager.CloseScene(layout, true);
                if (beforeLayout.IsValid() && beforeLayout.isLoaded) SceneManager.SetActiveScene(beforeLayout);
            }
            previousScene = SceneManager.GetActiveScene();
            previewScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            previewScene.name = "JC_은행_스킬카메라_임시편집";
            SceneManager.SetActiveScene(previewScene);
            root = new GameObject("JC_BankSkillCamera_편집용_저장불필요");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(bank.visualPrefab, previewScene);
            visual.transform.SetParent(root.transform, false);
            JC.Environment.JcContactShadingController contact = null;
            if (bank.contactShadingProfile)
            {
                contact = root.AddComponent<JC.Environment.JcContactShadingController>();
                contact.Initialize(bank.contactShadingProfile, visual.transform);
            }
            root.AddComponent<JcBankBackgroundMaterials>().Initialize(visual, bank.materialProfile, contact,
                bank.toonProfile ? bank.toonProfile.surfaceShader : null);
            root.AddComponent<JcBankEnvironment>().Initialize(bank.environmentProfile, visual.transform);
            if (bank.toonProfile) root.AddComponent<JcBankToonController>().Initialize(bank.toonProfile);
            var cameraObject = new GameObject("JC_BankSkillCamera_Edit · 직접 이동 후 저장");
            cameraObject.transform.SetParent(root.transform, false);
            editCamera = cameraObject.AddComponent<Camera>(); editCamera.enabled = false;
            editCamera.aspect = 16f / 9f; editCamera.nearClipPlane = .1f; editCamera.farClipPlane = 300;
            editCamera.clearFlags = CameraClearFlags.SolidColor; editCamera.backgroundColor = Color.gray;
            cameraObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            bank.ApplyCamera(editCamera);
            markers = new GameObject("전투진형 크기 참고 · 실제 편성 아님"); markers.transform.SetParent(root.transform, false);
            allyMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(.2f, .5f, .85f), hideFlags = HideFlags.DontSave };
            enemyMaterial = new Material(allyMaterial) { color = new Color(.8f, .25f, .2f) };
            foreach (var cell in cells)
            {
                var marker = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                marker.name = cell.name; marker.transform.SetParent(markers.transform, false);
                marker.transform.position = cell.position + Vector3.up;
                marker.transform.localScale = new Vector3(1.2f, 1, 1.2f);
                DestroyImmediate(marker.GetComponent<Collider>());
                var renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = cell.ally ? allyMaterial : enemyMaterial;
                if (cell.target >= 0) targetMarkers[cell.target] = renderer;
            }
            image = new RenderTexture(960, 540, 24) { hideFlags = HideFlags.DontSave, name = "은행 카메라 편집 미리보기" };
            editCamera.targetTexture = image;
            RestoreBase(); HighlightTarget(); RenderPreview();
        }

        void OnInspectorUpdate() { RenderPreview(); Repaint(); }
        public void RenderPreview()
        {
            if (!editCamera || !previewScene.IsValid() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            // JC | 2026-11-06 | 임시 씬 이외의 열린 씬은 렌더에서 제외하여 배경/광원이 겹치지 않게 합니다.
            editCamera.scene = previewScene;
            editCamera.Render();
        }
        public void ClosePreview() => ClosePreview(true);
        void ClosePreview(bool closeScene)
        {
            if (closing) return;
            closing = true;
            try
            {
                if (editCamera) editCamera.targetTexture = null;
                // 환경 컴포넌트가 자신의 씬에서 원래 글로벌 설정을 복원한 후 씬을 제거합니다.
                if (previewScene.IsValid() && previewScene.isLoaded) SceneManager.SetActiveScene(previewScene);
                if (root)
                {
                    root.GetComponent<JcBankToonController>()?.ReleaseEditorPreview();
                    root.GetComponent<JC.Environment.JcContactShadingController>()?.ReleaseEditorPreview();
                    root.GetComponent<JcBankEnvironment>()?.ReleaseEditorPreview();
                    root.GetComponent<JcBankBackgroundMaterials>()?.ReleaseEditorPreview();
                    DestroyImmediate(root);
                }
                if (closeScene && previewScene.IsValid() && previewScene.isLoaded) EditorSceneManager.CloseScene(previewScene, true);
                if (previousScene.IsValid() && previousScene.isLoaded) SceneManager.SetActiveScene(previousScene);
                if (image) { image.Release(); DestroyImmediate(image); }
                if (allyMaterial) DestroyImmediate(allyMaterial);
                if (enemyMaterial) DestroyImmediate(enemyMaterial);
                Array.Clear(targetMarkers, 0, targetMarkers.Length);
                previewScene = default; previousScene = default; root = null; editCamera = null; image = null;
            }
            finally { closing = false; }
        }
    }

    // JC | 2026-11-06 | 기존 BankTestScene 진입 오브젝트에서 편집 도구를 찾을 수 있게 연결합니다.
    [CustomEditor(typeof(JcBankBattleTest))]
    public sealed class JcBankBattleTestEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button(new GUIContent("블랙불릿 4개 스킬 카메라 편집", "은행 배경·진형을 에디트 모드에서 미리 보고 스킬별 카메라 구도를 저장하는 창을 엽니다."))) JcBankSkillCameraWindow.Open();
        }
    }
}
