#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using DefenseBattle.View;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Android AAB 빌드. 비밀값은 저장소에 두지 않는다:
/// - 비밀번호는 환경변수에서만: DB_KEYSTORE_PASS (키스토어), DB_KEYALIAS_PASS (키 별칭, 없으면 키스토어와 같게)
/// - 나머지는 프로젝트 루트의 release.local.json (gitignore, 예시 docs/release.local.example.json) 또는 같은 이름의 환경변수:
///   keystorePath(DB_KEYSTORE_PATH), keyAlias(DB_KEYALIAS), admobAppId(DB_ADMOB_APP_ID), admobRewardedId(DB_ADMOB_REWARDED_ID)
/// 릴리스 빌드 동안만 AdMob 앱 ID 와 Resources/AdUnits.txt(보상형 광고 단위)를 넣고, 끝나면(실패해도) 되돌린다.
/// 출력: Builds/defensebattle-v{versionCode}.aab (테스트 빌드는 Builds/defensebattle-test.aab - 디버그 키·테스트 광고)
/// </summary>
public static class ReleaseBuilder
{
    const string LocalConfig = "release.local.json";
    const string AdsSettingsPath = "Assets/GoogleMobileAds/Resources/GoogleMobileAdsSettings.asset";
    const string AdUnitsPath = "Assets/Resources/" + AdUnitConfig.ResourceName + ".txt";
    const string Scene = "Assets/Scenes/Battle.unity";

    [Serializable]
    sealed class LocalSettings
    {
        public string keystorePath;
        public string keyAlias;
        public string admobAppId;
        public string admobRewardedId;
    }

    /// <summary>프로세스 → 사용자 → 시스템 순으로 환경변수 (Unity Hub 가 오래 떠 있으면 프로세스 환경에 새 변수가 없다)</summary>
    static string Env(string name)
    {
        string v = Environment.GetEnvironmentVariable(name)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User)
            ?? Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    static LocalSettings Load()
    {
        var s = File.Exists(LocalConfig) ? JsonUtility.FromJson<LocalSettings>(File.ReadAllText(LocalConfig)) : new LocalSettings();
        s.keystorePath = Env("DB_KEYSTORE_PATH") ?? s.keystorePath;
        s.keyAlias = Env("DB_KEYALIAS") ?? s.keyAlias;
        s.admobAppId = Env("DB_ADMOB_APP_ID") ?? s.admobAppId;
        s.admobRewardedId = Env("DB_ADMOB_REWARDED_ID") ?? s.admobRewardedId;
        return s;
    }

    /// <summary>릴리스 빌드에 빠진 값 (비어 있으면 빌드 가능)</summary>
    public static List<string> Missing(string keystorePath, string keyAlias, string keystorePass, string appId, string rewardedId)
    {
        var miss = new List<string>();
        if (string.IsNullOrEmpty(keystorePath) || !File.Exists(keystorePath)) miss.Add("keystorePath (파일이 있어야 함)");
        if (string.IsNullOrEmpty(keyAlias)) miss.Add("keyAlias");
        if (string.IsNullOrEmpty(keystorePass)) miss.Add("환경변수 DB_KEYSTORE_PASS");
        if (!AdUnitConfig.IsAppId(appId)) miss.Add("admobAppId (ca-app-pub-…~…)");
        if (!AdUnitConfig.IsUnitId(rewardedId)) miss.Add("admobRewardedId (ca-app-pub-…/…)");
        if (appId == AdUnitConfig.TestAppIdAndroid || rewardedId == AdUnitConfig.TestRewardedAndroid) miss.Add("실제 AdMob ID (지금 값은 Google 테스트 ID)");
        return miss;
    }

    [MenuItem("Tools/Defense Battle/Build Release AAB")]
    public static void BuildRelease()
    {
        LocalSettings s = Load();
        string pass = Env("DB_KEYSTORE_PASS");
        string aliasPass = Env("DB_KEYALIAS_PASS") ?? pass;
        List<string> miss = Missing(s.keystorePath, s.keyAlias, pass, s.admobAppId, s.admobRewardedId);
        if (miss.Count > 0)
            throw new InvalidOperationException("릴리스 빌드에 필요한 값이 없습니다 (" + LocalConfig + " / 환경변수, docs/ugs-setup.md 출시 순서): " + string.Join(", ", miss));
        Build($"Builds/defensebattle-v{PlayerSettings.Android.bundleVersionCode}.aab", s.keystorePath, s.keyAlias, pass, aliasPass, s.admobAppId, s.admobRewardedId);
    }

    /// <summary>비밀값 없이 빌드가 되는지 확인 (디버그 키·테스트 광고 - 올릴 수 없는 빌드)</summary>
    [MenuItem("Tools/Defense Battle/Build Test AAB")]
    public static void BuildTest() => Build("Builds/defensebattle-test.aab", null, null, null, null, null, null);

    static void Build(string output, string keystore, string alias, string pass, string aliasPass, string appId, string rewardedId)
    {
        bool release = keystore != null;
        var ads = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AdsSettingsPath);
        var adsSo = ads != null ? new SerializedObject(ads) : null;
        SerializedProperty appIdProp = adsSo?.FindProperty("adMobAndroidAppId");
        string savedAppId = appIdProp?.stringValue;
        bool savedCustom = PlayerSettings.Android.useCustomKeystore;
        string savedKeystore = PlayerSettings.Android.keystoreName;
        string savedAlias = PlayerSettings.Android.keyaliasName;
        bool savedAab = EditorUserBuildSettings.buildAppBundle;
        try
        {
            if (release)
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keyaliasName = alias;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasPass = aliasPass;
                if (appIdProp != null)
                {
                    appIdProp.stringValue = appId;
                    adsSo.ApplyModifiedPropertiesWithoutUndo();
                }
                Directory.CreateDirectory("Assets/Resources");
                File.WriteAllText(AdUnitsPath, AdUnitConfig.Format(rewardedId));
                AssetDatabase.ImportAsset(AdUnitsPath);
            }
            else PlayerSettings.Android.useCustomKeystore = false;

            EditorUserBuildSettings.buildAppBundle = true;
            EditorUserBuildSettings.development = false;
            Directory.CreateDirectory("Builds");
            BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { Scene },
                locationPathName = output,
                target = BuildTarget.Android,
                options = BuildOptions.None,
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception($"빌드 실패: {report.summary.result}, 에러 {report.summary.totalErrors}건");
            Debug.Log($"[ReleaseBuilder] 빌드 성공 - {output} ({report.summary.totalSize / (1024 * 1024)}MB, {(release ? "릴리스" : "테스트: 디버그 키·테스트 광고")})");
        }
        finally
        {
            // 넣었던 실제 값을 저장소 파일에서 지운다
            PlayerSettings.Android.keystorePass = "";
            PlayerSettings.Android.keyaliasPass = "";
            PlayerSettings.Android.useCustomKeystore = savedCustom;
            PlayerSettings.Android.keystoreName = savedKeystore;
            PlayerSettings.Android.keyaliasName = savedAlias;
            EditorUserBuildSettings.buildAppBundle = savedAab;
            if (appIdProp != null && appIdProp.stringValue != savedAppId)
            {
                appIdProp.stringValue = savedAppId;
                adsSo.ApplyModifiedPropertiesWithoutUndo();
            }
            if (File.Exists(AdUnitsPath)) AssetDatabase.DeleteAsset(AdUnitsPath);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
