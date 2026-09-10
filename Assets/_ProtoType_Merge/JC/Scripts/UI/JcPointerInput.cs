using UnityEngine;

/// <summary>커서와 카메라가 함께 사용하는 입력 상태. UnityEditor 의존성은 두지 않는다.</summary>
public static class JcPointerInput
{
#if UNITY_EDITOR
    // 에디터 디버그 제어만 허용. Development Build/DEBUG도 플레이어에서는 제외한다.
    public static bool EditorManaged { get; set; }
    public static bool EditorPlayInput { get; set; }
    public static Vector2 EditorPosition { get; set; }
    public static Vector2 EditorSize { get; set; }
    private static int suppressThroughFrame = -1;
#endif
    private static Object scrollOwner;
    private static Vector2 scrollDirection;
    private static int scrollFrame = -10;

#if UNITY_EDITOR
    public static bool CanControl => (EditorManaged
        ? EditorPlayInput
        : Application.isFocused) && Time.frameCount > suppressThroughFrame;
    public static Vector2 Position => Application.isEditor && EditorManaged ? EditorPosition : (Vector2)Input.mousePosition;
    public static Vector2 Size => Application.isEditor && EditorManaged ? EditorSize : new Vector2(Screen.width, Screen.height);
#else
    // 플레이어에는 편집 상태/좌표 덮어쓰기/전환 입력 억제 경로 자체가 없다.
    public static bool CanControl => Application.isFocused;
    public static Vector2 Position => Input.mousePosition;
    public static Vector2 Size => new Vector2(Screen.width, Screen.height);
#endif
    public static bool Inside => Position.x >= 0 && Position.y >= 0 && Position.x < Size.x && Position.y < Size.y;
    public static Vector2 ScrollDirection => CanControl && scrollOwner != null && Time.frameCount - scrollFrame <= 1
        ? scrollDirection : Vector2.zero;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
#if UNITY_EDITOR
        EditorManaged = false;
        EditorPlayInput = false;
        suppressThroughFrame = -1;
#endif
        scrollOwner = null;
        scrollDirection = Vector2.zero;
        scrollFrame = -10;
    }

#if UNITY_EDITOR
    public static void SuppressTransitionInput() => suppressThroughFrame = Time.frameCount + 1;
#endif
    public static void ReportScroll(Object owner, Vector2 direction)
    {
        scrollOwner = owner;
        scrollDirection = direction;
        scrollFrame = Time.frameCount;
    }
    public static void ClearScroll(Object owner)
    {
        if (scrollOwner != owner) return;
        scrollDirection = Vector2.zero;
    }

    // 축별 속도를 구한 뒤 방향과 속력을 분리한다. 침범 정도를 두 번 곱하지 않는다.
    public static float AxisSpeed(float coordinate, float length, float inner, float outer, float boundary, float maximum)
    {
        if (length <= 0) return 0;
        inner = Mathf.Clamp(inner, 1, Mathf.Max(1, length * .5f));
        outer = Mathf.Max(1, outer);
        boundary = Mathf.Max(0, boundary);
        maximum = Mathf.Max(boundary, maximum);
        float distance;
        float sign;
        if (coordinate < inner) { distance = -coordinate; sign = -1; }
        else if (coordinate > length - inner) { distance = coordinate - length; sign = 1; }
        else return 0;
        float speed = distance <= 0 ? boundary * Mathf.Clamp01((distance + inner) / inner)
            : Mathf.Lerp(boundary, maximum, Mathf.Clamp01(distance / outer));
        return sign * speed;
    }

    public static Vector2 EdgeVelocity(Vector2 pointer, Vector2 size, JcPointerProfile profile)
    {
        Vector2 axes = new Vector2(
            AxisSpeed(pointer.x, size.x, profile.innerPixels, profile.outerPixels, profile.boundarySpeed, profile.maximumSpeed),
            AxisSpeed(pointer.y, size.y, profile.innerPixels, profile.outerPixels, profile.boundarySpeed, profile.maximumSpeed));
        // 대각선도 같은 깊이의 직선 이동보다 빨라지지 않는다.
        return axes.sqrMagnitude > 0 ? axes.normalized * Mathf.Max(Mathf.Abs(axes.x), Mathf.Abs(axes.y)) : Vector2.zero;
    }
}

#if UNITY_EDITOR
/// <summary>에디터 자동 복귀의 순수 상태 모델. 빌드에는 편집 전환 상태 머신을 포함하지 않는다.</summary>
public sealed class JcPointerSession
{
    public enum Mode { ManualEdit, Playing, AutomaticEdit }
    public Mode State { get; private set; } = Mode.ManualEdit;
    public double ReturnSince { get; private set; } = -1;
    public void Play() { State = Mode.Playing; ReturnSince = -1; }
    public void Edit(bool automatic = false) { State = automatic ? Mode.AutomaticEdit : Mode.ManualEdit; ReturnSince = -1; }
    public void CancelReturn() => ReturnSince = -1;
    public bool Tick(float outsideDistance, float exitDistance, float hysteresis, double now, double delay,
        bool automaticEnabled, bool confined, bool canReturn)
    {
        exitDistance = Mathf.Max(1, exitDistance);
        if (State == Mode.Playing && automaticEnabled && !confined && outsideDistance > exitDistance)
        { Edit(true); return false; }
        if (State != Mode.AutomaticEdit) return false;
        if (!automaticEnabled || confined) { Edit(); return false; }
        float returnDistance = Mathf.Max(0, exitDistance - Mathf.Clamp(hysteresis, 0, exitDistance));
        if (!canReturn || outsideDistance > returnDistance) { CancelReturn(); return false; }
        if (ReturnSince < 0) ReturnSince = now;
        if (now - ReturnSince < System.Math.Max(0, delay)) return false;
        Play();
        return true;
    }
}
#endif
