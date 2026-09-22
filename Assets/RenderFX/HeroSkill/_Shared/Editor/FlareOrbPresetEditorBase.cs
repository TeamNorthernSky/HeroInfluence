using UnityEngine;
using UnityEditor;

namespace JC.VFX
{
    /// <summary>
    /// FlareOrb 계열 프리셋 에디터 공통 베이스 (표준 패턴: 툴팁+적용/캡처+라이브 프리뷰 MPB).
    /// - 라이브 프리뷰 토글 GUI / 적용·캡처 버튼 / 씬 셸 스코프 판정 / 셸 형태 반영·캡처 공통화.
    /// - 파생: FlareOrbPresetEditor(F0), FlareOrbAltPresetEditor(F1) — 경로 상수·전용 재질 Fill만 구현.
    /// 씬 스코프: 셸의 부모 이름에 "Alt" 포함 여부로 F0/F1을 상호 배제한다.
    /// </summary>
    public abstract class FlareOrbPresetEditorBase : Editor
    {
        protected const string DIR = "Assets/RenderFX/HeroSkill/Lumina/FlareBomb";

        protected abstract string LiveKey { get; }
        protected abstract string HelpText { get; }
        protected abstract void LivePush();
        protected abstract void ApplyPreset();
        protected abstract void CapturePreset();

        /// <summary>
        /// 본문 필드 그리기. 기본은 전체 표시이고, 한 프리셋이 여러 대상을 겸하는 경우
        /// 파생 에디터가 「그 프리셋이 실제로 쓰지 않는 섹션」을 걸러내도록 훅으로 열어 둔다.
        /// </summary>
        protected virtual void DrawBody() => DrawDefaultInspector();

        public override void OnInspectorGUI()
        {
            bool live = EditorPrefs.GetBool(LiveKey, true);
            bool newLive = EditorGUILayout.ToggleLeft(new GUIContent("라이브 프리뷰 (씬 인스턴스에 즉시 반영, 비파괴)","현재 프리셋을 사용하는 살아 있는 이펙트에 조절값을 반영합니다. 파일 저장과는 별개입니다."), live);
            if (newLive != live) EditorPrefs.SetBool(LiveKey, newLive);
            EditorGUILayout.Space(2);

            EditorGUI.BeginChangeCheck();
            DrawBody();
            bool changed = EditorGUI.EndChangeCheck();
            if (changed && newLive) LivePush();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(new GUIContent("▶ 프리팹에 적용","이 프리셋에 연결된 부품과 전용 재질에 현재 값을 확정합니다."), GUILayout.Height(30))) ApplyPreset();
                if (GUILayout.Button(new GUIContent("● 현재값 캡처","연결된 부품과 재질의 값을 이 프리셋으로 읽습니다. 파일 저장은 별도입니다."), GUILayout.Height(30))) CapturePreset();
            }
            EditorGUILayout.HelpBox(JcLuminaPresetEditorBridge.IsPartPreset(target) ? "현재 연결된 부품만 적용·캡처합니다. 조절값은 다음 시전부터 반영되며, 라이브 프리뷰를 켜면 현재 인스턴스에도 반영됩니다. 프리뷰와 전투는 같은 에셋을 사용합니다." : HelpText, MessageType.Info);
        }

        protected static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);

        protected static bool IsAltShell(FlareOrbShell shell) =>
            shell.transform.parent != null && shell.transform.parent.name.Contains("Alt");

        /// <summary>흑염(Dark) 변형 소속 여부. 크림판 프리셋의 라이브 푸시가 다크 변형을 덮지 않도록 스코프에서 제외.</summary>
        protected static bool InDarkVariant(Component c) =>
            c != null && (c.transform.root.name.Contains("Dark") || c.GetComponentInParent<JcLuminaPartPresetBinder>() != null);

        // ---------- 셸 형태 공통 처리 ----------

        /// <summary>씬 인스턴스의 셸에 프리셋 형태·자전을 직접 반영(라이브 프리뷰).</summary>
        protected static void ApplyShellToInstance(FlareOrbShell shell, FlareOrbPresetBase p, bool isBack)
        {
            shell.radius = p.shellRadius;
            shell.tipStart = p.shellTipStart;
            shell.tipHeight = p.shellTipHeight;
            shell.tipPower = p.shellTipPower;
            shell.yOffset = p.shellYOffset;
            shell.bodyHeightRatio = p.shellBodyHeightRatio;
            shell.spinSpeed = isBack ? p.backSpinSpeed : p.shellSpinSpeed;
        }

        /// <summary>프리팹 콘텐츠의 모든 셸에 프리셋 형태·자전을 직렬화 반영(확정 적용).</summary>
        protected static void ApplyShellsToPrefab(GameObject orbRoot, FlareOrbPresetBase p)
        {
            foreach (var shell in orbRoot.GetComponentsInChildren<FlareOrbShell>(true))
            {
                bool isBack = shell.gameObject.name.Contains("Back");
                var so = new SerializedObject(shell);
                so.FindProperty("radius").floatValue = p.shellRadius;
                so.FindProperty("tipStart").floatValue = p.shellTipStart;
                so.FindProperty("tipHeight").floatValue = p.shellTipHeight;
                so.FindProperty("tipPower").floatValue = p.shellTipPower;
                so.FindProperty("yOffset").floatValue = p.shellYOffset;
                so.FindProperty("bodyHeightRatio").floatValue = p.shellBodyHeightRatio;
                so.FindProperty("spinSpeed").floatValue = isBack ? p.backSpinSpeed : p.shellSpinSpeed;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary>프리팹의 셸에서 프리셋 형태·자전을 역방향 읽기(캡처).</summary>
        protected static void CaptureShellsFromPrefab(GameObject orbRoot, FlareOrbPresetBase p)
        {
            foreach (var shell in orbRoot.GetComponentsInChildren<FlareOrbShell>(true))
            {
                if (shell.gameObject.name.Contains("Back"))
                {
                    p.backSpinSpeed = shell.spinSpeed;
                    continue;
                }
                p.shellRadius = shell.radius;
                p.shellTipStart = shell.tipStart;
                p.shellTipHeight = shell.tipHeight;
                p.shellTipPower = shell.tipPower;
                p.shellYOffset = shell.yOffset;
                p.shellBodyHeightRatio = shell.bodyHeightRatio;
                p.shellSpinSpeed = shell.spinSpeed;
            }
        }

        /// <summary>스코프 내 셸(+선택적으로 형제 코어/림)의 MPB 오버라이드 해제.</summary>
        protected static void ClearShellOverrides(System.Func<FlareOrbShell, bool> scope, bool includeCoreRim)
        {
            foreach (var shell in Object.FindObjectsByType<FlareOrbShell>(FindObjectsSortMode.None))
            {
                if (!scope(shell)) continue;
                var mr = shell.GetComponent<MeshRenderer>();
                if (mr != null) mr.SetPropertyBlock(null);
                if (!includeCoreRim) continue;
                var root = shell.transform.parent;
                if (root == null) continue;
                foreach (var childName in new[] { "Core_Sphere", "CoreRim" })
                {
                    var c = root.Find(childName);
                    if (c == null) continue;
                    var cmr = c.GetComponent<MeshRenderer>();
                    if (cmr != null) cmr.SetPropertyBlock(null);
                }
            }
        }
    }
}
