using System;
using System.Collections;
using UnityEngine;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 프리뷰 씬 단축키 — 인스펙터에서 컨트롤러를 띄우지 않고 스킬을 재생/초기화한다.
    ///
    /// 기본 배치: Q/W/E/R = 저스티스 4종, T = 초기화.
    /// 재생 중에 다른 키를 누르면 먼저 초기화한 뒤 한 프레임 쉬고 재생한다
    /// (PreviewResetGuard가 고아 시퀀스를 끊을 틈을 준다).
    /// </summary>
    public class JusticePreviewHotkeys : MonoBehaviour
    {
        [Serializable]
        public class Binding
        {
            [Tooltip("누를 키.")]
            public KeyCode key = KeyCode.Q;
            [Tooltip("재생할 SkillData.skillIndex.")]
            public int skillIndex = 1010;
            [Tooltip("화면 안내에 표시할 이름.")]
            public string label = "";
        }

        [Header("스킬 키")]
        [SerializeField]
        private Binding[] bindings =
        {
            new Binding { key = KeyCode.Q, skillIndex = 1010, label = "저스티스 등장!" },
            new Binding { key = KeyCode.W, skillIndex = 1020, label = "저스티스 펀치" },
            new Binding { key = KeyCode.E, skillIndex = 1030, label = "저스티스 대쉬" },
            new Binding { key = KeyCode.R, skillIndex = 1040, label = "저스티스 크래쉬" },
        };

        [Header("초기화")]
        [Tooltip("프리뷰를 초기 상태로 되돌리는 키.")]
        [SerializeField] private KeyCode resetKey = KeyCode.T;

        [Header("화면 안내")]
        [Tooltip("게임 뷰 좌상단에 키 안내를 표시한다.")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private int overlayFontSize = 13;

        private SkillPresentationPreviewController preview;
        private GUIStyle style;

        private SkillPresentationPreviewController Preview
        {
            get
            {
                if (preview == null) preview = FindFirstObjectByType<SkillPresentationPreviewController>();
                return preview;
            }
        }

        private void Update()
        {
            if (Preview == null) return;

            if (Input.GetKeyDown(resetKey))
            {
                Preview.ResetPreview();
                return;
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                Binding b = bindings[i];
                if (b == null || !Input.GetKeyDown(b.key)) continue;
                StartCoroutine(PlayRoutine(b.skillIndex));
                return;
            }
        }

        private IEnumerator PlayRoutine(int skillIndex)
        {
            if (Preview.IsPlaying)
            {
                Preview.ResetPreview();
                yield return null;   // PreviewResetGuard가 고아 시퀀스를 끊을 한 프레임
            }

            Preview.SetSelectedSkillIndex(skillIndex);
            Preview.PlaySelectedSkill();
        }

        private void OnGUI()
        {
            if (!showOverlay) return;

            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { fontSize = overlayFontSize, richText = true };
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < bindings.Length; i++)
            {
                Binding b = bindings[i];
                if (b == null) continue;
                sb.Append("<b>").Append(b.key).Append("</b>  ").Append(b.skillIndex);
                if (!string.IsNullOrEmpty(b.label)) sb.Append("  ").Append(b.label);
                sb.AppendLine();
            }
            sb.Append("<b>").Append(resetKey).Append("</b>  초기화");

            string state = Preview != null && Preview.IsPlaying ? "  <color=#ffd24a>[재생 중]</color>" : "";
            GUI.Label(new Rect(12, 10, 460, 140), sb + state, style);
        }
    }
}
