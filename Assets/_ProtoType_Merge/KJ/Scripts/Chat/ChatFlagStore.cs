using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// [KJ 260714] 채팅 분기 조건용 플래그 저장소 (F008). Set_Flag_* 효과가 쓰고, 분기 조건 평가가 읽는다.
/// 저장 연동: GameSaveService.Capture가 CaptureSnapshot으로 담고, 로드(DH)는 RestoreSnapshot 호출.
/// 새 게임 리셋: GameManager.ResetForNewGame → Clear().
/// </summary>
public static class ChatFlagStore
{
    private static readonly Dictionary<string, int> flags = new Dictionary<string, int>();

    /// <summary>미설정 플래그는 0.</summary>
    public static int Get(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && flags.TryGetValue(key.Trim(), out int v) ? v : 0;
    }

    public static void Set(string key, int value = 1)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        flags[key.Trim()] = value;
    }

    public static void Clear()
    {
        flags.Clear();
    }

    /// <summary>"Flag_X==1" / "Flag_X!=1" 평가. 빈 조건 = 통과. 문법 미지원 = 불통과 + 경고.</summary>
    public static bool EvaluateCondition(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return true;

        string op = condition.Contains("==") ? "==" : condition.Contains("!=") ? "!=" : null;
        if (op == null)
        {
            Debug.LogWarning($"[ChatFlagStore] 조건 문법 미지원: '{condition}' — 불통과 처리");
            return false;
        }

        string[] parts = condition.Split(new[] { op }, StringSplitOptions.None);
        if (parts.Length != 2 || !int.TryParse(parts[1].Trim(), out int expected))
        {
            Debug.LogWarning($"[ChatFlagStore] 조건 파싱 실패: '{condition}' — 불통과 처리");
            return false;
        }

        int actual = Get(parts[0]);
        return op == "==" ? actual == expected : actual != expected;
    }

    // ── 저장 연동 (스냅샷 패턴 — 필드 추가 시 SaveService 무수정) ──
    [Serializable]
    public struct Entry
    {
        public string key;
        public int value;
    }

    public static List<Entry> CaptureSnapshot()
    {
        var list = new List<Entry>(flags.Count);
        foreach (KeyValuePair<string, int> pair in flags)
            list.Add(new Entry { key = pair.Key, value = pair.Value });
        return list;
    }

    public static void RestoreSnapshot(IReadOnlyList<Entry> snapshot)
    {
        flags.Clear();
        if (snapshot == null) return;
        for (int i = 0; i < snapshot.Count; i++)
            Set(snapshot[i].key, snapshot[i].value);
    }
}
