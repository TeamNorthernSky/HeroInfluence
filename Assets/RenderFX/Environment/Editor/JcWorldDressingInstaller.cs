using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.Env.EditorTools
{
    /// <summary>
    /// 월드 드레싱(지역 팔레트 · 나무 산포 · 미니어처 DOF · 외곽 바닥) 설치·재적용기 (6단계).
    ///
    /// ★260908 설계: 팀 공용 씬(DHScene_3)은 다른 팀원이 덮어쓸 수 있다(260903 실증). 그래서 드레싱을
    ///   씬에 손으로 심지 않고 <b>프리팹 1개 + 이 메뉴</b>로 심는다 — 씬이 통째로 되돌아가도
    ///   메뉴 한 번이면 같은 상태로 복원된다(환경 프로파일의 「▶ 적용」과 같은 완충 구조).
    ///   값은 전부 자산이 소유한다: 존/재질 = REGION_*.asset, DOF = VOL_DOF_Miniature.asset, 나무 = 프리팹 5종.
    ///   나무 산포 결과물은 씬에 굽지 않고 플레이 시작 시 생성한다(scatterOnStart) — 씬 파일 비대화·머지 잡음 방지.
    ///
    /// 멱등: 이미 설치돼 있으면 새로 만들지 않고 결선·라이트 바이어스만 다시 맞춘다.
    /// </summary>
    public static class JcWorldDressingInstaller
    {
        public const string PrefabPath = "Assets/RenderFX/Environment/Prefabs/JC_WorldDressing.prefab";
        public const string RootName = "JC_WorldDressing";
        /// <summary>샤프 엣지 나무의 그림자 노멀 바이어스 누출(가는 빛 줄) 방지값 — 260908 실측.</summary>
        public const float ShadowNormalBias = 0.15f;

        [MenuItem("JC/월드 드레싱/설치 · 재적용 (활성 씬)")]
        public static void InstallOrReapply()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[JC] 플레이 중에는 설치할 수 없습니다."); return; }
            var scene = SceneManager.GetActiveScene();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) { Debug.LogError($"[JC] 프리팹 없음: {PrefabPath}"); return; }

            var inst = scene.GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            bool created = false;
            if (inst == null)
            {
                inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                inst.name = RootName;
                Undo.RegisterCreatedObjectUndo(inst, "JC 월드 드레싱 설치");
                created = true;
            }

            // 라이트: 샤프 엣지 그림자 누출 방지 바이어스
            var sun = RenderSettings.sun;
            if (sun == null) sun = Object.FindObjectsOfType<Light>().FirstOrDefault(l => l.type == LightType.Directional && l.gameObject.scene == scene);
            if (sun != null && !Mathf.Approximately(sun.shadowNormalBias, ShadowNormalBias))
            {
                Undo.RecordObject(sun, "JC shadowNormalBias");
                sun.shadowNormalBias = ShadowNormalBias;
            }

            // DOF 트래커 → 씬 메인 카메라
            var tracker = inst.GetComponentInChildren<JcDofFocusTracker>(true);
            if (tracker != null) { var cam = Camera.main; if (cam != null && tracker.targetCamera != cam) { Undo.RecordObject(tracker, "JC DOF camera"); tracker.targetCamera = cam; } }

            // 지역 팔레트 전역 변수 + 나무 재질 값 밀어 넣기
            var palette = inst.GetComponentInChildren<JcRegionPaletteController>(true);
            if (palette != null) palette.Push();

            EditorSceneManager.MarkSceneDirty(scene);
            var sc = inst.GetComponentInChildren<JcTreeScatter>(true);
            Debug.Log($"[JC] 월드 드레싱 {(created ? "설치" : "재적용")} 완료 — 씬 '{scene.name}', sun bias {ShadowNormalBias}, " +
                      $"scatterOnStart={(sc ? sc.scatterOnStart.ToString() : "-")}. 저장은 직접(Ctrl+S). 나무 프리뷰는 ▶ 뿌리기 / 저장 전 ✕ 지우기.");
            Selection.activeGameObject = inst;
        }

        [MenuItem("JC/월드 드레싱/나무 프리뷰 뿌리기 (활성 씬)")]
        public static void PreviewTrees()
        {
            var sc = FindScatter(); if (sc == null) return;
            Undo.RegisterFullObjectHierarchyUndo(sc.root != null ? sc.root.gameObject : sc.gameObject, "JC 나무 프리뷰");
            sc.Scatter();
            EditorSceneManager.MarkSceneDirty(sc.gameObject.scene);
            Debug.Log($"[JC] 나무 프리뷰 {sc.lastCount}그루 — 저장 전 ✕ 지우기 권장(런타임에 다시 생성됨).");
        }

        [MenuItem("JC/월드 드레싱/나무 프리뷰 지우기 (활성 씬)")]
        public static void ClearTrees()
        {
            var sc = FindScatter(); if (sc == null) return;
            Undo.RegisterFullObjectHierarchyUndo(sc.root != null ? sc.root.gameObject : sc.gameObject, "JC 나무 프리뷰 지우기");
            sc.Clear();
            EditorSceneManager.MarkSceneDirty(sc.gameObject.scene);
        }

        private static JcTreeScatter FindScatter()
        {
            var inst = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == RootName);
            var sc = inst ? inst.GetComponentInChildren<JcTreeScatter>(true) : null;
            if (sc == null) Debug.LogWarning("[JC] 활성 씬에 JC_WorldDressing 이 없습니다 — 먼저 설치하세요.");
            return sc;
        }
    }
}
