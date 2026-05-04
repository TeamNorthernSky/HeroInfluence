using System.Collections.Generic;
using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal static class ForgeOutlineDetect
    {
        public class SourceCache
        {
            public int W, H;
            public Color32[] Pixels;
            public float[] LabL;
            public float[] LabA;
            public float[] LabB;
            public byte[] EdgeMag;   // 0 or 255 (Canny binary)
            public bool HasEdge;
        }

        // -------- Cache build --------
        public static SourceCache BuildCache(Texture2D src, bool computeEdge, float gaussianSigma, float lowThresh, float highThresh)
        {
            var cache = new SourceCache
            {
                W = src.width,
                H = src.height,
                Pixels = src.GetPixels32()
            };
            int n = cache.W * cache.H;
            cache.LabL = new float[n];
            cache.LabA = new float[n];
            cache.LabB = new float[n];
            for (int i = 0; i < n; i++)
            {
                var p = cache.Pixels[i];
                RgbToLab(p.r, p.g, p.b, out cache.LabL[i], out cache.LabA[i], out cache.LabB[i]);
            }
            if (computeEdge)
            {
                cache.EdgeMag = Canny(cache.Pixels, cache.W, cache.H, gaussianSigma, lowThresh, highThresh);
                cache.HasEdge = true;
            }
            return cache;
        }

        public static void RecomputeEdge(SourceCache cache, float gaussianSigma, float lowThresh, float highThresh)
        {
            if (cache == null || cache.Pixels == null) return;
            cache.EdgeMag = Canny(cache.Pixels, cache.W, cache.H, gaussianSigma, lowThresh, highThresh);
            cache.HasEdge = true;
        }

        // -------- RGB → LAB (D65) --------
        static void RgbToLab(byte r, byte g, byte b, out float L, out float A, out float B)
        {
            float lr = SrgbToLinear(r / 255f);
            float lg = SrgbToLinear(g / 255f);
            float lb = SrgbToLinear(b / 255f);

            float X = lr * 0.4124564f + lg * 0.3575761f + lb * 0.1804375f;
            float Y = lr * 0.2126729f + lg * 0.7151522f + lb * 0.0721750f;
            float Z = lr * 0.0193339f + lg * 0.1191920f + lb * 0.9503041f;

            const float Xn = 0.95047f, Yn = 1.0f, Zn = 1.08883f;
            float fx = LabF(X / Xn);
            float fy = LabF(Y / Yn);
            float fz = LabF(Z / Zn);

            L = 116f * fy - 16f;
            A = 500f * (fx - fy);
            B = 200f * (fy - fz);
        }

        static float SrgbToLinear(float c)
        {
            return c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
        }

        static float LabF(float t)
        {
            const float delta3 = (6f / 29f) * (6f / 29f) * (6f / 29f);
            const float invK = 1f / (3f * (6f / 29f) * (6f / 29f));
            return t > delta3 ? Mathf.Pow(t, 1f / 3f) : t * invK + 4f / 29f;
        }

        static float LabDistanceSq(float L1, float A1, float B1, float L2, float A2, float B2)
        {
            float dL = L1 - L2, dA = A1 - A2, dB = B1 - B2;
            return dL * dL + dA * dA + dB * dB;
        }

        // -------- Canny (Gaussian → Sobel → NMS → Double Threshold → Hysteresis) --------
        public static byte[] Canny(Color32[] src, int W, int H, float sigma, float lowThresh, float highThresh)
        {
            int n = W * H;

            // 1) Grayscale (luminance)
            float[] gray = new float[n];
            for (int i = 0; i < n; i++)
            {
                var p = src[i];
                gray[i] = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
            }

            // 2) Gaussian blur (separable)
            float[] blurred = GaussianBlur(gray, W, H, Mathf.Max(0.1f, sigma));

            // 3) Sobel gradient + direction quantization
            float[] mag = new float[n];
            byte[] dir = new byte[n]; // 0:0°, 1:45°, 2:90°, 3:135°
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    int idx = y * W + x;
                    float a = blurred[(y - 1) * W + (x - 1)];
                    float b = blurred[(y - 1) * W + x];
                    float c = blurred[(y - 1) * W + (x + 1)];
                    float d = blurred[y * W + (x - 1)];
                    float f = blurred[y * W + (x + 1)];
                    float g = blurred[(y + 1) * W + (x - 1)];
                    float h = blurred[(y + 1) * W + x];
                    float i2 = blurred[(y + 1) * W + (x + 1)];

                    float gx = -a - 2f * d - g + c + 2f * f + i2;
                    float gy = -a - 2f * b - c + g + 2f * h + i2;
                    mag[idx] = Mathf.Sqrt(gx * gx + gy * gy);

                    float angle = Mathf.Atan2(gy, gx) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 180f;
                    if (angle < 22.5f || angle >= 157.5f) dir[idx] = 0;
                    else if (angle < 67.5f) dir[idx] = 1;
                    else if (angle < 112.5f) dir[idx] = 2;
                    else dir[idx] = 3;
                }
            }

            // 4) Non-maximum suppression
            float[] nms = new float[n];
            for (int y = 1; y < H - 1; y++)
            {
                for (int x = 1; x < W - 1; x++)
                {
                    int idx = y * W + x;
                    float m = mag[idx];
                    float n1, n2;
                    switch (dir[idx])
                    {
                        case 0: n1 = mag[idx - 1]; n2 = mag[idx + 1]; break;
                        case 1: n1 = mag[idx - W + 1]; n2 = mag[idx + W - 1]; break;
                        case 2: n1 = mag[idx - W]; n2 = mag[idx + W]; break;
                        default: n1 = mag[idx - W - 1]; n2 = mag[idx + W + 1]; break;
                    }
                    nms[idx] = (m >= n1 && m >= n2) ? m : 0f;
                }
            }

            // 5) Double threshold + Hysteresis (BFS from strong edges)
            byte[] result = new byte[n];
            var queue = new Queue<int>();
            for (int i = 0; i < n; i++)
            {
                if (nms[i] >= highThresh) { result[i] = 255; queue.Enqueue(i); }
                else if (nms[i] >= lowThresh) result[i] = 128; // weak (candidate)
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % W, y = idx / W;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= H) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= W || (dx == 0 && dy == 0)) continue;
                        int nidx = ny * W + nx;
                        if (result[nidx] == 128)
                        {
                            result[nidx] = 255;
                            queue.Enqueue(nidx);
                        }
                    }
                }
            }
            for (int i = 0; i < n; i++)
                if (result[i] == 128) result[i] = 0;

            return result;
        }

        static float[] GaussianBlur(float[] src, int W, int H, float sigma)
        {
            int radius = Mathf.Max(1, Mathf.CeilToInt(sigma * 3f));
            int size = radius * 2 + 1;
            float[] kernel = new float[size];
            float sum = 0f;
            float twoSigma2 = 2f * sigma * sigma;
            for (int i = 0; i < size; i++)
            {
                float x = i - radius;
                kernel[i] = Mathf.Exp(-x * x / twoSigma2);
                sum += kernel[i];
            }
            float invSum = 1f / sum;
            for (int i = 0; i < size; i++) kernel[i] *= invSum;

            float[] tmp = new float[W * H];
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    float acc = 0f;
                    for (int k = 0; k < size; k++)
                    {
                        int sx = Mathf.Clamp(x + k - radius, 0, W - 1);
                        acc += src[row + sx] * kernel[k];
                    }
                    tmp[row + x] = acc;
                }
            }
            float[] dst = new float[W * H];
            for (int y = 0; y < H; y++)
            {
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    float acc = 0f;
                    for (int k = 0; k < size; k++)
                    {
                        int sy = Mathf.Clamp(y + k - radius, 0, H - 1);
                        acc += tmp[sy * W + x] * kernel[k];
                    }
                    dst[row + x] = acc;
                }
            }
            return dst;
        }

        // -------- Tolerance Region Growing (다중 시드 Union, 8-연결, Edge-aware AND) --------
        // 반환 mask는 W*H. 시드 색과의 LAB 거리가 colorTol 이내이고,
        // useEdgeAware && cache.HasEdge면 EdgeMag != 0 픽셀에서만 확장.
        // 어떤 시드 색이든 임계 이내이면 OK (Union).
        public static byte[] RegionGrow(SourceCache cache, IList<Vector2Int> seeds, float colorTol, bool useEdgeAware, out RectInt dirty)
        {
            int W = cache.W, H = cache.H;
            byte[] mask = new byte[W * H];
            dirty = new RectInt(0, 0, 0, 0);
            if (seeds == null || seeds.Count == 0) return mask;

            var seedColors = new List<Vector3>(seeds.Count);
            var queue = new Queue<int>();
            int minX = W, minY = H, maxX = -1, maxY = -1;

            for (int s = 0; s < seeds.Count; s++)
            {
                int sx = seeds[s].x, sy = seeds[s].y;
                if (sx < 0 || sx >= W || sy < 0 || sy >= H) continue;
                int sidx = sy * W + sx;
                seedColors.Add(new Vector3(cache.LabL[sidx], cache.LabA[sidx], cache.LabB[sidx]));
                if (mask[sidx] == 0)
                {
                    mask[sidx] = 255;
                    queue.Enqueue(sidx);
                    if (sx < minX) minX = sx;
                    if (sx > maxX) maxX = sx;
                    if (sy < minY) minY = sy;
                    if (sy > maxY) maxY = sy;
                }
            }
            if (seedColors.Count == 0) return mask;

            float tolSq = colorTol * colorTol;
            bool edgeOn = useEdgeAware && cache.HasEdge && cache.EdgeMag != null;

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % W, y = idx / W;
                for (int dy = -1; dy <= 1; dy++)
                {
                    int ny = y + dy;
                    if (ny < 0 || ny >= H) continue;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int nx = x + dx;
                        if (nx < 0 || nx >= W || (dx == 0 && dy == 0)) continue;
                        int nidx = ny * W + nx;
                        if (mask[nidx] != 0) continue;
                        if (edgeOn && cache.EdgeMag[nidx] == 0) continue;

                        float nL = cache.LabL[nidx];
                        float nA = cache.LabA[nidx];
                        float nB = cache.LabB[nidx];
                        bool ok = false;
                        for (int s = 0; s < seedColors.Count; s++)
                        {
                            Vector3 sc = seedColors[s];
                            float distSq = LabDistanceSq(nL, nA, nB, sc.x, sc.y, sc.z);
                            if (distSq <= tolSq) { ok = true; break; }
                        }
                        if (!ok) continue;

                        mask[nidx] = 255;
                        queue.Enqueue(nidx);
                        if (nx < minX) minX = nx;
                        if (nx > maxX) maxX = nx;
                        if (ny < minY) minY = ny;
                        if (ny > maxY) maxY = ny;
                    }
                }
            }

            if (maxX < 0) dirty = new RectInt(0, 0, 0, 0);
            else dirty = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            return mask;
        }

        // -------- Fill Holes (외곽선 마스크의 닫힌 내부 자동 채움) --------
        // 가장자리에서 mask=0 픽셀을 4-연결 flood fill로 외부 라벨링 →
        // 라벨링되지 않은 mask=0 픽셀이 외곽선 안쪽 → mask=255로 채움.
        public static void FillHoles(byte[] mask, int W, int H)
        {
            int n = W * H;
            bool[] visited = new bool[n];
            var queue = new Queue<int>();

            for (int x = 0; x < W; x++)
            {
                int top = x;
                int bot = (H - 1) * W + x;
                if (mask[top] == 0 && !visited[top]) { visited[top] = true; queue.Enqueue(top); }
                if (mask[bot] == 0 && !visited[bot]) { visited[bot] = true; queue.Enqueue(bot); }
            }
            for (int y = 0; y < H; y++)
            {
                int left = y * W;
                int right = y * W + W - 1;
                if (mask[left] == 0 && !visited[left]) { visited[left] = true; queue.Enqueue(left); }
                if (mask[right] == 0 && !visited[right]) { visited[right] = true; queue.Enqueue(right); }
            }

            while (queue.Count > 0)
            {
                int idx = queue.Dequeue();
                int x = idx % W, y = idx / W;
                // 4-연결만 사용 (대각 누수 방지)
                if (x > 0)     { int ni = idx - 1; if (!visited[ni] && mask[ni] == 0) { visited[ni] = true; queue.Enqueue(ni); } }
                if (x < W - 1) { int ni = idx + 1; if (!visited[ni] && mask[ni] == 0) { visited[ni] = true; queue.Enqueue(ni); } }
                if (y > 0)     { int ni = idx - W; if (!visited[ni] && mask[ni] == 0) { visited[ni] = true; queue.Enqueue(ni); } }
                if (y < H - 1) { int ni = idx + W; if (!visited[ni] && mask[ni] == 0) { visited[ni] = true; queue.Enqueue(ni); } }
            }

            for (int i = 0; i < n; i++)
                if (mask[i] == 0 && !visited[i]) mask[i] = 255;
        }

        // -------- 마스크 병합 (Add / Subtract) --------
        // candidate가 1인 픽셀을 dst에 적용. add=true면 255 세팅, false면 0 세팅.
        // 반환: 실제 변경된 영역 bbox.
        public static RectInt ApplyToMask(byte[] dst, byte[] candidate, int W, int H, bool add)
        {
            int minX = W, minY = H, maxX = -1, maxY = -1;
            byte v = add ? (byte)255 : (byte)0;
            int n = W * H;
            for (int i = 0; i < n; i++)
            {
                if (candidate[i] == 0) continue;
                if (dst[i] != v)
                {
                    dst[i] = v;
                    int x = i % W, y = i / W;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            if (maxX < 0) return new RectInt(0, 0, 0, 0);
            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }
    }
}
