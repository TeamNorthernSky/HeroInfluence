using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 단독] GameLoadScene에 부착. 새 게임 진입 시 데이터 부트스트랩 후 탐사씬 진입.
/// 흐름: DHScene Additive 로드 → 대기(PartyUnitBootstrap.Start 완료)
///       → HeroUnionHQVisitDetector.ReevaluateNow(방문 상태 계산)
///       → DHScene Active 전환 + GameLoadScene Unload
/// [KJ 260707] 기획 확정: 새 게임/이어하기 모두 탐사씬에서 시작 — 방문 파티 로비 분기 제거.
///             로비는 탐사씬에서 HQ/점령 거점 더블클릭으로만 진입.
/// </summary>
[DisallowMultipleComponent]
public class GameLoadGate : MonoBehaviour
{
    // [JC 260514] dhSceneName 인스펙터 필드 폐기 → 동적 property. GameSceneManager.Instance.ExplorationScene 토글 반영.
    private string dhSceneName => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.ExplorationScene
        : "DHScene";

    [Tooltip("DHScene 부트스트랩 후 폴링 전 추가 대기 프레임 수. PartyUnitBootstrap의 Start 호출 보장")]
    [SerializeField] private int bootstrapWaitFrames = 2;

    private void Start()
    {
        StartCoroutine(RunGate());
    }

    private IEnumerator RunGate()
    {
        Debug.Log("[GameLoadGate] Begin. Loading DHScene additive...");

        // [JC 260617] 새 게임 시작 시 전체 영속 상태 초기화(첫 실행과 동일). DHScene 부트스트랩 전에 클리어.
        if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();

        // [JC 260514] GameSceneManager Instance 경유 (fallback SceneManager).
        var op = GameSceneManager.Instance != null
            ? GameSceneManager.Instance.LoadSceneAsync(dhSceneName, LoadSceneMode.Additive)
            : SceneManager.LoadSceneAsync(dhSceneName, LoadSceneMode.Additive);
        if (op == null)
        {
            Debug.LogError($"[GameLoadGate] {dhSceneName} 로드 실패. Build Settings 확인");
            yield break;
        }
        while (!op.isDone) yield return null;

        // PartyUnitBootstrap.Start가 호출되도록 충분히 대기
        for (int i = 0; i < bootstrapWaitFrames; i++)
            yield return null;

        // HeroUnionHQVisitDetector 즉시 재평가 (이벤트 기반 전환 후)
        var detector = FindFirstObjectByType<HeroUnionHQVisitDetector>();
        if (detector != null)
        {
            detector.ReevaluateNow();
            Debug.Log("[GameLoadGate] HeroUnionHQVisitDetector.ReevaluateNow 호출");
        }
        else
        {
            Debug.LogWarning("[GameLoadGate] HeroUnionHQVisitDetector를 DHScene에서 찾지 못함. 방문 체크 불가");
        }

        // [KJ 260707] 방문 파티 여부와 무관하게 항상 탐사씬 시작(기획 확정). 로비 분기 제거.
        Debug.Log($"[GameLoadGate] → {dhSceneName} Active 전환 + GameLoadScene Unload");
        Scene dh = SceneManager.GetSceneByName(dhSceneName);
        if (dh.IsValid())
            SceneManager.SetActiveScene(dh);
        yield return SceneManager.UnloadSceneAsync(gameObject.scene);
    }
}
