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

        /// <summary>
        /// 직교 카메라를 전장(정사각)이 화면의 칸(areaPx, 화면 px·아래가 0)에 꽉 맞게 놓는다 - 위·아래 UI 와 겹치지 않게.
        /// </summary>
        public static void FitOrtho(float screenW, float screenH, Rect areaPx, out float orthoSize, out Vector2 cameraPos)
        {
            float pxPerUnit = Mathf.Max(1f, Mathf.Min(areaPx.width, areaPx.height)) / WorldSize;
            orthoSize = screenH / (2f * pxPerUnit);
            // 전장 중심(월드 원점)이 칸 중심에 오도록 카메라를 반대로 민다
            cameraPos = new Vector2((screenW / 2f - areaPx.center.x) / pxPerUnit, (screenH / 2f - areaPx.center.y) / pxPerUnit);
        }

        /// <summary>FitOrtho 를 카메라에 적용 (area 는 Screen Space Overlay 캔버스의 칸)</summary>
        public static void FitCamera(Camera cam, RectTransform area)
        {
            area.GetWorldCorners(s_corners); // Overlay 캔버스: 월드 좌표 = 화면 px
            var rect = new Rect(s_corners[0].x, s_corners[0].y, s_corners[2].x - s_corners[0].x, s_corners[2].y - s_corners[0].y);
            FitOrtho(Screen.width, Screen.height, rect, out float ortho, out Vector2 pos);
            cam.orthographicSize = ortho;
            cam.transform.position = new Vector3(pos.x, pos.y, -10f);
        }

        static readonly Vector3[] s_corners = new Vector3[4];
    }
}
