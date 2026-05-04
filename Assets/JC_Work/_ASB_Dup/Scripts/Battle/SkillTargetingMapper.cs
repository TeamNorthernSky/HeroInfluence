using System.Collections.Generic;
using UnityEngine;

namespace ASB.Work.BattleGrid
{
    /// <summary>
    /// 2×3 전투 그리드 기준: 패턴 인덱스(0~8)를 절대 좌표 집합으로 변환한다.
    /// 인덱스 0은 항상 중심 셀(center)과 동일한 오프셋이다.
    /// </summary>
    public static class SkillTargetingMapper
    {
        /// <summary>고정 그리드 가로(열).</summary>
        public const int GridWidth = 2;

        /// <summary>고정 그리드 세로(행).</summary>
        public const int GridHeight = 3;

        /// <summary>
        /// 인덱스 0 = 중심, 1~8 = 3×3 이웃(행 우선: 상단행 좌→우, 중간행, 하단행).
        /// </summary>
        private static readonly Vector2Int[] PatternIndexToOffset =
        {
            new Vector2Int(0, 0),
            new Vector2Int(-1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(1, -1),
            new Vector2Int(-1, 0),
            new Vector2Int(1, 0),
            new Vector2Int(-1, 1),
            new Vector2Int(0, 1),
            new Vector2Int(1, 1),
        };

        public static HashSet<Vector2Int> GetMultiTargetCoordinates(Vector2Int centerCoords, IEnumerable<int> patternIndices)
        {
            var result = new HashSet<Vector2Int>();
            if (patternIndices == null)
            {
                return result;
            }

            foreach (int idx in patternIndices)
            {
                if (idx < 0 || idx >= PatternIndexToOffset.Length)
                {
                    continue;
                }

                Vector2Int abs = centerCoords + PatternIndexToOffset[idx];
                if (IsInBounds(abs))
                {
                    result.Add(abs);
                }
            }

            return result;
        }

        private static bool IsInBounds(Vector2Int c)
        {
            return c.x >= 0 && c.x < GridWidth && c.y >= 0 && c.y < GridHeight;
        }
    }
}
