// [CHEAT_ONLY / 다른 기능 재사용 금지 — 2026-10-27 사용자 요청]
// 이 파일은 1구역 플레이테스트 진입 시간을 줄이는 치트 전용입니다.
// 1구역 치트 전용 완료 노드 목록과 제한된 조건 판정입니다. 정식 퀘스트 순서·분기 엔진·공통 EXP 계산의 기준으로 사용하지 마세요.
// 의존 방향은 치트 → 기존 게임 API만 허용합니다. 일반 게임 코드는 이 폴더를 참조하지 마세요.
// 공통 기능이 필요하면 기존 정식 시스템을 사용하세요. 치트 구현을 공용 코드로 승격하거나 복사하지 마세요.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace JC.DebugTools
{
    // 진행 상태/보상은 복제하지 않습니다. 치트에서 고를 수 있는 기존 완료 노드의 연결만 정의합니다.
    public sealed class JcZone1WarpRoute
    {
        public readonly int Stage, BranchId;
        public readonly string Label, EventKey, BattleKey;
        public JcZone1WarpRoute(int stage, string label, string eventKey, string battleKey, int branchId)
        { Stage = stage; Label = label; EventKey = eventKey; BattleKey = battleKey; BranchId = branchId; }

        public static readonly JcZone1WarpRoute[] Routes =
        {
            new JcZone1WarpRoute(1, "약탈꾼 — 루미나 나락", "MainQuest1-1", "BE005", 12503),
            new JcZone1WarpRoute(1, "약탈꾼 — 네코밍 재앙", "MainQuest1-1", "BE005", 12505),
            new JcZone1WarpRoute(1, "소년 구출 — 소년 동행", "MainQuest1-2", "BE006", 11302),
            new JcZone1WarpRoute(2, "메인 2-1 — 인질 정보 확보", "MainQuest2-1", "BE121", 12002),
            new JcZone1WarpRoute(2, "메인 2-2", "MainQuest2-2", "BE122", 12402),
            new JcZone1WarpRoute(2, "메인 2-3", "MainQuest2-3", "BE123", 12802),
        };

        public static bool IsStageComplete(int stage, Func<string, bool> flag)
        {
            if (flag($"Flag_MainQuest{stage}Complete")) return true;
            for (int i = 1; i <= (stage == 1 ? 2 : 3); i++)
                if (flag($"Flag_MainQuest{stage}-{i}Complete")) return true;
            return false;
        }

        public static int NextStage(Func<string, bool> flag)
        {
            // 후속 분기를 먼저 수행한 저장 상태에서도 이전 선택을 소급하여 바꾸지 않습니다.
            if (IsStageComplete(2, flag)) return 0;
            return IsStageComplete(1, flag) ? 2 : 1;
        }

        public static bool Matches(DHEventBranchTemplate row, Func<string, bool> flag)
        {
            // 이 manifest의 완료 노드는 Flag == 1 / != 1 조건만 사용합니다.
            // 새 조건이 생겼을 때 무시한 채 진행하지 않도록 알 수 없는 조건은 거절합니다.
            if (row.TriggerValues.Count == 0) return string.IsNullOrEmpty(row.TriggerType);
            foreach (string raw in row.TriggerValues)
            {
                string value = raw.Replace(" ", "");
                bool not = value.EndsWith("!=1", StringComparison.Ordinal);
                if (!not && !value.EndsWith("==1", StringComparison.Ordinal))
                    throw new InvalidOperationException($"지원하지 않는 분기 조건: {raw}");
                string key = value.Substring(0, value.Length - 3);
                if (!key.StartsWith("Flag_", StringComparison.Ordinal))
                    throw new InvalidOperationException($"지원하지 않는 분기 조건: {raw}");
                if (flag(key) == not) return false;
            }
            return true;
        }

        public DHEventBranchTemplate ResolveCompletion(EventScriptCatalog catalog, Func<string, bool> flag)
        {
            if (!catalog.TryGetBranchOptionTemplates(1, BranchId, out var rows))
                throw new InvalidOperationException($"완료 분기 데이터를 찾지 못했습니다: {BranchId}");
            foreach (var row in rows) if (Matches(row, flag)) return row;
            throw new InvalidOperationException("현재 경로에 맞는 완료 분기가 없습니다.");
        }

        public static int ExpPerUnit(string battleKey, float totalExp, int participants)
        {
            if (battleKey == "BE161") return 0; // 아놀드 경험치는 치트에서 항상 제외.
            if (participants <= 0) throw new InvalidOperationException("전투 파티가 비어 있습니다.");
            return Mathf.CeilToInt(Mathf.Max(0, totalExp) / (float)participants);
        }
    }

    // 치트 선택 UI의 임시 계획 데이터입니다. 정식 진행 상태/저장 데이터 모델로 재사용하지 마세요.
    public sealed class JcZone1WarpStep
    {
        public JcZone1WarpRoute Route;
        public IReadOnlyList<DHEventEffectToken> Effects;
        public readonly List<DHEventRewardTemplate> Rewards = new List<DHEventRewardTemplate>();
        public int ExperiencePerUnit;
    }
}
