using System;
using UnityEngine;

/// <summary>상태 객체가 바뀌어도 셀마다 유지되는 공통 시각 기반 왕복 운동입니다.</summary>
public sealed class BattleCellBorderMotion
{
    private bool initialized;
    private BattleCellVisualState state;
    private double start;
    private float frequency, amplitude, height, duration, offset, velocityOffset;
    public void Reset() => initialized = false;

    public float Evaluate(BattleCellVisualState next, BattleCellBorderConfiguration config, double time)
    {
        float nextFrequency = 2f * Mathf.PI / Mathf.Max(.1f, config.bobPeriod) * (next == BattleCellVisualState.ConfirmedArea ? 1.5f : 1f);
        float nextHeight = Mathf.Max(0, config.For(next).shape.floatHeight);
        float nextAmplitude = Mathf.Min(Mathf.Max(0, config.bobAmplitude), nextHeight);
        if (!initialized || state != next || frequency != nextFrequency || height != nextHeight || amplitude != nextAmplitude)
        {
            Sample(time, out float oldPosition, out float oldVelocity);
            bool hadPrevious = initialized;
            state = next; frequency = nextFrequency; height = nextHeight; amplitude = nextAmplitude;
            start = time; duration = Mathf.Max(0, config.resyncDuration); offset = velocityOffset = 0;
            Wave(time, out float target, out float targetVelocity);
            if (hadPrevious && duration > 0) { offset = oldPosition - target; velocityOffset = oldVelocity - targetVelocity; }
            initialized = true;
        }
        Sample(time, out float value, out _);
        return value;
    }

    private void Wave(double time, out float position, out float velocity)
    {
        double phase = time * frequency;
        position = height + amplitude * (float)Math.Sin(phase);
        velocity = amplitude * frequency * (float)Math.Cos(phase);
    }
    private void Sample(double time, out float position, out float velocity)
    {
        Wave(time, out position, out velocity);
        if (duration <= 0 || time - start >= duration) return;
        float t = Mathf.Clamp01((float)(time - start) / duration);
        float t2 = t*t, t3 = t2*t;
        // 시작 위치와 속도를 이어받고, 종료 시 공통 파형의 위치·속도에 정확히 합류합니다.
        position += (2*t3-3*t2+1)*offset + (t3-2*t2+t)*duration*velocityOffset;
        velocity += (6*t2-6*t)*offset/duration + (3*t2-4*t+1)*velocityOffset;
    }
}
