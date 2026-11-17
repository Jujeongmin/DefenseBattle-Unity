using System;
using System.Collections.Generic;
using DefenseBattle.Core;
using UnityEngine;

namespace DefenseBattle.View
{
    /// <summary>상품 결제 버튼 동작 (menu.ts buy · offers.ts popup buy): 결제 → 지급 확인 → 안내, 상자 상품이면 바로 공개</summary>
    public sealed class ProductBuy
    {
        readonly Toast m_toast;
        readonly BattleAudio m_audio;
        readonly RevealPopup m_reveal;
        bool m_busy;

        public ProductBuy(Toast toast, BattleAudio audio, RevealPopup reveal)
        {
            m_toast = toast;
            m_audio = audio;
            m_reveal = reveal;
        }

        /// <summary>done(true) = 지급됨. 결제 중에 또 누르면 무시 (두 번 결제 방지)</summary>
        public async void Buy(string productId, Action<bool> done)
        {
            ProductDef p = Products.Get(productId);
            if (p == null || m_busy) return;
            if (!Store.Ready) { m_toast.Show(Loc.Tl("준비 중")); return; }
            m_busy = true;
            List<Reveal> got = null;
            try { got = await Store.Service.Buy(productId); }
            catch (Exception ex) { Debug.LogWarning("[Shop] 결제 실패: " + ex.Message); }
            finally { m_busy = false; }
            if (got == null)
            {
                m_toast.Show(Loc.Tl("결제가 완료되지 않았어요"));
                done?.Invoke(false);
                return;
            }
            if (p.Category == "offer" && LocalMeta.Data.Offers.Remove(productId)) LocalMeta.Save(); // 산 한정 상품은 닫는다
            m_audio.Play(Sfx.Unlock); // audio.fanfareUnlock
            m_toast.Show(Loc.Tl("구매 완료: {0}", Loc.Tl(p.Kr)));
            done?.Invoke(true);
            foreach (Reveal r in got) m_reveal.Show(r);
        }
    }
}
