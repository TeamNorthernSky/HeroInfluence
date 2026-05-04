using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// KJ_Work 전장의 안개 매니저.
/// Grid 기반 가시 영역을 Texture2D로 만들어 전역 셰이더 변수로 전달한다.
/// </summary>
public class KJ_FogOfWarManager : MonoBehaviour
{
    [Header("References")]
    public GridManager gridManager;
    public PartyGridMover[] partyMovers;

    [Header("Fog Map Settings")]
    [Tooltip("안개 맵 가로 셀 개수")]
    public int mapGridWidth = 128;
    [Tooltip("안개 맵 세로 셀 개수")]
    public int mapGridHeight = 128;

    [Header("Coordinate Mapping")]
    [Tooltip("그리드 원점이 중앙일 때 텍스처 인덱스가 음수가 되지 않도록 보정하는 X 오프셋")]
    public int gridOriginOffsetX = 64;
    [Tooltip("그리드 원점이 중앙일 때 텍스처 인덱스가 음수가 되지 않도록 보정하는 Y 오프셋")]
    public int gridOriginOffsetY = 64;

    [Tooltip("시야 반경 셀 수. 현재 위치를 포함한 정사각형 범위로 공개한다.")]
    public int sightRadiusCells = 1;

    [Tooltip("탐험 완료 지역 밝기. 1은 완전 공개, 0은 완전 비공개")]
    [Range(0f, 1f)]
    public float exploredBrightness = 0.5f;

    [Header("State (ReadOnly)")]
    [SerializeField] private Texture2D fogExploredTex;
    [SerializeField] private Texture2D fogCurrentTex;

    private Color[] exploredPixels;
    private Color[] currentPixels;

    private static readonly int CurrentTexGlobalId = Shader.PropertyToID("_VisibilityCurrentTex");
    private static readonly int ExploredTexGlobalId = Shader.PropertyToID("_VisibilityExploredTex");
    private static readonly int GridWorldSizeId = Shader.PropertyToID("_GridWorldSize");
    private static readonly int GridWorldOffsetId = Shader.PropertyToID("_GridWorldOffset");

    private void Start()
    {
        if (gridManager == null)
            gridManager = FindObjectOfType<GridManager>();

        if (partyMovers == null || partyMovers.Length == 0)
            partyMovers = FindObjectsOfType<PartyGridMover>();

        fogExploredTex = new Texture2D(mapGridWidth, mapGridHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "FogTex_Explored_KJ"
        };

        fogCurrentTex = new Texture2D(mapGridWidth, mapGridHeight, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "FogTex_Current_KJ"
        };

        exploredPixels = new Color[mapGridWidth * mapGridHeight];
        currentPixels = new Color[mapGridWidth * mapGridHeight];

        for (int i = 0; i < exploredPixels.Length; i++)
        {
            exploredPixels[i] = Color.clear;
            currentPixels[i] = Color.clear;
        }

        fogExploredTex.SetPixels(exploredPixels);
        fogExploredTex.Apply();

        fogCurrentTex.SetPixels(currentPixels);
        fogCurrentTex.Apply();

        float cellSize = gridManager != null ? gridManager.CellSize : 1f;
        float worldW = mapGridWidth * cellSize;
        float worldH = mapGridHeight * cellSize;
        Shader.SetGlobalVector(GridWorldSizeId, new Vector4(worldW, worldH, 1f / worldW, 1f / worldH));

        float worldOffsetX = gridOriginOffsetX * cellSize;
        float worldOffsetY = gridOriginOffsetY * cellSize;
        Shader.SetGlobalVector(GridWorldOffsetId, new Vector2(worldOffsetX, worldOffsetY));

        Shader.SetGlobalTexture(CurrentTexGlobalId, fogCurrentTex);
        Shader.SetGlobalTexture(ExploredTexGlobalId, fogExploredTex);

        foreach (var mover in partyMovers)
        {
            if (mover != null)
                mover.PathUpdated += OnPathUpdated;
        }

        RefreshAllPartyVision();
    }

    private void OnDestroy()
    {
        if (partyMovers != null)
        {
            foreach (var mover in partyMovers)
            {
                if (mover != null)
                    mover.PathUpdated -= OnPathUpdated;
            }
        }

        if (fogExploredTex != null)
            Destroy(fogExploredTex);

        if (fogCurrentTex != null)
            Destroy(fogCurrentTex);
    }

    private void OnPathUpdated(List<Vector2Int> path)
    {
        RefreshAllPartyVision();
    }

    public void RevealGrid(Vector2Int gridPos)
    {
        int texCenterX = gridPos.x + gridOriginOffsetX;
        int texCenterY = gridPos.y + gridOriginOffsetY;

        for (int x = texCenterX - sightRadiusCells; x <= texCenterX + sightRadiusCells; x++)
        {
            for (int y = texCenterY - sightRadiusCells; y <= texCenterY + sightRadiusCells; y++)
            {
                if (x < 0 || x >= mapGridWidth || y < 0 || y >= mapGridHeight)
                    continue;

                int index = y * mapGridWidth + x;
                exploredPixels[index] = new Color(exploredBrightness, exploredBrightness, exploredBrightness, 1f);
            }
        }

        fogExploredTex.SetPixels(exploredPixels);
        fogExploredTex.Apply();
    }

    private void RefreshAllPartyVision()
    {
        if (partyMovers == null || partyMovers.Length == 0)
            return;

        for (int i = 0; i < currentPixels.Length; i++)
            currentPixels[i] = Color.clear;

        foreach (var mover in partyMovers)
        {
            if (mover == null)
                continue;

            Vector2Int pos = mover.GetCurrentGrid();
            int texCenterX = pos.x + gridOriginOffsetX;
            int texCenterY = pos.y + gridOriginOffsetY;

            for (int x = texCenterX - sightRadiusCells; x <= texCenterX + sightRadiusCells; x++)
            {
                for (int y = texCenterY - sightRadiusCells; y <= texCenterY + sightRadiusCells; y++)
                {
                    if (x < 0 || x >= mapGridWidth || y < 0 || y >= mapGridHeight)
                        continue;

                    int index = y * mapGridWidth + x;
                    currentPixels[index] = new Color(1f, 0f, 0f, 1f);
                    exploredPixels[index] = new Color(exploredBrightness, exploredBrightness, exploredBrightness, 1f);
                }
            }
        }

        fogCurrentTex.SetPixels(currentPixels);
        fogCurrentTex.Apply();

        fogExploredTex.SetPixels(exploredPixels);
        fogExploredTex.Apply();
    }
}
