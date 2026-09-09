using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using JC.VFX;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 환경 컨트롤러 인스펙터 — 프로파일을 <b>인라인으로 편집</b>하고 값이 바뀌면 즉시 씬에 반영한다(라이브 프리뷰).
    ///
    /// ★260902 구조 개편: 하늘 파라미터를 프로젝트 창의 재질에서 찾아 만지는 대신
    ///   씬 하이어라키에서 JC_Environment 하나만 선택하면 전부 조절된다.
    ///   재질은 출력물이므로 직접 열 일이 없다(이중 정본 제거).
    ///
    /// 저장 규격은 [[reference-unity-so-live-tuning]] 그대로 — 조절값은 메모리, 디스크 확정은 명시 💾.
    /// 단 하늘 재질도 함께 갱신되므로 💾 는 <b>프로파일 + 하늘 재질</b>을 같이 저장한다.
    /// </summary>
    [CustomEditor(typeof(JcEnvironmentController))]
    public class JcEnvironmentControllerEditor : Editor
    {
        private const string FoldKey = "JC.Env.InlineProfileFold";
        private SerializedObject _profileSO;

        /// <summary>
        /// ★260904 초안(draft) 도입 — 인라인 편집은 <b>프로파일 원본이 아니라 이 사본</b>을 고친다.
        ///
        /// 왜: 이전에는 슬라이더를 만지는 즉시 <c>SerializedObject.ApplyModifiedProperties()</c> 가
        ///   프로파일 에셋을 고치고 dirty 까지 찍었다. 그래서 Ctrl+S(씬 저장)에 프로파일이 딸려 들어가
        ///   💾 게이트가 새고, 무엇보다 <b>되돌릴 방법이 없어 실험용으로 값을 막 굴릴 수 없었다</b>.
        ///   이제 원본은 [● 캡처] 를 누르기 전까지 변하지 않는다.
        ///
        /// 수명: 인스펙터는 선택만 바꿔도 재생성되므로 초안을 인스턴스 필드로 두면 곧바로 날아간다
        ///   (씬은 조절된 상태인데 인스펙터만 프로파일 값을 보여 주는 어긋남이 생긴다).
        ///   그래서 컨트롤러별 정적 캐시로 들고 있고, 도메인 리로드 등으로 비면
        ///   <b>프로파일이 아니라 「지금 씬 상태」로 다시 세운다</b>(EnsureDraft) — 화면과 인스펙터가 늘 같은 것을 가리키게.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, JcEnvironmentProfile> Drafts = new();

        private JcEnvironmentProfile _draft;

        /// <summary>
        /// 하늘 파라미터 필드 ↔ 셰이더 프로퍼티 대응.
        /// ★스카이박스 셰이더마다 가진 프로퍼티가 다르다(Procedural은 대기 산란 계열을 갖지만 Cubemap 은 _Exposure·_Rotation·_Tint 뿐).
        /// 컨트롤러는 <c>HasProperty</c> 가드로 없는 프로퍼티를 조용히 건너뛰므로, 인스펙터에 보이지만 실제로는
        /// 아무 일도 하지 않는 필드가 생긴다 — 그 필드를 회색 처리해 "지금 이 하늘에서는 무효"임을 드러낸다.
        /// 하늘 재질이 바뀌면 판정도 자동으로 따라간다(매 인스펙터 갱신마다 재판정).
        /// </summary>
        private static readonly (string field, string shaderProp)[] SkyParamMap =
        {
            ("skyTint",             "_SkyTint"),
            ("skyGroundColor",      "_GroundColor"),
            ("atmosphereThickness", "_AtmosphereThickness"),
            ("skyExposure",         "_Exposure"),
            ("sunSize",             "_SunSize"),
            ("sunSizeConvergence",  "_SunSizeConvergence"),
        };

        /// <summary>이 필드가 지금 하늘에서 무효인가. 무효면 사유를 함께 돌려준다.</summary>
        private static bool IsSkyParamInactive(string fieldName, JcEnvironmentProfile p, out string reason)
        {
            reason = null;

            // ①-b 정합 필드는 driveSkyboxParams 와 무관하다 — 하늘에 _Rotation 이 있느냐만 본다.
            //     (HDRI 를 쓸 때 driveSkyboxParams 는 꺼 두는 것이 정석인데, 정합이 필요한 상황이 바로 그때다)
            if (fieldName == "alignSkyToSun" || fieldName == "skySunAzimuth")
            {
                var mat = p.skyboxMaterial;
                if (mat == null) { reason = "하늘 재질 없음"; return true; }
                if (!mat.HasProperty("_Rotation")) { reason = $"{mat.shader.name} 미지원"; return true; }
                if (fieldName == "skySunAzimuth" && !p.alignSkyToSun) { reason = "정합 꺼짐"; return true; }
                return false;
            }

            string prop = null;
            foreach (var e in SkyParamMap)
                if (e.field == fieldName) { prop = e.shaderProp; break; }
            if (prop == null) return false;          // 하늘 파라미터가 아님 — 판정 대상 아님

            if (!p.driveSkyboxParams)
            {
                reason = "기록 꺼짐";
                return true;
            }
            var m = p.skyboxMaterial;
            if (m == null)
            {
                reason = "하늘 재질 없음";
                return true;
            }
            if (!m.HasProperty(prop))
            {
                reason = $"{m.shader.name} 미지원";
                return true;
            }
            return false;
        }

        /// <summary>
        /// 편집용 초안을 준비한다 — <b>프로파일 사본</b>으로만 뜬다.
        ///
        /// ★씬에서 역산해 채우지 않는다(260904 실측): <c>CaptureInto</c> 는 재질의 <c>_Rotation</c> 과
        ///   라이트 쿼터니언을 거쳐 값을 되돌리므로 부동소수점 왕복 오차가 남고(예: skySunAzimuth 36.04957),
        ///   오일러는 [0,360) 로 정규화돼 −5 가 355 로 바뀌기도 한다. 그대로 두면 <b>편집이 없어도 「편집 중」</b>이 뜬다.
        ///   초안을 프로파일 사본으로만 두면 차이 = 진짜 편집이 되어 판정이 정확해진다.
        ///   인스펙터 밖에서 라이트를 직접 스윕한 경우는 [● 캡처] 가 그때 씬을 흡수하므로 잃지 않는다.
        ///
        /// ⚠도메인 리로드(스크립트 컴파일·플레이 진입)가 나면 초안은 사라지고 프로파일 값으로 되돌아간다.
        ///   씬에 이미 나가 있는 값은 그대로이므로, 어긋났다면 [▶ 적용] 한 번이면 맞는다.
        /// </summary>
        private void EnsureDraft(JcEnvironmentController c)
        {
            int key = c.GetInstanceID();
            Drafts.TryGetValue(key, out var cached);

            // 잘못된 플래그로 만들어진 초안이 남아 있으면 편집 가능 상태로 복구
            if (cached != null && (cached.hideFlags & HideFlags.NotEditable) != 0)
                cached.hideFlags = HideFlags.DontSave;

            if (cached == null)
            {
                cached = ScriptableObject.CreateInstance<JcEnvironmentProfile>();
                EditorUtility.CopySerialized(c.Profile, cached);
                // ★DontSave 를 쓸 것 — HideAndDontSave 에는 NotEditable 이 들어 있어
                //   초안이 읽기 전용이 되고 인라인 필드가 전부 잠긴다(260904 실증).
                //   플래그는 CopySerialized 뒤에 세운다(플래그도 복사되기 때문).
                cached.hideFlags = HideFlags.DontSave;
                cached.name = c.Profile.name;
                Drafts[key] = cached;
                _draft = null;                                  // 아래에서 SerializedObject 를 새로 문다
            }

            if (_draft == cached && _profileSO != null) return;
            _draft = cached;
            _profileSO = new SerializedObject(_draft);
        }

        /// <summary>초안이 프로파일과 다른가 = 아직 담기지 않은 편집이 있는가.</summary>
        private bool HasPendingEdits(JcEnvironmentController c)
        {
            if (_draft == null || c.Profile == null) return false;
            var a = new SerializedObject(_draft).GetIterator();
            var b = new SerializedObject(c.Profile).GetIterator();
            bool enter = true;
            while (a.NextVisible(enter) & b.NextVisible(enter))
            {
                enter = false;
                if (a.propertyPath == "m_Script") continue;
                if (!SerializedProperty.DataEquals(a, b)) return true;
            }
            return false;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var c = (JcEnvironmentController)target;

            EditorGUILayout.Space(6);

            if (c.Profile == null)
            {
                EditorGUILayout.HelpBox("Profile 슬롯에 환경 프로파일(ENV_*)을 꽂아 주세요.", MessageType.Warning);
                return;
            }

            // ── 초안 인라인 편집 (씬에는 라이브 반영 / 프로파일은 캡처 전까지 무변경) ──
            EnsureDraft(c);
            bool pending = HasPendingEdits(c);

            bool fold = EditorPrefs.GetBool(FoldKey, true);
            bool newFold = EditorGUILayout.Foldout(fold,
                $"환경 프로파일 — {c.Profile.name}{(pending ? "  ● 편집 중" : "")}  (값 변경 시 즉시 씬 반영)",
                true, EditorStyles.foldoutHeader);
            if (newFold != fold) EditorPrefs.SetBool(FoldKey, newFold);

            if (newFold)
            {
                _profileSO.Update();
                EditorGUI.BeginChangeCheck();

                int inactiveCount = 0;
                string lastReason = null;

                using (new EditorGUI.IndentLevelScope())
                {
                    var it = _profileSO.GetIterator();
                    bool enter = true;
                    while (it.NextVisible(enter))
                    {
                        enter = false;
                        if (it.propertyPath == "m_Script") continue;

                        // 현재 하늘에서 무효인 파라미터는 회색 + 사유 접미. 유효해지면 자동으로 원상 복귀한다.
                        bool inactive = IsSkyParamInactive(it.propertyPath, _draft, out string reason);
                        if (inactive) { inactiveCount++; lastReason = reason; }

                        var label = new GUIContent(
                            inactive ? $"{it.displayName}  ({reason})" : it.displayName,
                            it.tooltip);

                        using (new EditorGUI.DisabledScope(inactive))
                            EditorGUILayout.PropertyField(it, label, true);
                    }
                }

                if (inactiveCount > 0)
                    EditorGUILayout.HelpBox(
                        $"회색 필드 {inactiveCount}개는 지금 하늘에서 적용되지 않습니다 ({lastReason}).\n" +
                        "하늘 재질을 바꾸거나 Drive Skybox Params 를 켜면 자동으로 다시 활성화됩니다.",
                        MessageType.Info);

                if (EditorGUI.EndChangeCheck())
                {
                    _profileSO.ApplyModifiedProperties();   // ★초안에만 쓴다 — 프로파일 원본은 그대로
                    ApplyLive(c);   // ★슬라이더를 움직이는 즉시 씬이 따라온다
                }
            }

            DrawSunAlignSection(c);

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "게이트 2단: 위 값 조절은 씬에만 반영된다 → [● 캡처]로 프로파일에 담고 → [💾]로 파일 확정.\n" +
                "캡처 전까지 프로파일은 변하지 않으므로 값을 마음껏 굴려도 되고, [▶ 적용]이 되돌리기다.\n" +
                "★순서 권장: 태양 강도를 0으로 내리고 하늘(①)만으로 기저 밝기를 잡은 뒤, 태양(③)을 올린다.\n" +
                "※플레이 진입 시에는 프로파일 값이 적용된다(캡처 안 한 편집은 반영되지 않음).",
                MessageType.None);

            if (pending)
                EditorGUILayout.HelpBox(
                    "● 편집 중 — 지금 화면의 값이 프로파일에 담기지 않았습니다.\n" +
                    "[● 캡처]를 눌러야 프로파일에 들어가고, [▶ 적용]을 누르면 편집분을 버리고 프로파일 상태로 되돌립니다.",
                    MessageType.Info);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(pending ? "▶ 적용 — 프로파일 → 씬 (편집분 버림)" : "▶ 적용 — 프로파일 → 씬",
                        GUILayout.Height(28)))
                    RevertToProfile(c);

                if (GUILayout.Button("● 캡처 — 씬 → 프로파일", GUILayout.Height(28)))
                    CaptureToProfile(c);
            }

            // 💾 — 프로파일 + 하늘 재질을 함께 확정
            if (GUILayout.Button(pending ? "💾 디스크 저장 (⚠ 미캡처 편집분은 빠짐)" : "💾 디스크 저장 (프로파일 + 하늘 재질)",
                    GUILayout.Height(30)))
            {
                if (pending)
                    Debug.LogWarning("[JcEnvironment] 담기지 않은 편집이 있습니다 — 지금 저장하면 " +
                                     "화면의 값이 아니라 프로파일에 담긴 값이 기록됩니다. [● 캡처] 후 다시 💾 하세요.", c.Profile);
                SaveProfileAndSky(c);
            }
        }

        /// <summary>편집분을 버리고 프로파일 상태로 되돌린다(초안 재생성 + 씬 재적용).</summary>
        private void RevertToProfile(JcEnvironmentController c)
        {
            c.ApplyNow();                                   // 씬·하늘 재질을 프로파일 값으로 되돌린다
            if (_draft != null) Object.DestroyImmediate(_draft);
            Drafts.Remove(c.GetInstanceID());
            _draft = null; _profileSO = null;
            EnsureDraft(c);
            MarkSceneDirty(c);
            SceneView.RepaintAll();
        }

        /// <summary>씬 상태를 초안에 담고, 그 초안을 프로파일로 옮긴다(메모리 — 디스크는 💾).</summary>
        private void CaptureToProfile(JcEnvironmentController c)
        {
            c.CaptureInto(_draft);      // 인스펙터 밖에서 라이트를 직접 스윕한 경우까지 흡수

            Undo.RecordObject(c.Profile, "Capture Environment");
            string keepName = c.Profile.name;               // ★CopySerialized 는 이름도 덮는다 — 에셋명이 깨지지 않게 보존
            EditorUtility.CopySerialized(_draft, c.Profile);
            c.Profile.name = keepName;
            EditorUtility.SetDirty(c.Profile);

            _profileSO = new SerializedObject(_draft);
            Debug.Log($"[JcEnvironment] 씬 라이팅을 '{c.Profile.name}' 에 캡처(메모리). 확정은 💾.", c.Profile);
        }

        /// <summary>
        /// ①-b 하늘↔태양 방위 정합 — 지금 어떤 각도가 나가는지 수치로 보여 주고, 실측 버튼을 단다.
        /// ★고도 불일치는 _Rotation 으로 못 고치므로(Y축 회전뿐) 경고로 드러낸다 — 조용히 어긋난 채 두지 않는다.
        /// </summary>
        private void DrawSunAlignSection(JcEnvironmentController c)
        {
            var p = _draft;                 // 표시·실측 모두 초안 기준(화면에 나가 있는 값)
            if (p == null) return;
            var mat = p.skyboxMaterial;
            bool supported = mat != null && mat.HasProperty("_Rotation");

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("하늘↔태양 방위 정합", EditorStyles.boldLabel);

            if (!supported)
            {
                EditorGUILayout.HelpBox(mat == null
                    ? "하늘 재질이 없어 정합을 쓸 수 없습니다."
                    : $"'{mat.shader.name}' 에는 _Rotation 이 없어 정합 대상이 아닙니다.\n" +
                      "Procedural 하늘은 태양 원반이 Sun Source 를 따라가므로 이미 정합 상태입니다.",
                    MessageType.None);
                return;
            }

            var sunRot = c.ResolveSunRotation(p);
            float sunAz = JcEnvironmentController.SunAzimuthOf(sunRot);
            float sunEl = JcEnvironmentController.SunElevationOf(sunRot);
            float rot = Mathf.Repeat(p.skySunAzimuth - sunAz, 360f);

            EditorGUILayout.LabelField(
                $"태양 방위 {sunAz:F1}°  −  하늘 태양 기준 {p.skySunAzimuth:F1}°  →  _Rotation {rot:F1}°" +
                (p.alignSkyToSun ? "" : "   ※정합 꺼짐 — 미적용"));

            float dEl = sunEl - p.skySunElevation;
            if (p.alignSkyToSun && Mathf.Abs(dEl) > 10f)
                EditorGUILayout.HelpBox(
                    $"고도가 {dEl:+0.0;-0.0}° 어긋나 있습니다 — 하늘의 태양은 {p.skySunElevation:F1}°, 라이트는 {sunEl:F1}°.\n" +
                    "_Rotation 은 Y축 회전뿐이라 고도는 맞출 수 없습니다. 맞추려면 태양(③)의 회전 X 를 " +
                    $"{p.skySunElevation:F0} 근처로 내려야 합니다(그림자가 그만큼 길어집니다).",
                    MessageType.Warning);

            if (GUILayout.Button("🔎 태양 자동 탐색 — 하늘 그림에서 실측", GUILayout.Height(24)))
                RunSunFinder(c);
        }

        /// <summary>하늘 그림 속 태양을 실측해 프로파일 기준값에 담는다(메모리 — 확정은 💾).</summary>
        private void RunSunFinder(JcEnvironmentController c)
        {
            var p = _draft;                 // ★초안에 담는다 — 캡처 전까지 프로파일은 그대로
            if (p == null) return;

            var res = JcSkySunFinder.Find(p.skyboxMaterial);
            if (!res.ok)
            {
                Debug.LogWarning("[JcEnvironment] 태양 탐색 실패 — 하늘 재질을 확인해 주세요.", c);
                return;
            }

            p.skySunAzimuth = res.azimuth;
            p.skySunElevation = res.elevation;
            _profileSO = new SerializedObject(p);   // 인라인 표시 갱신
            ApplyLive(c);

            Debug.Log($"[JcEnvironment] 하늘 태양 실측 — 방위 {res.azimuth:F1}° / 고도 {res.elevation:F1}° " +
                      $"(피크 휘도 {res.luminance:F0}, 주변 하늘 대비 {res.contrast:F0}배). " +
                      "초안에 담겼습니다 — 프로파일 반영은 [● 캡처].", c);

            if (res.contrast < 3f)
                Debug.LogWarning("[JcEnvironment] 하늘에 태양이라 부를 만한 밝은 점이 없습니다 — " +
                                 "흐린 하늘 HDRI 라면 방위 정합의 의미가 적습니다.", c);
        }

        /// <summary>외부(프로파일 자산 인스펙터 등)에서 씬을 직접 바꿨을 때 초안을 무효화한다.</summary>
        internal static void InvalidateDraft(JcEnvironmentController c)
        {
            if (c == null) return;
            int key = c.GetInstanceID();
            if (Drafts.TryGetValue(key, out var d) && d != null) Object.DestroyImmediate(d);
            Drafts.Remove(key);
        }

        /// <summary>초안 값을 즉시 씬에 반영. 재질도 갱신되므로 dirty 마킹까지 함께.</summary>
        private void ApplyLive(JcEnvironmentController c)
        {
            c.Apply(_draft != null ? _draft : c.Profile);

            var sky = RenderSettings.skybox;
            if (sky != null && !Application.isPlaying && AssetDatabase.Contains(sky))
                EditorUtility.SetDirty(sky);   // 디스크 확정은 💾 에서

            MarkSceneDirty(c);
            SceneView.RepaintAll();
            EditorApplication.QueuePlayerLoopUpdate();   // 게임뷰도 즉시 갱신
        }

        private static void SaveProfileAndSky(JcEnvironmentController c)
        {
            if (c.Profile != null)
            {
                EditorUtility.SetDirty(c.Profile);
                AssetDatabase.SaveAssetIfDirty(c.Profile);
            }

            var sky = c.Profile != null ? c.Profile.skyboxMaterial : null;
            if (sky != null && AssetDatabase.Contains(sky))
            {
                EditorUtility.SetDirty(sky);
                AssetDatabase.SaveAssetIfDirty(sky);
            }

            Debug.Log($"[JcEnvironment] 디스크 저장 완료 — 프로파일 '{(c.Profile ? c.Profile.name : "없음")}'" +
                      $" / 하늘 재질 '{(sky ? sky.name : "없음")}'", c.Profile);
        }

        /// <summary>
        /// ★스크립트가 바꾼 씬 값(RenderSettings·라이트)은 Unity 가 dirty 로 자동 인식하지 못한다 —
        /// dirty 가 아니면 Ctrl+S 가 「변경 없음」으로 저장을 건너뛰어, 재로드 시 값이 되돌아간다(260811 실증).
        /// 에디트 모드의 적용 경로는 반드시 씬을 명시 dirty 마킹한다.
        /// </summary>
        internal static void MarkSceneDirty(Component ctx)
        {
            if (Application.isPlaying || ctx == null) return;
            EditorSceneManager.MarkSceneDirty(ctx.gameObject.scene);
        }
    }

    /// <summary>프로파일 자산 인스펙터 — 열린 씬의 컨트롤러를 찾아 적용 + 💾.</summary>
    [CustomEditor(typeof(JcEnvironmentProfile))]
    public class JcEnvironmentProfileEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var p = (JcEnvironmentProfile)target;

            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox(
                "이 프로파일은 씬의 JC_Environment(컨트롤러) 인스펙터에서 인라인으로 편집하는 것이 기본 흐름입니다 — " +
                "거기서는 값 변경이 즉시 씬에 반영됩니다.", MessageType.None);

            if (GUILayout.Button("▶ 적용 — 열린 씬에 반영", GUILayout.Height(26)))
            {
                var c = Object.FindFirstObjectByType<JcEnvironmentController>(FindObjectsInactive.Include);
                if (c != null)
                {
                    c.Apply(p);
                    JcEnvironmentControllerEditor.InvalidateDraft(c);   // 컨트롤러 쪽 초안이 낡지 않게
                    JcEnvironmentControllerEditor.MarkSceneDirty(c);
                    SceneView.RepaintAll();
                }
                else Debug.LogWarning("[JcEnvironment] 씬에 JcEnvironmentController 가 없습니다 — GO 에 붙여 주세요.");
            }
            JcPresetEditorUtil.DrawSaveButton(p, wide: true);
        }
    }
}
