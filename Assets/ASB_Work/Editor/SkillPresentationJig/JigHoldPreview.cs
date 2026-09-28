using System.Collections.Generic;
using UnityEngine.Timeline;

namespace ASB.Work.EditorTools.Jig
{
    /// <summary>
    /// 지그 프리뷰용 Hold 마커(<see cref="PresentationHoldMarker"/>) 판정. 순수 함수만 둔다(테스트 대상).
    ///
    /// 규칙은 런타임 <c>SkillPresentationDirector</c>의 <c>CollectHoldMarks</c>와 재생 루프 판정을 <b>복제</b>한 것이다
    /// (런타임 private 메서드는 에디터에서 호출하지 않는다). 런타임 규칙이 바뀌면 여기도 같이 바꾼다.
    /// 차이: 런타임은 마커 목록 순서대로 검사하지만, 여기서는 시각 순으로 정렬해 앞의 Hold부터 하나씩 처리한다.
    /// </summary>
    public static class JigHoldPreview
    {
        private const double Epsilon = 0.0001d;

        /// <summary>마커 트랙의 Hold 목록(시각, 정지시간 배속-초). DurationSeconds가 0인 마커는 제외, 시각 순 정렬.</summary>
        public static List<KeyValuePair<double, float>> CollectHolds(TimelineAsset timeline)
        {
            var list = new List<KeyValuePair<double, float>>();
            if (timeline == null || timeline.markerTrack == null) return list;

            foreach (IMarker m in timeline.markerTrack.GetMarkers())
            {
                if (m is PresentationHoldMarker hm && hm.DurationSeconds > 0f)
                {
                    list.Add(new KeyValuePair<double, float>(hm.time, hm.DurationSeconds));
                }
            }

            list.Sort((a, b) => a.Key.CompareTo(b.Key));
            return list;
        }

        /// <summary>
        /// 이번 tick 구간 (prevTime, nowTime]에 걸린 미소비 Hold 중 가장 앞의 인덱스. 없으면 -1.
        /// 런타임과 같은 경계: <c>ht &gt; prev + ε &amp;&amp; ht &lt;= now + ε</c> — 구간 시작 시각과 같은 마커는 발동하지 않는다.
        /// </summary>
        public static int FindCrossedHold(IReadOnlyList<KeyValuePair<double, float>> holds, bool[] consumed,
            double prevTime, double nowTime)
        {
            if (holds == null) return -1;

            for (int i = 0; i < holds.Count; i++)
            {
                if (consumed != null && i < consumed.Length && consumed[i]) continue;

                double ht = holds[i].Key;
                if (ht > prevTime + Epsilon && ht <= nowTime + Epsilon) return i;
            }
            return -1;
        }
    }
}
