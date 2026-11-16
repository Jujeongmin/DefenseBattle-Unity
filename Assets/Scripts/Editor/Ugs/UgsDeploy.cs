using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Services.DeploymentApi.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// UGS 서버 설정 배포 (Deployment 창과 같은 동작): Cloud Code 모듈(.ccmr).
/// 대상 환경은 Project Settings → Services → Environments 에서 고른 것 (기본 production).
/// </summary>
public static class UgsDeploy
{
    const string Root = "Assets/CloudCode";

    [MenuItem("Tools/Defense Battle/Deploy Cloud Code")]
    public static async void DeployAll()
    {
        var paths = new List<string>();
        foreach (string p in Directory.GetFiles(Root, "*.*", SearchOption.AllDirectories))
        {
            string ext = Path.GetExtension(p);
            if (ext == ".ccmr")
                paths.Add(Path.GetFullPath(p));
        }

        IDeploymentWindow window = Deployments.Instance.DeploymentWindow;
        window.OpenWindow(); // 창이 열려 있어야 GetFromFiles·Deploy 가 동작한다 - 처음 열면 한 박자 기다린다
        await System.Threading.Tasks.Task.Delay(1000);
        List<IDeploymentItem> items = window.GetFromFiles(paths);
        items.RemoveAll(i => i == null);
        Debug.Log($"[UgsDeploy] 배포 시작 - 파일 {paths.Count}개, 항목 {items.Count}개");

        DeploymentResult<IDeploymentItem> result = await window.Deploy(items);

        // Deployed 목록에는 실패한 항목도 들어온다 - 항목 상태의 심각도로 판단한다
        var sb = new StringBuilder();
        int failed = 0;
        foreach (IDeploymentItem item in items)
        {
            bool ok = result.Deployed.Contains(item) && item.Status.MessageSeverity != SeverityLevel.Error;
            if (!ok) failed++;
            sb.AppendLine($"{(ok ? "OK  " : "FAIL")} {item.Name} - {item.Status.Message} {item.Status.MessageDetail}");
        }
        string summary = $"[UgsDeploy] 끝 - 성공 {items.Count - failed} / 실패 {failed}\n{sb}";
        if (failed > 0) Debug.LogError(summary);
        else Debug.Log(summary);
    }
}
