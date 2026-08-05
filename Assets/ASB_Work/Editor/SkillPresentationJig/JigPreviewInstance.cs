using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// 지그가 스크러빙에 쓰는 임시 프리뷰 인스턴스의 소유자.
    ///
    /// <b>실제 씬의 캐릭터에 Animation Track을 바인딩하지 않는다.</b> 바인딩하면 스크러빙이 그 캐릭터의
    /// 포즈를 바꾸고, 씬 저장 시 원치 않는 변경이 남는다. 프리팹에서 별도 인스턴스를 만들어 그것만 움직인다.
    ///
    /// <b>HideFlags.DontSave를 쓰지 않는다.</b> 이 저장소에 사고 이력이 있다 —
    /// JcCueTimingDriver.cs:76 원저자 주석: "HideFlags.DontSave 금지 — 「플레이 종료 시에도 파괴하지 않음」이
    /// 포함돼 있어 오브젝트가 에디트 모드로 새어 나가고 플레이할 때마다 누적된다."
    /// 여기서는 DontSaveInEditor만 쓰고, 자동 파괴에 의존하지 않고 네 시점에서 명시적으로 파괴한다.
    /// </summary>
    [InitializeOnLoad]
    public static class JigPreviewInstance
    {
        /// <summary>잔여 인스턴스 탐색용 이름 규약. 사람이 실수로 만들 이름이 아니어야 한다.</summary>
        public const string RootName = "[ASB Jig] Preview (임시 — 저장되지 않음)";

        private static GameObject _instance;
        private static PlayableDirector _director;

        /// <summary>
        /// 프리뷰 인스턴스를 만들기 직전 씬이 깨끗했는지. 정리 후 dirty 표시를 되돌리는 데 쓴다.
        ///
        /// HideFlags.DontSaveInEditor는 "저장에 포함되지 않음"만 보장하고 <b>씬의 dirty 표시는 막지 못한다.</b>
        /// 그대로 두면 편집자가 저장 프롬프트를 보게 되고, EditMode 테스트가 "unsaved changes"로 거부된다.
        /// <b>원래 dirty였다면 건드리지 않는다</b> — 편집자의 미저장 작업을 지워버리면 안 된다.
        /// </summary>
        private static bool _sceneWasCleanBeforeCreate;

        public static GameObject Current => _instance;
        public static PlayableDirector Director => _director;
        public static bool IsAlive => _instance != null;

        static JigPreviewInstance()
        {
            // ── 정리 훅 4개 중 3개 (나머지 하나는 창의 OnDisable) ──
            AssemblyReloadEvents.beforeAssemblyReload -= DestroyInstance;
            AssemblyReloadEvents.beforeAssemblyReload += DestroyInstance;

            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;

            EditorApplication.quitting -= DestroyInstance;
            EditorApplication.quitting += DestroyInstance;

            // 이전 세션에서 새어 나온 잔여물 정리(도메인 리로드 중 훅이 못 돈 경우 대비).
            EditorApplication.delayCall += DestroyStrays;
        }

        private static void OnPlayModeChanged(PlayModeStateChange _) => DestroyInstance();

        /// <summary>
        /// 프리팹에서 프리뷰 인스턴스를 만든다. 기존 인스턴스가 있으면 먼저 파괴한다.
        /// </summary>
        public static GameObject Create(GameObject characterPrefab, out string error)
        {
            error = null;
            DestroyInstance();

            if (characterPrefab == null)
            {
                error = "캐릭터 프리팹이 비어 있습니다.";
                return null;
            }

            Scene active = SceneManager.GetActiveScene();
            _sceneWasCleanBeforeCreate = active.IsValid() && !active.isDirty;

            GameObject go = (GameObject)PrefabUtility.InstantiatePrefab(characterPrefab);
            if (go == null)
            {
                error = $"프리팹 '{characterPrefab.name}' 인스턴스화에 실패했습니다.";
                return null;
            }

            go.name = RootName;

            // 씬 저장에 남지 않게 한다. DontSave(전체)는 쓰지 않는다 — 위 주석 참조.
            go.hideFlags |= HideFlags.DontSaveInEditor;

            var animator = go.GetComponentInChildren<Animator>(true);
            if (animator == null)
            {
                Object.DestroyImmediate(go);
                error = $"프리팹 '{characterPrefab.name}'에서 Animator를 찾지 못했습니다.";
                return null;
            }

            // Timeline 프리뷰가 클립을 평가하면 AniEvent_* 이벤트가 실제로 디스패치된다.
            // 프리뷰 인스턴스에는 수신기가 없어 Unity가 "has no receiver!" 경고를 매 이벤트마다 낸다.
            //
            // AnimationEventBridge를 붙여 흡수한다. Bind()를 부르지 않으므로 _owner가 null이고
            // 모든 AniEvent_* 메서드가 null 조건부 호출(_owner?. / Router?.)이라 전부 안전한 no-op이 된다.
            // 새 클래스를 만들지 않는 이유: 에디터 어셈블리의 MonoBehaviour는 GameObject에 붙일 수 없고
            // (Unity가 거부한다), 이 컴포넌트가 애초에 그 이벤트를 받기 위해 존재하는 것이다.
            // ★Animator가 있는 오브젝트에 붙여야 한다 — Unity가 거기서 메서드를 찾는다.
            if (animator.gameObject.GetComponent<AnimationEventBridge>() == null)
            {
                animator.gameObject.AddComponent<AnimationEventBridge>();
            }

            PlayableDirector director = go.GetComponent<PlayableDirector>();
            if (director == null)
            {
                director = go.AddComponent<PlayableDirector>();
            }
            director.playOnAwake = false;

            _instance = go;
            _director = director;
            return go;
        }

        public static Animator ResolveAnimator()
        {
            return _instance != null ? _instance.GetComponentInChildren<Animator>(true) : null;
        }

        public static void DestroyInstance()
        {
            // 프리뷰 카메라는 인스턴스의 자식이라 함께 파괴되지만, RenderTexture는 명시 해제가 필요하다.
            JigPreviewCamera.Forget();

            bool had = _instance != null;
            if (had)
            {
                Object.DestroyImmediate(_instance);
            }
            _instance = null;
            _director = null;

            if (had)
            {
                RestoreSceneCleanliness();
            }
        }

        /// <summary>
        /// 우리가 만들기 전에 씬이 깨끗했다면 dirty 표시를 되돌린다.
        /// 원래 dirty였다면 아무것도 하지 않는다(편집자의 미저장 변경을 삼키지 않기 위해).
        ///
        /// <c>EditorSceneManager.ClearSceneDirtiness</c>는 internal이라 리플렉션으로 호출한다.
        /// 버전이 바뀌어 사라지면 조용히 no-op이 되고, 그 경우 씬이 dirty로 남는 것 외의 피해는 없다.
        /// </summary>
        private static void RestoreSceneCleanliness()
        {
            if (!_sceneWasCleanBeforeCreate)
            {
                return;
            }
            _sceneWasCleanBeforeCreate = false;

            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid())
            {
                return;
            }

            MethodInfo clear = ResolveClearSceneDirtiness();
            if (clear == null)
            {
                Debug.LogWarning("[Jig] 씬 dirty 표시를 되돌리지 못했습니다(ClearSceneDirtiness 없음). " +
                                 "씬을 저장하거나 변경을 되돌려도 무해합니다.");
                return;
            }

            try
            {
                clear.Invoke(null, new object[] { active });
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Jig] 씬 dirty 표시 되돌리기 실패: {e.GetType().Name}");
            }
        }

        private static MethodInfo _clearSceneDirtiness;
        private static bool _clearResolved;

        private static MethodInfo ResolveClearSceneDirtiness()
        {
            if (_clearResolved)
            {
                return _clearSceneDirtiness;
            }
            _clearResolved = true;

            _clearSceneDirtiness = typeof(EditorSceneManager).GetMethod(
                "ClearSceneDirtiness",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                null,
                new[] { typeof(Scene) },
                null);

            return _clearSceneDirtiness;
        }

        /// <summary>이름 규약으로 잔여 인스턴스를 찾아 정리한다.</summary>
        public static void DestroyStrays()
        {
            GameObject[] all = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == RootName && all[i] != _instance)
                {
                    Object.DestroyImmediate(all[i]);
                }
            }
        }
    }
}
