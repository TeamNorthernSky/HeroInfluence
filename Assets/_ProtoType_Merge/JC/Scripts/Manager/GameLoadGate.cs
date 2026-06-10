using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 단독] GameLoadScene에 부착. 새 게임 진입 시 데이터 부트스트랩 후 분기.
/// 흐름: DHScene Additive 로드 → 1프레임 대기(PartyUnitBootstrap.Start 완료)
///       → CastleHQVisitDetector.ReevaluateNow → 방문 파티 체크
///       → 있음: DHScene Unload + 로비 씬(GameSceneManager.LobbyScene) Single 로드
///       → 없음: DHScene Active 전환 + GameLoadScene Unload
/// </summary>
[DisallowMultipleComponent]
public class GameLoadGate : MonoBehaviour
{
    // [JC 260514] dhSceneName 인스펙터 필드 폐기 → 동적 property. GameSceneManager.Instance.ExplorationScene 토글 반영.
    private string dhSceneName => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.ExplorationScene
        : "DHScene";

    // [JC 260610] 인스펙터 필드 폐기 → 동적 property. GameSceneManager.Instance.LobbyScene 단일 정본 반영.
    private string lobbySceneName => GameSceneManager.Instance != null
        ? GameSceneManager.Instance.LobbyScene
        : "HQLobbyScene";

    [Tooltip("DHScene 부트스트랩 후 폴링 전 추가 대기 프레임 수. PartyUnitBootstrap의 Start 호출 보장")]
    [SerializeField] private int bootstrapWaitFrames = 2;

    private void Start()
    {
        StartCoroutine(RunGate());
    }

    private IEnumerator RunGate()
    {
        Debug.Log("[GameLoadGate] Begin. Loading DHScene additive...");

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

        // CastleHQVisitDetector 즉시 재평가 (이벤트 기반 전환 후)
        var detector = FindFirstObjectByType<CastleHQVisitDetector>();
        if (detector != null)
        {
            detector.ReevaluateNow();
            Debug.Log("[GameLoadGate] CastleHQVisitDetector.ReevaluateNow 호출");
        }
        else
        {
            Debug.LogWarning("[GameLoadGate] CastleHQVisitDetector를 DHScene에서 찾지 못함. 방문 체크 불가");
        }

        bool hasVisiting = HQVisitState.Instance != null && HQVisitState.Instance.HasVisitingParty;
        Debug.Log($"[GameLoadGate] HasVisitingParty = {hasVisiting}");

        if (hasVisiting)
        {
            Debug.Log($"[GameLoadGate] → {lobbySceneName} (Single)");
            // [JC 260514] GameSceneManager Instance 경유.
            if (GameSceneManager.Instance != null)
                GameSceneManager.Instance.LoadScene(lobbySceneName, LoadSceneMode.Single);
            else
                SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }
        else
        {
            Debug.Log($"[GameLoadGate] 방문 파티 없음 → {dhSceneName} Active 전환 + GameLoadScene Unload");
            Scene dh = SceneManager.GetSceneByName(dhSceneName);
            if (dh.IsValid())
                SceneManager.SetActiveScene(dh);
            yield return SceneManager.UnloadSceneAsync(gameObject.scene);
        }
    }
}
