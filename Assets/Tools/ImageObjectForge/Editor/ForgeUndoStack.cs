using System.Collections.Generic;
using UnityEngine;

namespace Orora.ImageObjectForge
{
    internal class ForgeUndoEntry
    {
        public RectInt Rect;
        public byte[] Before;
        public byte[] After;
    }

    internal class ForgeUndoStack
    {
        public const int MaxEntries = 30;

        readonly LinkedList<ForgeUndoEntry> _undo = new LinkedList<ForgeUndoEntry>();
        readonly Stack<ForgeUndoEntry> _redo = new Stack<ForgeUndoEntry>();

        public bool CanUndo => _undo.Count > 0;
        public bool CanRedo => _redo.Count > 0;
        public int UndoCount => _undo.Count;
        public int RedoCount => _redo.Count;

        public void Clear() { _undo.Clear(); _redo.Clear(); }

        // 호출자: 작업 직전에 전체 mask의 클론(preMask)을 보관, 작업 후 이 함수 호출.
        // 두 상태를 비교해 실제로 바뀐 bbox만 보존.
        public void Push(byte[] preMask, byte[] postMask, int imgW, int imgH)
        {
            if (preMask == null || postMask == null) return;
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            for (int y = 0; y < imgH; y++)
            {
                int row = y * imgW;
                for (int x = 0; x < imgW; x++)
                {
                    if (preMask[row + x] != postMask[row + x])
                    {
                        if (x < minX) minX = x;
                        if (x > maxX) maxX = x;
                        if (y < minY) minY = y;
                        if (y > maxY) maxY = y;
                    }
                }
            }
            if (minX > maxX) return; // 변경 없음
            var rect = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
            var bef = new byte[rect.width * rect.height];
            var aft = new byte[rect.width * rect.height];
            for (int dy = 0; dy < rect.height; dy++)
            {
                int srcRow = (rect.y + dy) * imgW + rect.x;
                int dstRow = dy * rect.width;
                System.Array.Copy(preMask, srcRow, bef, dstRow, rect.width);
                System.Array.Copy(postMask, srcRow, aft, dstRow, rect.width);
            }
            _undo.AddLast(new ForgeUndoEntry { Rect = rect, Before = bef, After = aft });
            while (_undo.Count > MaxEntries) _undo.RemoveFirst();
            _redo.Clear();
        }

        // 힌트가 주어지는 경우(폴리곤 bbox 등)의 더 효율적인 푸시.
        public void PushHinted(byte[] preMask, byte[] postMask, int imgW, RectInt hint)
        {
            if (hint.width <= 0 || hint.height <= 0) return;
            var rect = hint;
            var bef = new byte[rect.width * rect.height];
            var aft = new byte[rect.width * rect.height];
            bool anyDiff = false;
            for (int dy = 0; dy < rect.height; dy++)
            {
                int srcRow = (rect.y + dy) * imgW + rect.x;
                int dstRow = dy * rect.width;
                System.Array.Copy(preMask, srcRow, bef, dstRow, rect.width);
                System.Array.Copy(postMask, srcRow, aft, dstRow, rect.width);
                if (!anyDiff)
                {
                    for (int dx = 0; dx < rect.width; dx++)
                        if (bef[dstRow + dx] != aft[dstRow + dx]) { anyDiff = true; break; }
                }
            }
            if (!anyDiff) return;
            _undo.AddLast(new ForgeUndoEntry { Rect = rect, Before = bef, After = aft });
            while (_undo.Count > MaxEntries) _undo.RemoveFirst();
            _redo.Clear();
        }

        public RectInt Undo(byte[] mask, int imgW)
        {
            if (_undo.Count == 0) return new RectInt(0, 0, 0, 0);
            var e = _undo.Last.Value;
            _undo.RemoveLast();
            _redo.Push(e);
            ApplyRect(mask, imgW, e.Before, e.Rect);
            return e.Rect;
        }

        public RectInt Redo(byte[] mask, int imgW)
        {
            if (_redo.Count == 0) return new RectInt(0, 0, 0, 0);
            var e = _redo.Pop();
            _undo.AddLast(e);
            ApplyRect(mask, imgW, e.After, e.Rect);
            return e.Rect;
        }

        static void ApplyRect(byte[] mask, int imgW, byte[] patch, RectInt r)
        {
            for (int y = 0; y < r.height; y++)
            {
                int srcRow = y * r.width;
                int dstRow = (r.y + y) * imgW + r.x;
                System.Array.Copy(patch, srcRow, mask, dstRow, r.width);
            }
        }
    }
}
