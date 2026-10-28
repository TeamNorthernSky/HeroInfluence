using System.Collections.Generic;
using UnityEngine;

namespace JC.VFX
{
    /// <summary>
    /// 전투 격자에서 「십자로 인접한 칸」을 고르는 규칙. LFL 연쇄처럼 <b>이웃 칸만</b> 퍼지는 연출이 쓴다.
    ///
    /// ★이건 임시 대용품이다. 정확한 판정은 격자 인덱스(<c>col×100 + row</c>)로 <c>|Δcol| + |Δrow| == 1</c>
    ///   을 보는 것이고, 그건 격자를 아는 스킬 로직의 몫이다. 여기 있는 것은 <b>좌표 거리로 그걸 흉내내는</b>
    ///   근사이며, 캐릭터가 칸 중앙에서 벗어나면 오판한다.
    ///   그래도 남겨 두는 이유는 테스트베드·프리뷰가 스킬 로직 없이도 굴러가야 하기 때문이다.
    ///
    /// ★규칙을 여기 한 곳에만 둔다. 예전에 이펙트 안에만 있던 판정이 실측 격자와 어긋나
    ///   (cellSize 2.0 vs 실측 3.30) <b>연쇄가 통째로 0건</b>이 된 적이 있다 — 260804.
    ///   두 군데에 같은 규칙을 적어 두면 한쪽만 고치게 된다.
    /// </summary>
    public static class VfxGridAdjacency
    {
        /// <summary>실측 칸 간격(m). Grid_c_r 의 x·z 가 모두 3.30 이다.</summary>
        public const float DefaultCellSize = 3.3f;

        /// <summary>수직축 어긋남 허용오차(m). 이 값보다 가까우면 「같은 줄」로 본다.</summary>
        public const float DefaultAxisTol = 0.6f;

        /// <summary>
        /// <paramref name="origin"/> 기준 상하좌우 1칸에 있는 후보를 가까운 순으로 <paramref name="result"/> 에 채운다.
        /// 대각·2칸은 제외한다. <paramref name="result"/> 는 먼저 비워진다.
        /// </summary>
        /// <param name="exclude">제외할 대상(보통 시전자·본 대상). null 은 무시된다.</param>
        public static void ResolveCross(
            Vector3 origin,
            IReadOnlyList<Transform> candidates,
            List<Transform> result,
            IReadOnlyList<Transform> exclude = null,
            float cellSize = DefaultCellSize,
            float axisTol = DefaultAxisTol,
            int maxCount = 4)
        {
            result.Clear();
            if (candidates == null) return;

            var scored = new List<(Transform t, float d)>();
            float upper = cellSize + axisTol;

            for (int i = 0; i < candidates.Count; i++)
            {
                Transform c = candidates[i];
                if (c == null || IsExcluded(c, exclude)) continue;

                Vector3 d = c.position - origin;
                float ax = Mathf.Abs(d.x), az = Mathf.Abs(d.z);

                // 한 축은 1칸 거리(+여유) 안, 나머지 축은 허용오차 안. 자기 자신(양축 0)은 하한으로 걸러진다.
                bool horizontal = ax <= upper && ax > axisTol && az <= axisTol;
                bool vertical = az <= upper && az > axisTol && ax <= axisTol;
                if (horizontal || vertical) scored.Add((c, d.sqrMagnitude));
            }

            scored.Sort((a, b) => a.d.CompareTo(b.d));
            for (int i = 0; i < scored.Count && i < maxCount; i++)
                result.Add(scored[i].t);
        }

        private static bool IsExcluded(Transform t, IReadOnlyList<Transform> exclude)
        {
            if (exclude == null) return false;
            for (int i = 0; i < exclude.Count; i++)
                if (exclude[i] == t) return true;
            return false;
        }
    }
}
