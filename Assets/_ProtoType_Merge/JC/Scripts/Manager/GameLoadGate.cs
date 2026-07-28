using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// [JC 단독] GameLoadScene에 부착. 새 게임 진입 시 데이터 부트스트랩 후 탐사씬 진입.
/// 흐름: DHScene Additive 로드 → 대기(PartyUnitBootstrap.Start 완료)
///       → HeroUnionHQVisitDetector.ReevaluateNow(방문 상태 계산)
///       → DHScene Active 전환 + GameLoadScene Unload
/// [KJ 260707] 기획 확정: 새 게임/이어하기 모두 탐사씬에서 시작 — 방문 파티 로비 분기 제거.
///             로비는 탐사씬에서 HQ/점령 거점 더블클릭으로만 진입.
/// [KJ 260714] 이어하기 배선: SaveSlotRepository.IsContinue면 ResetForNewGame 대신 저장 슬롯을 복원한다.
///   복원 순서 — (1) 저장 파일 역직렬화 → (2) BeginRestoreSession + RestoreFromGameSaveData(DDOL 리포/전역 상태를
///   씬 로드 전에 채움 → PartyUnitBootstrap이 기존 파티 감지 후 재시드하지 않고 복원값 동기화)
///   → (3) DHScene 로드 → (4) ApplySceneState(씬 오브젝트 반영) → (5) DHEventStateRepository.RestoreFlagSnapshot
///   → (6) CompleteRestoreSession. 이후 ReevaluateNow/Active 전환은 새게임과 공통(복원된 위치 기준 재평가).
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

        // [KJ 260714] 이어하기 여부 확정 후 플래그 소비(재진입/새게임 시 stale 방지).
        bool isContinue = SaveSlotRepository.IsContinue;
        SaveSlotRepository.IsContinue = false;

        GameSaveData restoreData = null;
        if (isContinue)
        {
            restoreData = LoadSaveData(SaveSlotRepository.CurrentSlot);
            if (restoreData == null || !restoreData.hasData)
            {
                Debug.LogWarning($"[GameLoadGate] 이어하기 실패 — 슬롯 {SaveSlotRepository.CurrentSlot} 저장 데이터 없음. 새 게임으로 폴백.");
                if (restoreData != null) UnityEngine.Object.Destroy(restoreData);
                restoreData = null;
                isContinue = false;
            }
        }

        if (isContinue)
        {
            // [KJ 260714] 복원 모드: DHScene 로드 전에 DDOL 리포/전역 상태를 저장 데이터로 채운다.
            // 부트스트랩(PartyUnitBootstrap)이 ContainsParty==true를 감지 → 기본값 재시드 대신 복원값 동기화.
            Debug.Log($"[GameLoadGate] 이어하기 복원 — 슬롯 {SaveSlotRepository.CurrentSlot} (day {restoreData.currentDay}).");
            DHGameStateRestoreService.BeginRestoreSession();
            DHGameStateRestoreService.RestoreFromGameSaveData(restoreData);
        }
        else
        {
            // [JC 260617] 새 게임 시작 시 전체 영속 상태 초기화(첫 실행과 동일). DHScene 부트스트랩 전에 클리어.
            if (GameManager.Instance != null) GameManager.Instance.ResetForNewGame();
        }

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

        if (isContinue)
        {
            // [KJ 260714] 씬 오브젝트 반영은 DHScene 로드 후에만 가능(FindObjectsByType). 파티 위치/유닛/게이트/레지스트리 동기화.
            DHGameStateRestoreService.ApplySceneState();
            // ChatFlags는 턴 시작 스냅샷에 없어 별도 복원(F008).
            // 복원 세션 종료 + 현재 상태를 새 턴 시작 스냅샷으로 캡처(이후 수동 저장이 이 시점 기준).
            DHGameStateRestoreService.CompleteRestoreSession(captureTurnStartSnapshot: true);
            UnityEngine.Object.Destroy(restoreData);
            restoreData = null;
            Debug.Log("[GameLoadGate] 이어하기 복원 완료.");
        }

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

    // [KJ 260714] save_slot_{i}.json → GameSaveData 역직렬화. GameSaveData는 ScriptableObject라
    // FromJson<T> 불가 → CreateInstance 후 FromJsonOverwrite. 실패/부재 시 null(호출부에서 새 게임 폴백).
    private static GameSaveData LoadSaveData(int slotIndex)
    {
        if (slotIndex < 0)
            return null;

        string path = Path.Combine(Application.persistentDataPath, $"save_slot_{slotIndex}.json");
        if (!File.Exists(path))
        {
            Debug.LogWarning($"[GameLoadGate] 저장 파일 없음: {path}");
            return null;
        }

        try
        {
            GameSaveData data = ScriptableObject.CreateInstance<GameSaveData>();
            JsonUtility.FromJsonOverwrite(File.ReadAllText(path), data);
            return data;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[GameLoadGate] 저장 파일 파싱 실패({slotIndex}): {ex.Message}");
            return null;
        }
    }
}
