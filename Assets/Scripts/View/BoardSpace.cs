using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>
    /// 전장 논리 좌표(720×720, 원점 좌상, y 아래로 - 원본 캔버스) ↔ 월드 (32px = 1 유닛, 전장 중심 = 원점, y 위로).
    /// </summary>
    public static class BoardSpace
    {
        /// <summary>논리 1px 의 월드 크기 (PPU 32)</summary>
        public const float Px = 1f / 32f;
        /// <summary>전장 한 변의 월드 크기 (22.5)</summary>
        public const float WorldSize = (float)Board.MapSize * Px;
        const float Half = WorldSize / 2f;

        public static Vector3 ToWorld(double x, double y) => new Vector3((float)x * Px - Half, Half - (float)y * Px, 0f);

        public static Vector2 ToLogical(Vector3 world) => new Vector2((world.x + Half) / Px, (Half - world.y) / Px);
    }
}
