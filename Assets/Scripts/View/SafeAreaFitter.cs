using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>이 RectTransform 을 화면 안전 영역(노치·홈 막대 제외)에 맞춘다. 바뀔 때만 다시 계산</summary>
    [ExecuteAlways]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        Rect m_lastSafe;
        Vector2Int m_lastScreen;

        void OnEnable() => Apply();

        void Update()
        {
            if (m_lastSafe != Screen.safeArea || m_lastScreen.x != Screen.width || m_lastScreen.y != Screen.height) Apply();
        }

        void Apply()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect safe = Screen.safeArea;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            m_lastSafe = safe;
            m_lastScreen = new Vector2Int(Screen.width, Screen.height);
        }
    }
}
