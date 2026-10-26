using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JC.BattleTesting
{
    // 초기 데이터 바인딩 동안 표시만 차단합니다. UI의 Awake/Start/이벤트 구독은 계속 실행됩니다.
    public sealed class JcBattleHudEntryGate : MonoBehaviour
    {
        readonly List<(CanvasGroup group, float alpha, bool interactable, bool raycasts, bool owned)> groups =
            new List<(CanvasGroup, float, bool, bool, bool)>();
        BattleFlowManager flow;
        bool releasing;

        public static void Install(Scene scene, BattleFlowManager flow)
        {
            if (flow == null || scene.GetRootGameObjects().Any(r => r.GetComponent<JcBattleHudEntryGate>() != null)) return;
            var owner = new GameObject("JC_BattleHudEntryGate");
            SceneManager.MoveGameObjectToScene(owner, scene);
            var gate = owner.AddComponent<JcBattleHudEntryGate>();
            gate.flow = flow;
            var targets = new HashSet<Transform>();
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var panel in root.GetComponentsInChildren<HeroInfoPanel>(true))
                {
                    var target = panel.transform;
                    for (var p = target; p != null; p = p.parent)
                        if (p.name == "BottomPanel") { target = p; break; }
                    targets.Add(target);
                }
                foreach (var panel in root.GetComponentsInChildren<TurnOrderUI>(true)) targets.Add(panel.transform);
            }
            foreach (var target in targets)
            {
                var group = target.GetComponent<CanvasGroup>();
                bool owned = group == null;
                if (owned) group = target.gameObject.AddComponent<CanvasGroup>();
                gate.groups.Add((group, group.alpha, group.interactable, group.blocksRaycasts, owned));
                group.alpha = 0; group.interactable = false; group.blocksRaycasts = false;
            }
            flow.OnTurnStarted += gate.OnTurnStarted;
        }

        void OnTurnStarted(int round, BattleCharactor unit)
        {
            if (unit != null && !releasing) { releasing = true; StartCoroutine(Reveal()); }
        }
        IEnumerator Reveal()
        {
            // 이벤트 구독 순서와 무관하게 모든 HUD 갱신 및 레이아웃 계산이 끝난 뒤 공개합니다.
            yield return null;
            Canvas.ForceUpdateCanvases();
            Restore();
            Destroy(gameObject);
        }
        void Restore()
        {
            foreach (var state in groups)
            {
                if (state.group == null) continue;
                state.group.alpha = state.alpha;
                state.group.interactable = state.interactable;
                state.group.blocksRaycasts = state.raycasts;
                if (state.owned) Destroy(state.group);
            }
            groups.Clear();
        }
        void OnDestroy()
        {
            if (flow != null) flow.OnTurnStarted -= OnTurnStarted;
            Restore();
        }
    }
}
