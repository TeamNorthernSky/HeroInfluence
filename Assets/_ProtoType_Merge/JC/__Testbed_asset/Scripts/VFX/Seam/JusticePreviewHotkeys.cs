using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ASB.Work.Battle.SkillExecution;
using ASBGridCell = ASB.Work.BattleGrid.GridCell;
using ASBGridManager = ASB.Work.BattleGrid.BattleGridManager;

namespace JC.VFX.Seam
{
    /// <summary>
    /// 프리뷰 씬 단축키 — 인스펙터에서 컨트롤러를 띄우지 않고 스킬을 재생/초기화한다.
    ///
    /// 배치: Q/W = 등장!(청/적), E/R = 펀치(청/적), A = 대쉬(청, 타겟 클릭), T = 초기화.
    /// 같은 skillIndex라도 <see cref="Binding.useAlternate"/>가 다르면 다른 색 프리셋으로 나온다.
    ///
    /// ★타겟 클릭 모드(<see cref="Binding.requireTargetClick"/>):
    /// 키를 누르면 즉시 재생하지 않고 적 클릭을 기다린다. 적을 클릭하면
    ///   1) 그 유닛을 프리뷰 타깃으로 지정하고
    ///   2) ASB 타겟팅 시스템(SkillAreaPreviewHelper)으로 범위를 계산해
    ///      그리드에 관통 구조를 하이라이트한 뒤
    ///   3) 스킬을 재생한다.
    /// 범위 계산·하이라이트는 전부 ASB 공개 API — JC가 자체 계산하는 것은 없다.
    ///
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
            [Tooltip("켜면 +스킬 색 변종(바인더의 보조 프리셋)으로 재생한다.")]
            public bool useAlternate;
            [Tooltip("켜면 즉시 재생하지 않고 적 클릭을 기다린다(관통 범위 하이라이트 포함).")]
            public bool requireTargetClick;
            [Tooltip("화면 안내에 표시할 이름.")]
            public string label = "";
        }

        [Header("스킬 키")]
        [SerializeField]
        private Binding[] bindings =
        {
            new Binding { key = KeyCode.Q, skillIndex = 1010, useAlternate = false, label = "저스티스 등장! (기본·청)" },
            new Binding { key = KeyCode.W, skillIndex = 1010, useAlternate = true,  label = "저스티스 등장! (+·적)" },
            new Binding { key = KeyCode.E, skillIndex = 1020, useAlternate = false, label = "저스티스 펀치 (기본·청)" },
            new Binding { key = KeyCode.R, skillIndex = 1020, useAlternate = true,  label = "저스티스 펀치 (+·적)" },
            new Binding { key = KeyCode.A, skillIndex = 1030, useAlternate = false, requireTargetClick = true, label = "저스티스 대쉬 (기본·청)" },
        };

        [Header("초기화")]
        [Tooltip("프리뷰를 초기 상태로 되돌리는 키.")]
        [SerializeField] private KeyCode resetKey = KeyCode.T;

        [Header("타겟 클릭")]
        [Tooltip("유닛 클릭 판정에 쓸 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera raycastCamera;
        [Tooltip("레이캐스트 최대 거리.")]
        [SerializeField] private float maxRayDistance = 200f;

        [Header("화면 안내")]
        [Tooltip("게임 뷰 좌상단에 키 안내를 표시한다.")]
        [SerializeField] private bool showOverlay = true;
        [SerializeField] private int overlayFontSize = 13;

        private SkillPresentationPreviewController preview;
        private GUIStyle style;
        private Binding pendingTarget;   // 타겟 클릭 대기 중인 바인딩

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
                CancelTargeting();
                ASBGridManager.Instance?.ClearPreviewHighlight();
                Preview.ResetPreview();
                return;
            }

            // 타겟 클릭 대기 상태
            if (pendingTarget != null)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                {
                    CancelTargeting();
                    return;
                }
                if (Input.GetMouseButtonDown(0))
                {
                    TryPickTargetAndPlay();
                    return;
                }
            }

            for (int i = 0; i < bindings.Length; i++)
            {
                Binding b = bindings[i];
                if (b == null || !Input.GetKeyDown(b.key)) continue;

                CancelTargeting();
                if (b.requireTargetClick)
                {
                    pendingTarget = b;   // 클릭을 기다린다
                }
                else
                {
                    ASBGridManager.Instance?.ClearPreviewHighlight();
                    StartCoroutine(PlayRoutine(b, null));
                }
                return;
            }
        }

        private void CancelTargeting() => pendingTarget = null;

        /// <summary>
        /// 마우스 아래의 적 유닛을 찾아 타깃으로 지정하고, 관통 범위를 하이라이트한 뒤 재생한다.
        /// InputHandler와 같은 판정(RaycastAll → GetComponentInParent&lt;BattleCharactor&gt;)을 쓴다.
        /// </summary>
        private void TryPickTargetAndPlay()
        {
            Camera cam = raycastCamera != null ? raycastCamera : Camera.main;
            if (cam == null) return;

            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            RaycastHit[] hits = Physics.RaycastAll(ray, maxRayDistance);

            BattleCharactor picked = null;
            float best = float.MaxValue;
            for (int i = 0; i < hits.Length; i++)
            {
                var unit = hits[i].collider.GetComponentInParent<BattleCharactor>();
                if (unit == null) continue;

                // 적 진영만: 그리드 x >= 2 (Grid_2_* 전열 / Grid_3_* 후열)
                var cell = unit.OccupiedCell ?? ASBGridManager.Instance?.FindCellByUnit(unit);
                if (cell == null || cell.Coords.x < 2) continue;

                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    picked = unit;
                }
            }
            if (picked == null) return;   // 적이 아닌 곳 클릭 — 대기 유지

            Binding b = pendingTarget;
            pendingTarget = null;
            StartCoroutine(PlayRoutine(b, picked));
        }

        private IEnumerator PlayRoutine(Binding b, BattleCharactor clickedTarget)
        {
            if (Preview.IsPlaying)
            {
                Preview.ResetPreview();
                yield return null;   // PreviewResetGuard가 고아 시퀀스를 끊을 한 프레임
            }

            // 타깃을 클릭으로 골랐다면 프리뷰 유닛을 갈아끼우고 관통 범위를 표시한다.
            if (clickedTarget != null)
            {
                var actorT = Preview.PreviewActorTransform;
                var actor = actorT != null ? actorT.GetComponent<BattleCharactor>() : null;
                if (actor != null)
                {
                    Preview.SetUnits(actor, clickedTarget);

                    var skill = DHCsvTemplateCatalog.Instance != null
                        ? DHCsvTemplateCatalog.Instance.GetSkillTemplate(b.skillIndex)
                        : null;

                    ASBGridManager.Instance?.ClearPreviewHighlight();
                    if (skill != null && SkillAreaPreviewHelper.TryGetAreaCells(
                            actor, clickedTarget, skill, out ASBGridCell mainCell, out List<ASBGridCell> splash))
                    {
                        ASBGridManager.Instance?.ShowPreviewHighlight(skill, mainCell, splash);
                    }
                }
            }

            // 스폰보다 먼저 세워야 한다. 바인더가 Awake에서 이 값을 보고 프리셋을 고른다.
            JusticeTrailPresetBinder.UseAlternate = b.useAlternate;

            Preview.SetSelectedSkillIndex(b.skillIndex);
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
                if (b.requireTargetClick) sb.Append(" (타겟 클릭)");
                if (!string.IsNullOrEmpty(b.label)) sb.Append("  ").Append(b.label);
                sb.AppendLine();
            }
            sb.Append("<b>").Append(resetKey).Append("</b>  초기화");

            string state =
                pendingTarget != null ? "  <color=#7fd0ff>[적을 클릭하세요 — ESC 취소]</color>"
                : Preview != null && Preview.IsPlaying ? "  <color=#ffd24a>[재생 중]</color>" : "";
            GUI.Label(new Rect(12, 10, 520, 160), sb + state, style);
        }
    }
}
