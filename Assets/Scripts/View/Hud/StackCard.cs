using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DefenseBattle.View
{
    /// <summary>덱 카드 한 장의 화면 요소 (씬 빌더가 연결). 내용은 UnitStacksView 가 채운다</summary>
    public sealed class StackCard : MonoBehaviour
    {
        public Image Border;
        public CanvasGroup Group;
        public TextMeshProUGUI Count;
        public GameObject MergeTag;
        public Image Arrow;
        public Image Icon;
        public TextMeshProUGUI Name;
        public Button UpButton;
        public TextMeshProUGUI UpText;
        public Button CardButton;
    }
}
