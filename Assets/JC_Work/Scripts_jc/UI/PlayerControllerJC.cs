using UnityEngine;

/// <summary>
/// 임시 플레이어 컨트롤러.
/// 우클릭(누르기/드래그) 위치로 이동, 시야 반경 속성 보유.
/// </summary>
public class PlayerControllerJC : MonoBehaviour
{
    [Header("Player Settings")]
    [SerializeField] private int sightRadius = 5;
    [SerializeField] private float moveSpeed = 8f;

    private PlayGridManagerJC grid;
    private PlayFogManagerJC fogOfWar;
    private Vector2Int currentGridPos;
    private Vector3 targetWorldPos;
    private bool hasTarget;

    public int SightRadius => sightRadius;
    public Vector2Int CurrentGridPos => currentGridPos;

    private void Start()
    {
        grid = FindFirstObjectByType<PlayGridManagerJC>();
        if (grid == null)
        {
            Debug.LogError("[PlayerControllerJC] PlayGridManagerJC를 찾을 수 없습니다");
            return;
        }

        fogOfWar = FindFirstObjectByType<PlayFogManagerJC>();

        currentGridPos = PlayGridManagerJC.WorldToGrid(transform.position);
        targetWorldPos = PlayGridManagerJC.GridToWorld(currentGridPos);
        targetWorldPos.y = transform.position.y;
        transform.position = targetWorldPos;

        // 시작 위치 시야 공개
        fogOfWar?.UpdatePlayerVisibility(currentGridPos, sightRadius);
    }

    private void Update()
    {
        if (grid == null) return;

        // 우클릭 누르는 중이면 계속 목표 갱신 (드래그 추적)
        if (Input.GetMouseButton(1))
        {
            HandleRightClick();
        }

        if (hasTarget)
        {
            MoveToTarget();
        }
    }

    private void HandleRightClick()
    {
        var ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        var plane = new Plane(Vector3.up, Vector3.zero);
        if (!plane.Raycast(ray, out float distance)) return;

        Vector3 hitPoint = ray.GetPoint(distance);
        Vector2Int targetGrid = PlayGridManagerJC.WorldToGrid(hitPoint);

        // 맵 경계 내로 클램프
        targetGrid.x = Mathf.Clamp(targetGrid.x, 0, grid.Width - 1);
        targetGrid.y = Mathf.Clamp(targetGrid.y, 0, grid.Height - 1);

        currentGridPos = targetGrid;
        targetWorldPos = PlayGridManagerJC.GridToWorld(targetGrid);
        targetWorldPos.y = transform.position.y;
        hasTarget = true;
    }

    private void MoveToTarget()
    {
        transform.position = Vector3.MoveTowards(transform.position, targetWorldPos, moveSpeed * Time.deltaTime);

        // 이동 중 현재 위치 기준 시야 갱신
        var movingGridPos = PlayGridManagerJC.WorldToGrid(transform.position);
        if (movingGridPos != currentGridPos)
        {
            currentGridPos = movingGridPos;
            fogOfWar?.UpdatePlayerVisibility(currentGridPos, sightRadius);
        }

        if (Vector3.Distance(transform.position, targetWorldPos) < 0.01f)
        {
            transform.position = targetWorldPos;
            hasTarget = false;
            currentGridPos = PlayGridManagerJC.WorldToGrid(transform.position);
            fogOfWar?.UpdatePlayerVisibility(currentGridPos, sightRadius);
        }
    }
}
