using System.Collections.Generic;
using DefenseBattle.Core;

namespace DefenseBattle.View
{
    /// <summary>
    /// 영웅 관리 상태 (ui.ts openHeroes): 직업 탭, 합성/판매 모드, 판매 수량 1·10·ALL, 등급 카드 누르기.
    /// </summary>
    public sealed class HeroPanelLogic
    {
        /// <summary>판매 수량 ALL</summary>
        public const int All = int.MaxValue;

        public Job Job;
        public bool MergeMode;
        public int Qty = 1;
        /// <summary>탭에 보일 직업: 덱 + 지금 판에 있는 유닛 직업</summary>
        public readonly List<Job> Jobs = new List<Job>(8);

        /// <summary>job·mergeMode 가 null 이면 원본처럼: 합성할 게 있으면 그 직업·합성 탭, 없으면 첫 직업·판매 탭</summary>
        public void Open(Game g, Job? job, bool? mergeMode)
        {
            RefreshJobs(g);
            Job firstMergeable = default;
            bool anyMerge = FirstMergeable(g, ref firstMergeable);
            Job = job ?? (anyMerge ? firstMergeable : Jobs[0]);
            MergeMode = mergeMode ?? anyMerge;
            Qty = 1;
        }

        /// <summary>직업 목록 다시 (덱 + 유닛 직업, 순서 유지)</summary>
        public void RefreshJobs(Game g)
        {
            Jobs.Clear();
            foreach (Job j in g.Opts.Deck) if (!Jobs.Contains(j)) Jobs.Add(j);
            foreach (Unit u in g.Units) if (!Jobs.Contains(u.Job)) Jobs.Add(u.Job);
        }

        /// <summary>등급 카드를 누름. 알릴 문장(토스트)이 있으면 돌려준다</summary>
        public string TapGrade(Game g, int gradeIndex)
        {
            var gk = (GradeKey)gradeIndex;
            if (MergeMode)
                return g.Merge(Job, gk) == null ? Loc.Tl("같은 등급 {0}개가 필요해요", Grades.MergeCount) : null;
            g.SellMany(Job, gk, Qty);
            return null;
        }

        static bool FirstMergeable(Game g, ref Job job)
        {
            List<KeyValuePair<Job, GradeKey>> list = g.Mergeable(); // 패널을 열 때만 (프레임마다 아님)
            if (list.Count == 0) return false;
            job = list[0].Key;
            return true;
        }
    }
}
