using UnityEngine;

/// <summary>
/// [JC 신설 260707] 가시성 경계 SDF(부호 있는 거리장) 빌더.
/// FogGridManager의 탐색 상태(IsExplored)를 경계 기준으로, 셀 단위 부호 거리(+=안개 쪽, -=개방 쪽)를
/// 2패스 챔퍼 거리변환으로 계산해 RFloat 텍스처(bilinear)로 굽고 전역 _FogDistanceTex로 푸시한다.
/// FogOfWarHI 셰이더가 등고선 smoothstep으로 소비 — 셀 격자를 벗어난 영역 기반의 매끄러운 경계.
/// 재계산은 fog 변경 이벤트가 있었던 프레임에만 수행(평시 프레임 비용 0). 수명은 RenderFXManager가 관리.
/// </summary>
public class FogDistanceField
{
    private static readonly int FogDistanceTexId = Shader.PropertyToID("_FogDistanceTex");
    private static readonly int FogDistanceTexBoundId = Shader.PropertyToID("_FogDistanceTexBound");
    private const float Diag = 1.4142f;
    private const float Far = 4096f;

    private FogGridManager grid;
    private Texture2D texture;
    private float[] distToExplored;
    private float[] distToUnexplored;
    private float[] signedDist;
    private bool dirty;

    private float roundingCells;
    private float[] blurKernel;
    private float blurKernelForRounding = -1f;

    /// <summary>경계 라운딩 반경(셀 단위). 거리장에 가우시안 블러를 걸어 윤곽 모서리를 둥글린다.</summary>
    public void SetRounding(float cells)
    {
        cells = Mathf.Max(0f, cells);
        if (Mathf.Approximately(cells, roundingCells)) return;
        roundingCells = cells;
        dirty = true;
    }

    public void Attach(FogGridManager gridManager)
    {
        Detach();
        if (gridManager == null) return;

        grid = gridManager;
        grid.FogChanged += MarkDirty;
        grid.CellVisibilityChanged += HandleCellChanged;
        dirty = true;
    }

    public void Detach()
    {
        if (grid != null)
        {
            grid.FogChanged -= MarkDirty;
            grid.CellVisibilityChanged -= HandleCellChanged;
            grid = null;
        }

        Shader.SetGlobalFloat(FogDistanceTexBoundId, 0f);
        if (texture != null)
        {
            Object.Destroy(texture);
            texture = null;
        }
        dirty = false;
    }

    public void MarkDirty() => dirty = true;

    private void HandleCellChanged(Vector2Int cell, FogVisibilityState state) => dirty = true;

    /// <summary>변경이 있었으면 재빌드. 매 프레임 호출해도 변경 없으면 no-op.</summary>
    public void RebuildIfDirty()
    {
        if (!dirty || grid == null) return;
        dirty = false;

        Vector2Int size = grid.GridSize;
        int w = size.x, h = size.y;
        int count = w * h;
        if (count < 1) return;

        if (signedDist == null || signedDist.Length != count)
        {
            distToExplored = new float[count];
            distToUnexplored = new float[count];
            signedDist = new float[count];
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                bool explored = grid.IsExplored(new Vector2Int(x, y));
                distToExplored[i] = explored ? 0f : Far;
                distToUnexplored[i] = explored ? Far : 0f;
            }
        }

        Chamfer(distToExplored, w, h);
        Chamfer(distToUnexplored, w, h);

        // 안개(미탐) 셀 = +탐색경계까지 거리 / 개방 셀 = -미탐경계까지 거리 (둘 중 하나는 0)
        for (int i = 0; i < count; i++)
            signedDist[i] = distToExplored[i] - distToUnexplored[i];

        ApplyRounding(w, h);

        if (texture == null || texture.width != w || texture.height != h)
        {
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(w, h, TextureFormat.RFloat, false, true)
            {
                name = "FogDistanceField",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        texture.SetPixelData(signedDist, 0);
        texture.Apply(false, false);
        Shader.SetGlobalTexture(FogDistanceTexId, texture);
        Shader.SetGlobalFloat(FogDistanceTexBoundId, 1f);
    }

    // 거리장 가우시안 분리 블러 = 등고선 모서리 라운딩 (반경 ≈ roundingCells).
    // distToExplored를 스크래치로 재사용 — signedDist 계산 후에는 소비처가 없다.
    private void ApplyRounding(int w, int h)
    {
        int r = Mathf.CeilToInt(roundingCells);
        if (r < 1) return;

        // 캐시 키는 연속값(roundingCells) — 정수 반경 r로 캐시하면 시그마 변화가
        // 반경 임계(셀 단위)에서만 반영되는 계단 현상이 생긴다
        if (!Mathf.Approximately(blurKernelForRounding, roundingCells))
        {
            blurKernelForRounding = roundingCells;
            blurKernel = new float[2 * r + 1];
            // 하한은 0 나눗셈 방지용 최소치만 — 크게 잡으면 저구간(라운딩<1셀)에서 변화가 잠긴다
            float sigma = Mathf.Max(roundingCells * 0.5f, 0.05f);
            float sum = 0f;
            for (int k = -r; k <= r; k++)
            {
                float v = Mathf.Exp(-(k * k) / (2f * sigma * sigma));
                blurKernel[k + r] = v;
                sum += v;
            }
            for (int k = 0; k < blurKernel.Length; k++)
                blurKernel[k] /= sum;
        }

        float[] scratch = distToExplored;

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int k = -r; k <= r; k++)
                    acc += signedDist[row + Mathf.Clamp(x + k, 0, w - 1)] * blurKernel[k + r];
                scratch[row + x] = acc;
            }
        }

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int k = -r; k <= r; k++)
                    acc += scratch[Mathf.Clamp(y + k, 0, h - 1) * w + x] * blurKernel[k + r];
                signedDist[y * w + x] = acc;
            }
        }
    }

    // 2패스 챔퍼 거리변환 (직교 1, 대각 √2 근사)
    private static void Chamfer(float[] d, int w, int h)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                float v = d[i];
                if (x > 0) v = Mathf.Min(v, d[i - 1] + 1f);
                if (y > 0)
                {
                    v = Mathf.Min(v, d[i - w] + 1f);
                    if (x > 0) v = Mathf.Min(v, d[i - w - 1] + Diag);
                    if (x < w - 1) v = Mathf.Min(v, d[i - w + 1] + Diag);
                }
                d[i] = v;
            }
        }

        for (int y = h - 1; y >= 0; y--)
        {
            for (int x = w - 1; x >= 0; x--)
            {
                int i = y * w + x;
                float v = d[i];
                if (x < w - 1) v = Mathf.Min(v, d[i + 1] + 1f);
                if (y < h - 1)
                {
                    v = Mathf.Min(v, d[i + w] + 1f);
                    if (x < w - 1) v = Mathf.Min(v, d[i + w + 1] + Diag);
                    if (x > 0) v = Mathf.Min(v, d[i + w - 1] + Diag);
                }
                d[i] = v;
            }
        }
    }
}
