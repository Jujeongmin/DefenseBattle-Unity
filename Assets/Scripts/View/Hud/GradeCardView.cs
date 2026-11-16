using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>영웅 관리의 등급 카드 한 장 (씬 빌더가 연결)</summary>
    public sealed class GradeCardView : MonoBehaviour
    {
        public Button Button;
        public Image Border;
        public CanvasGroup Group;
        public TextMeshProUGUI Name;
        public Image Icon;
        public TextMeshProUGUI Count;
        public TextMeshProUGUI Bottom;
    }
}
