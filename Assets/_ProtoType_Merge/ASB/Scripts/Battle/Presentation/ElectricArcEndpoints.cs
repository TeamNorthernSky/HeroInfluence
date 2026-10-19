using UnityEngine;

/// <summary>
/// VFX Graph 전기 아크(Ultra Mega Pack ArchedElectricity)의 시작점·끝점을 두 트랜스폼에 맞춘다.
/// <para>
/// 아크 그래프는 노출 속성 Pos1~Pos4(시작·제어점 2개·끝)를 VFXPositionBinder로 자식 트랜스폼에 묶어 둔다.
/// 이 컴포넌트는 그 자식 트랜스폼을 매 프레임 옮기기만 하고 그리기는 그래프가 담당한다.
/// </para>
/// <para>
/// VFXPropertyBinder는 DefaultExecutionOrder(1)의 Update에서 값을 읽으므로 기본 순서(0)의 Update에서 먼저 옮긴다.
/// 끝점 중 하나가 사라지면(유닛 파괴 등) 같은 오브젝트의 이펙트를 정지한다.
/// </para>
/// </summary>
[DisallowMultipleComponent]
public sealed class ElectricArcEndpoints : MonoBehaviour
{
    [SerializeField] private Transform pos1;
    [SerializeField] private Transform pos2;
    [SerializeField] private Transform pos3;
    [SerializeField] private Transform pos4;

    [SerializeField]
    [Tooltip("중간 제어점을 위로 띄우는 높이(월드, m). 0이면 직선.")]
    private float archHeight = 0.6f;

    private Transform _start;
    private Transform _end;
    private bool _bound;

    /// <summary>두 트랜스폼 사이를 잇는다. 이후 매 프레임 따라간다.</summary>
    public void Bind(Transform start, Transform end)
    {
        _start = start;
        _end = end;
        _bound = start != null && end != null;
        if (_bound) Apply(_start.position, _end.position);
    }

    private void Update()
    {
        if (!_bound) return;
        if (_start == null || _end == null)
        {
            _bound = false;
            GetComponent<JC.VFX.VfxEffect>()?.Stop();
            return;
        }

        Apply(_start.position, _end.position);
    }

    private void Apply(Vector3 start, Vector3 end)
    {
        // 그래프 컬링 바운드가 아크를 덮도록 루트를 중점에 둔 뒤 제어점을 월드 좌표로 놓는다.
        transform.position = (start + end) * 0.5f;
        ComputeControlPoints(start, end, archHeight, out Vector3 c1, out Vector3 c2);
        if (pos1 != null) pos1.position = start;
        if (pos2 != null) pos2.position = c1;
        if (pos3 != null) pos3.position = c2;
        if (pos4 != null) pos4.position = end;
    }

    /// <summary>시작·끝을 1/3, 2/3로 나눈 지점을 위로 띄워 베지어 제어점으로 쓴다.</summary>
    public static void ComputeControlPoints(Vector3 start, Vector3 end, float archHeight, out Vector3 c1, out Vector3 c2)
    {
        Vector3 lift = Vector3.up * archHeight;
        c1 = Vector3.Lerp(start, end, 1f / 3f) + lift;
        c2 = Vector3.Lerp(start, end, 2f / 3f) + lift;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        // Ultra Mega 아크 프리팹은 루트 직속에 Pos1~Pos4가 있다.
        pos1 = transform.Find("Pos1");
        pos2 = transform.Find("Pos2");
        pos3 = transform.Find("Pos3");
        pos4 = transform.Find("Pos4");
    }
#endif
}
