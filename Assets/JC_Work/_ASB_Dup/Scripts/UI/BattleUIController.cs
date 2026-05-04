using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 전투 UI 전체 제어:
/// - Application.logMessageReceived 훅 → 화이트리스트 필터 → 해당 패널에 라우팅
/// - 로그 문자열 패턴으로 턴 상태 관리 (BattleFlowManager 참조·리플렉션 없이)
///   - "[턴 시작] 플레이어 진영" 감지 → 하단 Clear + 적 턴 모드 OFF (우상단은 유지, A안)
///   - "[턴 시작] 적 진영"       감지 → 하단 + 우상단 둘 다 Clear, 적 턴 모드 ON
/// - 적 턴 모드일 때만 우상단에 적재
///
/// Clear 타이밍이 로그가 쌓이기 전에 일어나므로 해당 턴 첫 로그가 그대로 보존된다.
/// (이전 Update 폴링 방식은 턴 전이를 1프레임 늦게 감지해 첫 로그들을 날려먹는 버그가 있었음.)
///
/// BattleFlowManager의 "[턴 시작] {진영}: ..." 로그 형식이 바뀌면 이 파일도 동기화 필요.
///
/// [BUI-TRACE] 접두 로그: 진단용 임시 로그. 재귀 방지를 위해 OnLogReceived에서 즉시 컷한다.
/// </summary>
[DisallowMultipleComponent]
public class BattleUIController : MonoBehaviour
{
    [Header("Enemy Action Log (우상단)")]
    [SerializeField] private TextMeshProUGUI enemyLogText;
    [SerializeField] private ScrollRect enemyLogScrollRect;

    [Header("Current Turn Log (하단)")]
    [SerializeField] private TextMeshProUGUI turnLogText;
    [SerializeField] private ScrollRect turnLogScrollRect;

    [Header("Filter")]
    [Tooltip("이 문자열을 포함한 로그만 UI에 표시.")]
    [SerializeField] private string[] whitelistPrefixes =
    {
        "[턴 시작]",
        "[BattleFlow]",
        "[Battle]",
        "[Skill/"
    };

    [Tooltip("프리픽스 없는 한글 전투 로그 캡처용 키워드.")]
    [SerializeField] private string[] whitelistKeywords =
    {
        "피해를 입혔습니다",
        "체력을 회복",
        "리타이어"
    };

    private readonly StringBuilder enemyBuf = new StringBuilder(1024);
    private readonly StringBuilder turnBuf = new StringBuilder(1024);

    private bool isInEnemyTurn = false;

    private void OnEnable()
    {
        Application.logMessageReceived += OnLogReceived;
        Debug.Log("[BUI-TRACE] OnEnable subscribed");
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= OnLogReceived;
        Debug.Log("[BUI-TRACE] OnDisable unsubscribed");
    }

    private void OnLogReceived(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return;
        }

        // 자기 자신 trace 로그 재귀 차단 (최우선)
        if (condition.StartsWith("[BUI-TRACE]"))
        {
            return;
        }

        bool pass = PassesWhitelist(condition);
        string snippet = condition.Length > 30 ? condition.Substring(0, 30) : condition;
        Debug.Log($"[BUI-TRACE] OnLogReceived pass={pass} msg={snippet}");

        if (!pass)
        {
            return;
        }

        // 턴 시작 감지 → 버퍼/모드 상태 갱신 (로그를 turnBuf/enemyBuf에 쌓기 전에)
        if (condition.Contains("[턴 시작]"))
        {
            turnBuf.Clear();

            bool enemyTurnNext = condition.Contains("적 진영");
            if (enemyTurnNext)
            {
                enemyBuf.Clear();
                isInEnemyTurn = true;
            }
            else
            {
                // 플레이어 턴 시작: 우상단은 유지(A안), 적재만 멈춤
                isInEnemyTurn = false;
            }
            Debug.Log($"[BUI-TRACE] TurnStart enemyTurn={enemyTurnNext} isInEnemyTurn={isInEnemyTurn}");
        }

        AppendLine(turnBuf, condition);
        ApplyTurnBuffer();

        if (isInEnemyTurn)
        {
            AppendLine(enemyBuf, condition);
            ApplyEnemyBuffer();
        }
    }

    private static void AppendLine(StringBuilder sb, string line)
    {
        if (sb.Length > 0)
        {
            sb.Append('\n');
        }
        sb.Append(line);
    }

    private bool PassesWhitelist(string message)
    {
        for (int i = 0; i < whitelistPrefixes.Length; i++)
        {
            string prefix = whitelistPrefixes[i];
            if (!string.IsNullOrEmpty(prefix) && message.Contains(prefix))
            {
                return true;
            }
        }
        for (int i = 0; i < whitelistKeywords.Length; i++)
        {
            string keyword = whitelistKeywords[i];
            if (!string.IsNullOrEmpty(keyword) && message.Contains(keyword))
            {
                return true;
            }
        }
        return false;
    }

    private void ApplyTurnBuffer()
    {
        Debug.Log($"[BUI-TRACE] ApplyTurnBuffer len={turnBuf.Length} textNull={turnLogText == null}");
        if (turnLogText != null)
        {
            turnLogText.text = turnBuf.ToString();
            turnLogText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(turnLogText.rectTransform);
        }
        ScrollToBottom(turnLogScrollRect);
    }

    private void ApplyEnemyBuffer()
    {
        Debug.Log($"[BUI-TRACE] ApplyEnemyBuffer len={enemyBuf.Length} textNull={enemyLogText == null}");
        if (enemyLogText != null)
        {
            enemyLogText.text = enemyBuf.ToString();
            enemyLogText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(enemyLogText.rectTransform);
        }
        ScrollToBottom(enemyLogScrollRect);
    }

    private void ScrollToBottom(ScrollRect rect)
    {
        if (rect == null)
        {
            return;
        }
        // 0f = bottom, 1f = top
        rect.verticalNormalizedPosition = 0f;
    }
}
