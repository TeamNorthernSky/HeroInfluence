using UnityEngine;

/// <summary>
/// Moving Attack의 formation-local 좌표 변환과 Entry/Mid/Exit Catmull-Rom 경로 계산의 단일 출처.
/// UnityEditor 의존성이 없어 런타임과 에디터 프리뷰에서 함께 사용한다.
/// </summary>
public static class MovingAttackPath
{
    public struct Points
    {
        public Vector3 Entry;
        public Vector3 Mid;
        public Vector3 Exit;
    }

    public static Points BuildPoints(MovingAttackPresentation presentation, Vector3 actorOrigin, Vector3 formationCenter)
    {
        Vector3 forward = Flatten(formationCenter - actorOrigin);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                presentation.GetPathOffsets(out Vector2 entryOffset, out Vector2 midOffset, out Vector2 exitOffset);
                float y = actorOrigin.y;
                return new Points
                {
                    Entry = ToWorld(entryOffset, formationCenter, right, forward, y),
                    Mid = ToWorld(midOffset, formationCenter, right, forward, y),
                    Exit = ToWorld(exitOffset, formationCenter, right, forward, y)
                };
            }
        
            public static Vector2 ToOffset(Vector3 worldPoint, Vector3 actorOrigin, Vector3 formationCenter)
    {
        Vector3 forward = Flatten(formationCenter - actorOrigin);
        if (forward.sqrMagnitude <= 0.0001f)
        {
            forward = Vector3.forward;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 delta = worldPoint - formationCenter;
        return new Vector2(Vector3.Dot(delta, right), Vector3.Dot(delta, forward));
    }

    public static Vector3 Evaluate(Points points, float globalT)
    {
        globalT = Mathf.Clamp01(globalT);
        return globalT <= 0.5f
            ? CatmullRom(points.Entry, points.Entry, points.Mid, points.Exit, globalT * 2f)
            : CatmullRom(points.Entry, points.Mid, points.Exit, points.Exit, (globalT - 0.5f) * 2f);
    }

    public static void BuildArcLengthLut(Points points, int sampleCount, Vector3[] samples, float[] cumulativeLengths)
    {
        int count = Mathf.Max(48, sampleCount);
        if (samples == null || cumulativeLengths == null || samples.Length < count + 1 || cumulativeLengths.Length < count + 1)
        {
            throw new System.ArgumentException("MovingAttackPath LUT buffers are too small.");
        }

        for (int i = 0; i <= count; i++)
        {
            samples[i] = Evaluate(points, i / (float)count);
            cumulativeLengths[i] = i == 0 ? 0f : cumulativeLengths[i - 1] + Vector3.Distance(samples[i - 1], samples[i]);
        }
    }

    public static Vector3 EvaluateArcLength(Vector3[] samples, float[] cumulativeLengths, int sampleCount, float distanceProgress)
    {
        int count = Mathf.Max(48, sampleCount);
        if (samples == null || cumulativeLengths == null || samples.Length < count + 1 || cumulativeLengths.Length < count + 1)
        {
            return Vector3.zero;
        }

        float totalLength = cumulativeLengths[count];
        float targetDistance = Mathf.Clamp01(distanceProgress) * totalLength;
        int low = 0;
        int high = count;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (cumulativeLengths[middle] < targetDistance) low = middle + 1;
            else high = middle;
        }

        int upper = Mathf.Clamp(low, 1, count);
        int lower = upper - 1;
        float segmentLength = cumulativeLengths[upper] - cumulativeLengths[lower];
        float localT = segmentLength <= Mathf.Epsilon ? 0f :
            (targetDistance - cumulativeLengths[lower]) / segmentLength;
        return Vector3.Lerp(samples[lower], samples[upper], localT);
    }

    public static float FindNearestProgress(Vector3[] samples, float[] cumulativeLengths, int sampleCount, Vector3 targetPosition)
    {
        int count = Mathf.Max(48, sampleCount);
        if (samples == null || cumulativeLengths == null || samples.Length < count + 1 || cumulativeLengths.Length < count + 1)
        {
            return 1f;
        }

        float bestSqrDistance = float.MaxValue;
        int bestIndex = 0;
        for (int i = 0; i <= count; i++)
        {
            Vector3 delta = samples[i] - targetPosition;
            delta.y = 0f;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestIndex = i;
            }
        }

        float totalLength = cumulativeLengths[count];
        return totalLength <= Mathf.Epsilon ? 1f : cumulativeLengths[bestIndex] / totalLength;
    }

    private static Vector3 ToWorld(Vector2 offset, Vector3 center, Vector3 right, Vector3 forward, float y)
    {
        Vector3 point = center + right * offset.x + forward * offset.y;
        point.y = y;
        return point;
    }

    private static Vector3 Flatten(Vector3 value)
    {
        value.y = 0f;
        return value;
    }

    private static Vector3 CatmullRom(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * ((2f * c1)
            + (-c0 + c2) * t
            + (2f * c0 - 5f * c1 + 4f * c2 - c3) * t2
            + (-c0 + 3f * c1 - 3f * c2 + c3) * t3);
    }
}
