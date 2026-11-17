using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DefenseBattle.View.Tests
{
    /// <summary>Play 게임즈 로그인·계정 연결: 기기를 바꿔도 같은 플레이어로 들어오게 한다</summary>
    public class AccountLinkTests
    {
        sealed class FakeAuth : IAuthOps
        {
            public readonly List<string> Calls = new List<string>();
            public bool SignedIn;
            public bool HasToken;
            public bool Linked;
            public bool LinkedElsewhere;
            public bool GpgsSignInFails;

            public bool IsSignedIn => SignedIn;
            public bool SessionTokenExists => HasToken;

            public Task SignInAnonymously()
            {
                Calls.Add("anon");
                SignedIn = true;
                return Task.CompletedTask;
            }

            public Task SignInWithGpgs(string code)
            {
                Calls.Add("gpgs:" + code);
                if (GpgsSignInFails) throw new Exception("gpgs down");
                SignedIn = true;
                Linked = true;
                return Task.CompletedTask;
            }

            public Task<bool> HasGpgs()
            {
                Calls.Add("has");
                return Task.FromResult(Linked);
            }

            public Task<bool> LinkGpgs(string code)
            {
                Calls.Add("link:" + code);
                if (LinkedElsewhere) return Task.FromResult(false);
                Linked = true;
                return Task.FromResult(true);
            }

            public void SignOut()
            {
                Calls.Add("signout");
                SignedIn = false; // 세션 토큰은 남긴다 - 바꾸기에 실패하면 원래 익명 계정으로 돌아간다
            }
        }

        int m_resets;
        int m_codes;

        Func<Task<string>> Codes(bool available) => () =>
        {
            m_codes++;
            return Task.FromResult(available ? "code" + m_codes : null);
        };

        IEnumerator Run(FakeAuth auth, bool codeAvailable)
        {
            m_resets = 0;
            m_codes = 0;
            Task t = new AccountLink(auth, () => m_resets++).Run(Codes(codeAvailable));
            while (!t.IsCompleted) yield return null;
            if (t.IsFaulted) throw t.Exception;
        }

        [UnityTest]
        public IEnumerator NoCode_SignsInAnonymously()
        {
            var auth = new FakeAuth();
            yield return Run(auth, false);
            CollectionAssert.AreEqual(new[] { "anon" }, auth.Calls);
            Assert.IsTrue(auth.SignedIn);
        }

        [UnityTest]
        public IEnumerator NoSession_WithCode_SignsInWithGpgs()
        {
            var auth = new FakeAuth();
            yield return Run(auth, true);
            CollectionAssert.AreEqual(new[] { "gpgs:code1" }, auth.Calls);
            Assert.AreEqual(0, m_resets);
        }

        [UnityTest]
        public IEnumerator AnonymousSession_LinksGpgs()
        {
            var auth = new FakeAuth { HasToken = true };
            yield return Run(auth, true);
            CollectionAssert.AreEqual(new[] { "anon", "has", "link:code1" }, auth.Calls);
            Assert.IsTrue(auth.Linked);
            Assert.AreEqual(0, m_resets);
        }

        [UnityTest]
        public IEnumerator AlreadyLinkedElsewhere_SwitchesToGpgsAccount()
        {
            var auth = new FakeAuth { HasToken = true, LinkedElsewhere = true };
            yield return Run(auth, true);
            // 연결 시도에 쓴 코드는 한 번용 - 새 코드로 그 Play 게임즈 계정에 들어간다
            CollectionAssert.AreEqual(new[] { "anon", "has", "link:code1", "signout", "gpgs:code2" }, auth.Calls);
            Assert.IsTrue(auth.SignedIn);
            Assert.AreEqual(1, m_resets);
        }

        [UnityTest]
        public IEnumerator AlreadyLinked_DoesNothing()
        {
            var auth = new FakeAuth { HasToken = true, Linked = true };
            yield return Run(auth, true);
            CollectionAssert.AreEqual(new[] { "anon", "has" }, auth.Calls);
            Assert.AreEqual(0, m_resets);
        }

        [UnityTest]
        public IEnumerator GpgsFails_FallsBackToAnonymous()
        {
            var auth = new FakeAuth { GpgsSignInFails = true };
            yield return Run(auth, true);
            CollectionAssert.AreEqual(new[] { "gpgs:code1", "anon" }, auth.Calls);
            Assert.IsTrue(auth.SignedIn);
        }

        [UnityTest]
        public IEnumerator SwitchFails_SignsBackInAnonymously()
        {
            var auth = new FakeAuth { HasToken = true, LinkedElsewhere = true, GpgsSignInFails = true };
            yield return Run(auth, true);
            // 바꾸던 중 실패해도 로그인은 된 상태로 끝난다 (남은 세션 토큰으로 원래 익명 계정)
            Assert.IsTrue(auth.SignedIn);
            Assert.AreEqual("anon", auth.Calls[auth.Calls.Count - 1]);
        }

        [UnityTest]
        public IEnumerator CodeProviderThrows_SignsInAnonymously()
        {
            var auth = new FakeAuth();
            Task t = new AccountLink(auth, () => { }).Run(() => throw new Exception("no play services"));
            while (!t.IsCompleted) yield return null;
            Assert.IsFalse(t.IsFaulted);
            CollectionAssert.AreEqual(new[] { "anon" }, auth.Calls);
        }
    }
}
