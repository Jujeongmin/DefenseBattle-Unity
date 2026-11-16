using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DefenseBattle.View.Tests
{
    /// <summary>씬 빌더 결과 (Battle.unity) - 전장 위 표시는 정사각 전장 안에 (style.css #battlefield aspect-ratio 1)</summary>
    public class BattleSceneTests
    {
        const string ScenePath = "Assets/Scenes/Battle.unity";

        [Test]
        public void Overlays_SitOnSquareBoard()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                Transform area = Find(scene, "Hud/SafeArea/BoardArea");
                Assert.IsNotNull(area);
                Transform board = area.Find("Board");
                Assert.IsNotNull(board, "BoardArea 안의 정사각 칸");
                var fitter = board.GetComponent<AspectRatioFitter>();
                Assert.IsNotNull(fitter);
                Assert.AreEqual(AspectRatioFitter.AspectMode.FitInParent, fitter.aspectMode);
                Assert.AreEqual(1f, fitter.aspectRatio);
                foreach (string name in new[] { "DragSurface", "GoldBadge", "Mythic", "Dynamic" })
                    Assert.IsNotNull(board.Find(name), name + " 는 정사각 전장 안에");

                var runner = Object.FindAnyObjectByType<BattleRunner>();
                var so = new SerializedObject(runner);
                Assert.AreEqual(board, so.FindProperty("m_boardArea").objectReferenceValue, "카메라도 정사각 칸에 맞춘다");
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }
        }

        static Transform Find(Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.name == rootName) return slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
            return null;
        }
    }
}
