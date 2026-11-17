using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace DefenseBattle.View.Tests
{
    /// <summary>계정 삭제 (Google Play 계정 삭제 정책): 서버 데이터 → 로그인 계정 → 기기 기록 순서, 실패하면 거기서 멈춘다</summary>
    public class AccountDeletionTests
    {
        readonly List<string> m_calls = new List<string>();
        bool m_serverFails, m_authFails;

        AccountDeletion New() => new AccountDeletion(
            () => { m_calls.Add("server"); return m_serverFails ? Task.FromException(new Exception("offline")) : Task.CompletedTask; },
            () => { m_calls.Add("auth"); return m_authFails ? Task.FromException(new Exception("auth")) : Task.CompletedTask; },
            () => m_calls.Add("local"));

        static IEnumerator Wait(Task t)
        {
            for (int i = 0; i < 20 && !t.IsCompleted; i++) yield return null;
            Assert.IsTrue(t.IsCompleted);
        }

        [SetUp]
        public void Reset()
        {
            m_calls.Clear();
            m_serverFails = m_authFails = false;
        }

        [UnityTest]
        public IEnumerator Run_DeletesServerThenAuthThenLocal()
        {
            Task<bool> t = New().Run();
            yield return Wait(t);
            Assert.IsTrue(t.Result);
            CollectionAssert.AreEqual(new[] { "server", "auth", "local" }, m_calls);
        }

        [UnityTest]
        public IEnumerator ServerFails_StopsBeforeAuth()
        {
            m_serverFails = true;
            Task<bool> t = New().Run();
            yield return Wait(t);
            Assert.IsFalse(t.Result);
            CollectionAssert.AreEqual(new[] { "server" }, m_calls, "서버 데이터를 못 지웠으면 로그인 계정도 남겨 다시 시도할 수 있게");
        }

        [UnityTest]
        public IEnumerator AuthFails_KeepsLocalForRetry()
        {
            m_authFails = true;
            Task<bool> t = New().Run();
            yield return Wait(t);
            Assert.IsFalse(t.Result);
            CollectionAssert.AreEqual(new[] { "server", "auth" }, m_calls);
        }
    }
}
