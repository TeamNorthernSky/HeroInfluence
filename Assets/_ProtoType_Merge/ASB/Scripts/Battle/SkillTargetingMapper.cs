using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.BattleGrid
{
    /// <summary>
    /// 절대 좌표 보드 기준: 패턴 인덱스(0~8, 9, 10)를 좌표 집합으로 변환합니다.
    /// 방향 반전 없이 월드 절대 방향(+X 전방)을 사용합니다.
    /// </summary>
    public static class SkillTargetingMapper
    {
        // 2  3  4
        // 1  0  5
        // 8  7  6
        //
        // 3(전방)은 월드 북쪽(+X) 고정, 캐스터 진영에 따른 반전 없음.

        private static readonly Vector2Int[] PatternIndexToOffset =
        {
            new Vector2Int(0, 0),
            new Vector2Int(0, -1),
            new Vector2Int(1, -1),
            new Vector2Int(1,  0),
            new Vector2Int(1,  1),
            new Vector2Int(0, 1),
            new Vector2Int(-1, 1),
            new Vector2Int(-1, 0),
            new Vector2Int(-1, -1),
        };

        public static HashSet<Vector2Int> GetMultiTargetCoordinates(Vector2Int centerCoords, IEnumerable<int> patternIndices)
        {
            var result = new HashSet<Vector2Int>();
            if (patternIndices == null)
            {
                return result;
            }

            GridManager gridManager = GridManager.Instance;
            if (gridManager == null)
            {
                return result;
            }

            foreach (int idx in patternIndices)
            {
                if (idx == 9)
                {
                    // 열(컬럼): 중심 x와 같은 절대 좌표만 포함
                    List<Vector2Int> coords = gridManager.GetAllCoordsSnapshot();
                    for (int i = 0; i < coords.Count; i++)
                    {
                        Vector2Int c = coords[i];
                        if (c.x == centerCoords.x)
                        {
                            result.Add(c);
                        }
                    }
                    continue;
                }

                if (idx == 10)
                {
                    // 전체 공격(10): 중심 타겟이 속한 진영 보드(아군 0~1 / 적군 2~3)만 포함
                    List<Vector2Int> coords = gridManager.GetAllCoordsSnapshot();
                    bool isEnemySide = centerCoords.x >= 2;
                    for (int i = 0; i < coords.Count; i++)
                    {
                        Vector2Int c = coords[i];
                        bool isCellEnemySide = c.x >= 2;
                        if (isEnemySide != isCellEnemySide)
                        {
                            continue;
                        }

                        result.Add(c);
                    }
                    continue;
                }

                if (idx < 0 || idx >= PatternIndexToOffset.Length)
                {
                    continue;
                }

                Vector2Int abs = centerCoords + PatternIndexToOffset[idx];
                // 보드 밖 좌표는 예외 없이 조용히 제외합니다.
                if (gridManager.TryGetCell(abs, out GridCell _) )
                {
                    result.Add(abs);
                }
            }

            return result;
        }
    }
}
