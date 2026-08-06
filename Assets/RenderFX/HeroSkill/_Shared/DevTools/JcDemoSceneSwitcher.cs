using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.VFX.Seam
{
    /// <summary>
    /// VFX 데모 빌드 전용 씬 전환기.
    ///
    /// 조작: <b>F8</b> = z_JC_Testbed / <b>F9</b> = z_JC_PreViewsScene_2 / <b>F10</b> = z_JC_PreViewsScene_3
    /// 현재 씬과 같은 키를 누르면 아무 동작도 하지 않는다(무의미한 재로드 방지).
    ///
    /// ★F 키를 쓰는 이유 — 숫자키 조합은 전부 기존 치트가 선점하고 있다.
    ///   Ctrl+Shift+8 = 턴 스킵(InputHandler) / Shift+9·0 = 전멸(BattleCheatController, Ctrl 무시).
    ///   같은 씬에서 눌러 「아무 동작 없음」이 되는 경우, 씬 전환은 안 되고 치트만 발동해 버린다.
    ///
    /// ★배치 — 씬에 오브젝트를 두지 않는다. 런타임에 스스로 생성되고 씬에 저장되지 않는다.
    ///   (HideFlags.DontSave는 「플레이 종료 시 파괴 안 함」을 포함해 에디트 모드로 새어 나가므로 쓰지 않는다.)
    ///
    /// ★「초기화해서 이동」 — LoadSceneMode.Single로 통째 재로드한다. 씬 안의 상태는 전부 새로 시작된다.
    ///   단 DontDestroyOnLoad 객체(영속 매니저·JC 드라이버 등)는 설계상 살아남는다.
    /// </summary>
    [DisallowMultipleComponent]
    public class JcDemoSceneSwitcher : MonoBehaviour
    {
        private struct Entry
        {
            public KeyCode key;
            public string scene;
            public string label;
        }

        private static readonly Entry[] Entries =
        {
            new Entry { key = KeyCode.F8,  scene = "z_JC_Testbed",         label = "테스트베드" },
            new Entry { key = KeyCode.F9,  scene = "z_JC_PreViewsScene_2", label = "저스티스 프리뷰" },
            new Entry { key = KeyCode.F10, scene = "z_JC_PreViewsScene_3", label = "블랙불릿 프리뷰" },
            new Entry { key = KeyCode.F11, scene = "z_JC_PreViewsScene_5", label = "네코밍 프리뷰" },
        };

        /// <summary>
        /// ★이 전환기는 <b>위 목록에 있는 씬에서만</b> 동작한다.
        /// DontDestroyOnLoad 로 살아남기 때문에, 막지 않으면 부트씬·본게임 씬에도 안내가 뜨고
        /// F 키가 먹혀 버린다. 데모용 도구가 본 게임 흐름에 끼어들면 안 된다.
        /// </summary>
        private static bool InDemoScene()
        {
            string cur = SceneManager.GetActiveScene().name;
            for (int i = 0; i < Entries.Length; i++)
                if (Entries[i].scene == cur) return true;
            return false;
        }

        [Tooltip("화면 좌하단에 키 안내를 표시한다.")]
        private const bool ShowOverlay = true;

        private string message;
        private float messageUntil;

        /// <summary>
        /// ★<b>레거시 — 현재 비활성</b> (260803).
        ///
        /// 끈 이유: 이 전환기는 260801 중간 발표 데모 빌드 전용으로 만든 것이고,
        ///   그 빌드에는 테스트 씬 3개만 담겨 있었다. 이후 <b>빌드 씬 목록에서 테스트 씬을 빼면서</b>
        ///   F 키를 누르면 다음 오류가 난다:
        ///     "Scene 'z_JC_Testbed' couldn't be loaded because it has not been added to the build settings"
        ///   게다가 DontDestroyOnLoad 로 살아남아 본 게임 씬까지 따라다닌다.
        ///   데모용 도구가 본 게임 흐름에 끼어들 이유가 없다.
        ///
        /// 되살리는 법: 아래 Enabled 를 true 로 바꾸고,
        ///   <b>File → Build Settings 에 Entries 의 씬들을 추가</b>한다. 둘 다 해야 동작한다.
        /// </summary>
        private const bool Enabled = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Enabled) return;                                            // 레거시 — 생성 자체를 안 한다
            if (FindAnyObjectByType<JcDemoSceneSwitcher>() != null) return;   // 중복 방지

            var go = new GameObject("[JC] DemoSceneSwitcher");
            DontDestroyOnLoad(go);
            go.AddComponent<JcDemoSceneSwitcher>();
        }

        private void Update()
        {
            if (!Enabled) return;
            if (!InDemoScene()) return;   // 데모 씬 밖에서는 키를 먹지 않는다

            for (int i = 0; i < Entries.Length; i++)
            {
                if (!Input.GetKeyDown(Entries[i].key)) continue;

                string current = SceneManager.GetActiveScene().name;
                if (current == Entries[i].scene)
                {
                    Notify($"이미 {Entries[i].label} 씬입니다.");
                    return;
                }

                Notify($"{Entries[i].label} 로 이동…");
                SceneManager.LoadScene(Entries[i].scene, LoadSceneMode.Single);
                return;
            }
        }

        private void Notify(string text)
        {
            message = text;
            messageUntil = Time.unscaledTime + 1.5f;
            Debug.Log("[DemoSwitcher] " + text);
        }

        private void OnGUI()
        {
            if (!Enabled) return;
            if (!ShowOverlay) return;
            if (!InDemoScene()) return;   // 부트씬·본게임 씬에 안내가 뜨지 않게

            var style = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true };
            string current = SceneManager.GetActiveScene().name;

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Entries.Length; i++)
            {
                bool here = current == Entries[i].scene;
                sb.Append(here ? "<b>▶ " : "  ")
                  .Append(Entries[i].key).Append("</b>  ")
                  .Append(Entries[i].label)
                  .Append(here ? "  (현재)" : "")
                  .AppendLine();
            }

            float h = 22f * Entries.Length + 26f;
            GUI.Label(new Rect(12, Screen.height - h, 420, h), sb.ToString(), style);

            if (!string.IsNullOrEmpty(message) && Time.unscaledTime < messageUntil)
            {
                GUI.Label(new Rect(12, Screen.height - h - 20f, 420, 20f),
                    "<color=#7fd0ff>" + message + "</color>", style);
            }
        }
    }
}
