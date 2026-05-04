using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal struct PenVertex
    {
        public Vector2 Anchor;
        public Vector2 HandleIn;   // 이전 세그먼트 쪽 핸들 (Anchor 기준 상대좌표 아님, 절대좌표)
        public Vector2 HandleOut;  // 다음 세그먼트 쪽 핸들
        public bool IsCorner;      // true면 핸들 없음 (직선 코너)

        public static PenVertex Corner(Vector2 pos)
        {
            return new PenVertex { Anchor = pos, HandleIn = pos, HandleOut = pos, IsCorner = true };
        }

        public static PenVertex Smooth(Vector2 pos, Vector2 handleOut)
        {
            // 대칭 핸들: handleIn은 anchor를 기준으로 handleOut의 반대
            var handleIn = pos + (pos - handleOut);
            return new PenVertex { Anchor = pos, HandleIn = handleIn, HandleOut = handleOut, IsCorner = false };
        }

        public bool HasHandleIn => !IsCorner && Vector2.Distance(HandleIn, Anchor) > 0.01f;
        public bool HasHandleOut => !IsCorner && Vector2.Distance(HandleOut, Anchor) > 0.01f;

        // 핸들 제거 → 코너로 변환
        public void MakeCorner()
        {
            HandleIn = Anchor;
            HandleOut = Anchor;
            IsCorner = true;
        }

        // handleOut 이동 시 대칭 모드: handleIn도 반대로 따라감
        public void SetHandleOutSymmetric(Vector2 hOut)
        {
            HandleOut = hOut;
            HandleIn = Anchor + (Anchor - hOut);
        }

        // handleIn 이동 시 대칭 모드
        public void SetHandleInSymmetric(Vector2 hIn)
        {
            HandleIn = hIn;
            HandleOut = Anchor + (Anchor - hIn);
        }
    }
}
