using System;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 구역 끌어 교환 (ui.ts setupDrag): 한 구역에서 눌러 다른 구역에서 떼면 SwapZones.
    /// 전장 밖에서 떼기·두 번째 손가락·판 종료·패널 열림이면 취소. 드래그 상태는 화면 쪽에만 있다 (엔진은 SwapZones 만).
    /// </summary>
    public sealed class ZoneDragLogic
    {
        public bool Active { get; private set; }
        public int From { get; private set; } = -1;
        /// <summary>교환 대상 구역 (없으면 -1)</summary>
        public int To { get; private set; } = -1;
        /// <summary>손가락 위치 (논리 px)</summary>
        public float X { get; private set; }
        public float Y { get; private set; }
        int m_pointer = -1;

        /// <summary>논리 좌표가 있는 구역: 0 위 / 1 오른쪽 / 2 아래 / 3 왼쪽, 정대각선·전장 밖이면 -1</summary>
        public static int ZoneAt(Vector2 p)
        {
            double center = Board.MapSize / 2;
            double dx = p.x - center, dy = p.y - center;
            if (Math.Abs(dx) > Board.MapSize / 2 || Math.Abs(dy) > Board.MapSize / 2) return -1;
            if (dy < 0 && Math.Abs(dx) < Math.Abs(dy)) return 0;
            if (dx > 0 && Math.Abs(dy) < Math.Abs(dx)) return 1;
            if (dy > 0 && Math.Abs(dx) < Math.Abs(dy)) return 2;
            if (dx < 0 && Math.Abs(dy) < Math.Abs(dx)) return 3;
            return -1;
        }

        public void Down(int pointerId, Vector2 p, Game g, bool blocked)
        {
            if (Active) { Cancel(); return; } // 두 번째 손가락: 취소
            if (blocked || g.Over) return;
            int z = ZoneAt(p);
            if (z < 0) return;
            Active = true;
            From = z;
            To = -1;
            X = p.x;
            Y = p.y;
            m_pointer = pointerId;
        }

        public void Move(int pointerId, Vector2 p)
        {
            if (!Active || pointerId != m_pointer) return;
            X = p.x;
            Y = p.y;
            int z = ZoneAt(p);
            To = z >= 0 && z != From ? z : -1;
        }

        /// <summary>손을 뗌 - 교환했으면 true</summary>
        public bool Up(int pointerId, Vector2 p, Game g, bool blocked)
        {
            if (!Active || pointerId != m_pointer) return false;
            int from = From, end = ZoneAt(p);
            bool swap = !blocked && !g.Over && end >= 0 && end != from;
            Cancel();
            if (swap) g.SwapZones(from, end);
            return swap;
        }

        public void Cancel()
        {
            Active = false;
            From = To = -1;
            m_pointer = -1;
        }
    }
}
