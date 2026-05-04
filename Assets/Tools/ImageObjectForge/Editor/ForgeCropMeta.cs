using System;

namespace Orora.ImageObjectForge
{
    [Serializable]
    internal class ForgeCropMeta
    {
        public int version = 3;

        // v3 표준: 좌표계 기준 = 원본 배경 해상도 (캔버스 RefResolution과 매칭되어야 정확).
        public CanvasSize canvasSize;

        // v3 표준: 원본 배경 좌표계의 크롭 영역(픽셀, y-up).
        public CropBounds cropBounds;

        public string yConvention = "up";
        public string createdAt;
        public string toolVersion = "ImageObjectForge 0.3";

        // ---- v2 호환 폴백 필드 (역직렬화 시에만 사용) ----
        // v2 사이드카 로드 시 canvasSize가 비어 있으면 ForgeIO에서 sourceSize → canvasSize로 매핑.
        // v3 저장에서는 채우지 않음.
        public CanvasSize sourceSize;

        [Serializable]
        public struct CanvasSize
        {
            public int width;
            public int height;
        }

        [Serializable]
        public struct CropBounds
        {
            public int x;
            public int y;
            public int width;
            public int height;
        }
    }
}
