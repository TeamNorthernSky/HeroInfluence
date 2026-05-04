using UnityEngine;

/// <summary>
/// 맵 카메라 컨트롤러. 약한 Perspective 쿼터뷰, 엣지 스크롤, 방향 커서.
/// </summary>
public class MapCameraController : MonoBehaviour
{
    [Header("Camera Settings")]
    [SerializeField] private float cameraAngleX = 35f;
    [SerializeField] private float fieldOfView = 25f;
    [SerializeField] private float cameraHeight = 30f;

    [Header("Edge Scroll")]
    [SerializeField] private float minScrollSpeed = 5f;
    [SerializeField] private float maxScrollSpeed = 30f;

    private Camera cam;
    private float edgeZoneX = 120f;
    private float edgeZoneY = 120f;
    private bool edgeScrollEnabled = false;

    // 커서
    private const int CursorSize = 32;
    private Texture2D[] cursorTextures;
    private bool isCursorOverridden;
    private int lastCursorIndex = -1;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        GenerateCursorTextures();
    }

    private void Start()
    {
        cam.fieldOfView = fieldOfView;
        transform.rotation = Quaternion.Euler(cameraAngleX, 0f, 0f);

        var grid = FindFirstObjectByType<PlayGridManagerJC>();
        int gridW = grid != null ? grid.Width : 64;
        int gridH = grid != null ? grid.Height : 64;
        float centerX = gridW * PlayGridManagerJC.CellSize * 0.5f;
        float centerZ = gridH * PlayGridManagerJC.CellSize * 0.5f;
        transform.position = new Vector3(centerX, cameraHeight, centerZ - CameraZOffset());
    }

    private void OnDestroy()
    {
        // 커서 텍스처 정리
        if (cursorTextures != null)
        {
            foreach (var tex in cursorTextures)
                if (tex != null) Destroy(tex);
        }
        if (isCursorOverridden)
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    public void SetEdgeZone(float zoneX, float zoneY)
    {
        edgeZoneX = zoneX;
        edgeZoneY = zoneY;
    }

    private void LateUpdate()
    {
        // Tab키로 엣지 스크롤 토글
        if (Input.GetKeyDown(KeyCode.Tab))
            edgeScrollEnabled = !edgeScrollEnabled;

        Vector3 mousePos = Input.mousePosition;

        // 마우스가 화면 밖이면 이동하지 않음
        bool mouseOutside = mousePos.x < 0 || mousePos.x > Screen.width ||
                            mousePos.y < 0 || mousePos.y > Screen.height;

        float moveX = 0f;
        float moveZ = 0f;

        if (edgeScrollEnabled && !mouseOutside)
        {
            moveX = GetAxisIntensity(mousePos.x, Screen.width, edgeZoneX);
            moveZ = GetAxisIntensity(mousePos.y, Screen.height, edgeZoneY);
        }

        UpdateCursor(moveX, moveZ);

        if (Mathf.Approximately(moveX, 0f) && Mathf.Approximately(moveZ, 0f))
        {
            ClampPosition();
            return;
        }

        float intensity = Mathf.Max(Mathf.Abs(moveX), Mathf.Abs(moveZ));
        float speed = Mathf.Lerp(minScrollSpeed, maxScrollSpeed, intensity);

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ).normalized;
        transform.position += moveDir * speed * Time.deltaTime;

        ClampPosition();
    }

    private float GetAxisIntensity(float mouseValue, float screenSize, float zoneSize)
    {
        if (mouseValue < 0)
            return -1f;
        if (mouseValue > screenSize)
            return 1f;

        if (mouseValue < zoneSize)
        {
            float t = 1f - (mouseValue / zoneSize);
            return -t;
        }

        if (mouseValue > screenSize - zoneSize)
        {
            float t = 1f - ((screenSize - mouseValue) / zoneSize);
            return t;
        }

        return 0f;
    }

    private void ClampPosition()
    {
        // 현재는 제한 없이 자유 이동
        Vector3 pos = transform.position;
        pos.y = cameraHeight;
        transform.position = pos;
    }

    private float CameraZOffset()
    {
        return cameraHeight / Mathf.Tan(cameraAngleX * Mathf.Deg2Rad);
    }

    // ========== 커서 ==========

    private void UpdateCursor(float moveX, float moveZ)
    {
        if (Mathf.Approximately(moveX, 0f) && Mathf.Approximately(moveZ, 0f))
        {
            if (isCursorOverridden)
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                isCursorOverridden = false;
                lastCursorIndex = -1;
            }
            return;
        }

        // 8방향 인덱스 결정
        // moveZ>0=상, moveZ<0=하, moveX>0=우, moveX<0=좌
        int dirIndex = GetDirectionIndex(moveX, moveZ);

        if (dirIndex != lastCursorIndex)
        {
            var tex = cursorTextures[dirIndex];
            Cursor.SetCursor(tex, new Vector2(CursorSize * 0.5f, CursorSize * 0.5f), CursorMode.Auto);
            isCursorOverridden = true;
            lastCursorIndex = dirIndex;
        }
    }

    /// <summary>
    /// 0=상,1=우상,2=우,3=우하,4=하,5=좌하,6=좌,7=좌상
    /// </summary>
    private int GetDirectionIndex(float x, float z)
    {
        bool hasX = !Mathf.Approximately(x, 0f);
        bool hasZ = !Mathf.Approximately(z, 0f);

        if (hasZ && !hasX) return z > 0 ? 0 : 4;
        if (hasX && !hasZ) return x > 0 ? 2 : 6;

        // 대각선
        if (x > 0 && z > 0) return 1;
        if (x > 0 && z < 0) return 3;
        if (x < 0 && z < 0) return 5;
        return 7; // x < 0 && z > 0
    }

    private void GenerateCursorTextures()
    {
        cursorTextures = new Texture2D[8];

        // 각 방향의 화살표를 가리키는 단위 벡터
        Vector2[] directions =
        {
            new Vector2(0, 1),     // 상
            new Vector2(1, 1).normalized,   // 우상
            new Vector2(1, 0),     // 우
            new Vector2(1, -1).normalized,  // 우하
            new Vector2(0, -1),    // 하
            new Vector2(-1, -1).normalized, // 좌하
            new Vector2(-1, 0),    // 좌
            new Vector2(-1, 1).normalized   // 좌상
        };

        for (int i = 0; i < 8; i++)
        {
            cursorTextures[i] = CreateArrowTexture(directions[i]);
        }
    }

    private Texture2D CreateArrowTexture(Vector2 dir)
    {
        var tex = new Texture2D(CursorSize, CursorSize, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Point;

        // 투명으로 초기화
        var clear = new Color(0, 0, 0, 0);
        var pixels = new Color[CursorSize * CursorSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

        var arrowColor = new Color(1f, 1f, 1f, 1f);
        var outlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);

        Vector2 center = new Vector2(CursorSize * 0.5f, CursorSize * 0.5f);
        Vector2 tip = center + dir * 13f;
        Vector2 perp = new Vector2(-dir.y, dir.x);

        // 화살표 줄기
        Vector2 tailEnd = center - dir * 8f;
        DrawLineThick(pixels, center - dir * 1f, tailEnd, 1.5f, arrowColor);

        // 화살표 머리 (삼각형)
        Vector2 headBase = center + dir * 4f;
        Vector2 wing1 = headBase + perp * 6f;
        Vector2 wing2 = headBase - perp * 6f;

        DrawLineThick(pixels, tip, wing1, 1.5f, arrowColor);
        DrawLineThick(pixels, tip, wing2, 1.5f, arrowColor);
        DrawLineThick(pixels, wing1, wing2, 1.2f, arrowColor);

        // 삼각형 내부 채우기
        FillTriangle(pixels, tip, wing1, wing2, arrowColor);

        // 아웃라인 (주변 1px)
        var outlined = new Color[pixels.Length];
        System.Array.Copy(pixels, outlined, pixels.Length);
        for (int y = 1; y < CursorSize - 1; y++)
        {
            for (int x = 1; x < CursorSize - 1; x++)
            {
                if (pixels[y * CursorSize + x].a < 0.5f)
                {
                    bool nearArrow = false;
                    for (int dy = -1; dy <= 1 && !nearArrow; dy++)
                        for (int dx = -1; dx <= 1 && !nearArrow; dx++)
                            if (pixels[(y + dy) * CursorSize + (x + dx)].a > 0.5f)
                                nearArrow = true;
                    if (nearArrow)
                        outlined[y * CursorSize + x] = outlineColor;
                }
            }
        }

        tex.SetPixels(outlined);
        tex.Apply();
        return tex;
    }

    private void DrawLineThick(Color[] pixels, Vector2 a, Vector2 b, float thickness, Color col)
    {
        float dist = Vector2.Distance(a, b);
        int steps = Mathf.CeilToInt(dist * 2);
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
            int radius = Mathf.CeilToInt(thickness);
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (dx * dx + dy * dy <= thickness * thickness)
                    {
                        int px = Mathf.RoundToInt(p.x) + dx;
                        int py = Mathf.RoundToInt(p.y) + dy;
                        if (px >= 0 && px < CursorSize && py >= 0 && py < CursorSize)
                            pixels[py * CursorSize + px] = col;
                    }
                }
            }
        }
    }

    private void FillTriangle(Color[] pixels, Vector2 v0, Vector2 v1, Vector2 v2, Color col)
    {
        int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(v0.x, Mathf.Min(v1.x, v2.x))));
        int maxX = Mathf.Min(CursorSize - 1, Mathf.CeilToInt(Mathf.Max(v0.x, Mathf.Max(v1.x, v2.x))));
        int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(v0.y, Mathf.Min(v1.y, v2.y))));
        int maxY = Mathf.Min(CursorSize - 1, Mathf.CeilToInt(Mathf.Max(v0.y, Mathf.Max(v1.y, v2.y))));

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x, y);
                if (PointInTriangle(p, v0, v1, v2))
                    pixels[y * CursorSize + x] = col;
            }
        }
    }

    private bool PointInTriangle(Vector2 p, Vector2 v0, Vector2 v1, Vector2 v2)
    {
        float d1 = Sign(p, v0, v1);
        float d2 = Sign(p, v1, v2);
        float d3 = Sign(p, v2, v0);
        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
        return !(hasNeg && hasPos);
    }

    private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
    {
        return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
    }
}
