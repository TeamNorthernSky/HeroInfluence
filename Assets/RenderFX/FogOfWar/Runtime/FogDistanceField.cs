using UnityEngine;

/// <summary>
/// [JC 신설 260707 / 260714 이중화 / 260806 걷힘 연출·층별 속도] 가시성 경계 SDF(부호 있는 거리장) 빌더.
/// FogGridManager의 상태를 경계 기준으로, 셀 단위 부호 거리(+=안개 쪽, -=개방 쪽)를
/// 2패스 챔퍼 거리변환으로 계산해 RGBAFloat 텍스처(bilinear)로 굽고 전역 _FogDistanceTex로 푸시한다.
///   R = IsExplored 경계 (짙은 안개층 등고선) — 짙은층 속도로 수렴
///   G = IsVisible  경계 (Fogged 베일층 등고선) — 베일층 속도로 수렴
///   B = IsExplored 경계 시트용 사본 / A = IsVisible 경계 시트용 사본 — 구름 시트 속도로 수렴
/// FogOfWarHI 셰이더가 base는 RG, 구름 시트는 BA를 샘플 — 상태 3겹(짙은층·베일·시트)이 각자 속도로 걷힌다.
///
/// 걷힘 연출: 목표 필드(target)와 표시 필드(shown)를 분리 — 상태 변경 시 target만 즉시 재계산하고,
/// shown이 매 프레임 등속(셀/초)으로 target에 수렴한다. 거리값이 등속으로 움직이면 등고선(=안개 벽)도
/// 정확히 그 속도로 미끄러진다(선형). 수렴 중 새 리빌이 와도 target만 갱신되어 끊김 없이 이어진다.
/// 씬 부착 직후 유예창과 명시 Snap은 즉시 반영 — 씬 진입/세이브 복구/시야 리셋에는 연출을 걸지 않는다.
/// 수명은 RenderFXManager가 관리(매 프레임 Tick 호출).
/// </summary>
public class FogDistanceField
{
    private static readonly int FogDistanceTexId = Shader.PropertyToID("_FogDistanceTex");
    private static readonly int FogDistanceTexBoundId = Shader.PropertyToID("_FogDistanceTexBound");
    private const float Diag = 1.4142f;
    private const float Far = 4096f;
    private const float SnapGraceSeconds = 1f;   // Attach 직후 이 시간 안의 변경은 즉시 반영 (씬 진입·세이브 복구)

    private FogGridManager grid;
    private Texture2D texture;
    private float[] distToOpen;       // 스크래치: 개방(explored/visible) 셀까지 거리
    private float[] distToClosed;     // 스크래치: 닫힌 셀까지 거리
    private float[] targetExplored;   // 목표: IsExplored 경계 (R·B 공용 목표)
    private float[] targetVisible;    // 목표: IsVisible 경계 (G·A 공용 목표)
    private float[] shownExplored;    // R 표시: 짙은층 속도로 수렴
    private float[] shownVisible;     // G 표시: 베일층 속도로 수렴
    private float[] shownSheetExplored; // B 표시: 시트 속도로 수렴
    private float[] shownSheetVisible;  // A 표시: 시트 속도로 수렴
    private float[] packed;           // RGBA 인터리브 업로드 버퍼
    private int texW, texH;
    private bool dirty;
    private bool animating;
    private float snapUntil;
    private float speedExploredCells; // 짙은층 걷힘 속도(셀/초). 0 = 즉시
    private float speedVisibleCells;  // 베일층 걷힘 속도(셀/초). 0 = 즉시
    private float speedSheetCells;    // 구름 시트 걷힘 속도(셀/초). 0 = 즉시

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

    /// <summary>층별 걷힘 연출 속도(셀/초). 0인 층은 연출 없이 즉시 반영.</summary>
    public void SetRevealSpeeds(float exploredCellsPerSec, float visibleCellsPerSec, float sheetCellsPerSec)
    {
        speedExploredCells = Mathf.Max(0f, exploredCellsPerSec);
        speedVisibleCells = Mathf.Max(0f, visibleCellsPerSec);
        speedSheetCells = Mathf.Max(0f, sheetCellsPerSec);
    }

    public void Attach(FogGridManager gridManager)
    {
        Detach();
        if (gridManager == null) return;

        grid = gridManager;
        grid.FogChanged += MarkDirty;
        grid.CellVisibilityChanged += HandleCellChanged;
        dirty = true;
        snapUntil = Time.time + SnapGraceSeconds;
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
        animating = false;
    }

    public void MarkDirty() => dirty = true;

    private void HandleCellChanged(Vector2Int cell, FogVisibilityState state) => dirty = true;

    /// <summary>매 프레임 호출. 변경이 있으면 target 재계산 후 shown을 등속 전진 — 평시 프레임 비용 0.</summary>
    public void Tick(float deltaTime)
    {
        if (grid == null) return;

        if (dirty)
        {
            bool resized = RebuildTarget();
            if (resized || Time.time < snapUntil)
                SnapShownToTarget();
            else
                animating = true;
        }

        if (animating)
            AdvanceShown(deltaTime);
    }

    /// <summary>즉시 반영 — 필요 시 target 재계산 후 shown=target 업로드. 시야 리셋 등 연출 생략 지점용.</summary>
    public void SnapToTarget()
    {
        if (grid == null) return;
        if (dirty) RebuildTarget();
        SnapShownToTarget();
    }

    // target 필드 재계산. 그리드 크기가 바뀌어 버퍼를 재할당했으면 true(호출부가 snap 처리).
    private bool RebuildTarget()
    {
        dirty = false;

        Vector2Int size = grid.GridSize;
        int w = size.x, h = size.y;
        int count = w * h;
        if (count < 1) return false;

        bool resized = targetExplored == null || targetExplored.Length != count;
        if (resized)
        {
            distToOpen = new float[count];
            distToClosed = new float[count];
            targetExplored = new float[count];
            targetVisible = new float[count];
            shownExplored = new float[count];
            shownVisible = new float[count];
            shownSheetExplored = new float[count];
            shownSheetVisible = new float[count];
            packed = new float[count * 4];
        }

        texW = w;
        texH = h;

        BuildSignedField(w, h, count, exploredBoundary: true, targetExplored);
        BuildSignedField(w, h, count, exploredBoundary: false, targetVisible);

        ApplyRounding(targetExplored, w, h);
        ApplyRounding(targetVisible, w, h);

        return resized;
    }

    private void SnapShownToTarget()
    {
        if (targetExplored == null) return;

        System.Array.Copy(targetExplored, shownExplored, targetExplored.Length);
        System.Array.Copy(targetVisible, shownVisible, targetVisible.Length);
        System.Array.Copy(targetExplored, shownSheetExplored, targetExplored.Length);
        System.Array.Copy(targetVisible, shownSheetVisible, targetVisible.Length);
        animating = false;
        UploadShown();
    }

    // shown을 target으로 층별 등속 전진(선형) — 각 층의 등고선이 자기 속도로 미끄러진다.
    private void AdvanceShown(float deltaTime)
    {
        int count = texW * texH;
        bool remaining = false;
        remaining |= AdvanceChannel(shownExplored, targetExplored, speedExploredCells * deltaTime, count);
        remaining |= AdvanceChannel(shownVisible, targetVisible, speedVisibleCells * deltaTime, count);
        remaining |= AdvanceChannel(shownSheetExplored, targetExplored, speedSheetCells * deltaTime, count);
        remaining |= AdvanceChannel(shownSheetVisible, targetVisible, speedSheetCells * deltaTime, count);

        animating = remaining;
        UploadShown();
    }

    // 채널 1벌 전진. step<=0(속도 0)은 즉시 도달. 미수렴 원소가 남아 있으면 true.
    private static bool AdvanceChannel(float[] shown, float[] target, float step, int count)
    {
        if (step <= 0f)
        {
            System.Array.Copy(target, shown, count);
            return false;
        }

        bool remaining = false;
        for (int i = 0; i < count; i++)
        {
            float v = Mathf.MoveTowards(shown[i], target[i], step);
            shown[i] = v;
            if (v != target[i]) remaining = true;
        }

        return remaining;
    }

    private void UploadShown()
    {
        int count = texW * texH;
        if (count < 1 || shownExplored == null) return;

        for (int i = 0; i < count; i++)
        {
            int p = i * 4;
            packed[p] = shownExplored[i];
            packed[p + 1] = shownVisible[i];
            packed[p + 2] = shownSheetExplored[i];
            packed[p + 3] = shownSheetVisible[i];
        }

        if (texture == null || texture.width != texW || texture.height != texH)
        {
            if (texture != null) Object.Destroy(texture);
            texture = new Texture2D(texW, texH, TextureFormat.RGBAFloat, false, true)
            {
                name = "FogDistanceField",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        texture.SetPixelData(packed, 0);
        texture.Apply(false, false);
        Shader.SetGlobalTexture(FogDistanceTexId, texture);
        Shader.SetGlobalFloat(FogDistanceTexBoundId, 1f);
    }

    // 개방 판정(explored 또는 visible) 기준의 부호 거리장을 result에 채운다.
    // 닫힌(안개) 셀 = +개방경계까지 거리 / 개방 셀 = -닫힌경계까지 거리 (둘 중 하나는 0)
    private void BuildSignedField(int w, int h, int count, bool exploredBoundary, float[] result)
    {
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                var cell = new Vector2Int(x, y);
                bool open = exploredBoundary ? grid.IsExplored(cell) : grid.IsVisible(cell);
                distToOpen[i] = open ? 0f : Far;
                distToClosed[i] = open ? Far : 0f;
            }
        }

        Chamfer(distToOpen, w, h);
        Chamfer(distToClosed, w, h);

        for (int i = 0; i < count; i++)
            result[i] = distToOpen[i] - distToClosed[i];
    }

    // 거리장 가우시안 분리 블러 = 등고선 모서리 라운딩 (반경 ≈ roundingCells).
    // distToOpen을 스크래치로 재사용 — signed 계산 후에는 소비처가 없다.
    private void ApplyRounding(float[] data, int w, int h)
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

        float[] scratch = distToOpen;

        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                float acc = 0f;
                for (int k = -r; k <= r; k++)
                    acc += data[row + Mathf.Clamp(x + k, 0, w - 1)] * blurKernel[k + r];
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
                data[y * w + x] = acc;
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
