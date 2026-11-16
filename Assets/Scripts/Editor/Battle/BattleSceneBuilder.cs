using DefenseBattle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Battle.unity 만들기 (Tools → Defense Battle → Build Battle Scene). 씬 YAML 을 손으로 고치지 않고 이 스크립트로 다시 만든다.
/// 원본 index.html #game-frame 순서: 상단바 / 전장 / 진행 바 / 하단 조작부 (기준 해상도 720×1280).
/// 캔버스 3개: HudStatic(프레임·버튼) / HudDynamic(바뀌는 글자) / Overlay(패널) - 같은 칸 배치를 둘 다 가진다.
/// </summary>
public static class BattleSceneBuilder
{
    public const string ScenePath = "Assets/Scenes/Battle.unity";

    /// <summary>기준 해상도 단위 칸 높이 (720×1280)</summary>
    public const float TopBarH = 92, ProgressH = 32, ControlsH = 300;

    [MenuItem("Tools/Defense Battle/Build Battle Scene")]
    public static void Build()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // 새 씬을 연 뒤에 불러온다 (씬 전환이 참조 없는 에셋을 내려 버려 먼저 부른 참조가 비게 된다)
        var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(SpriteCatalog.AssetPath);
        // 프로젝트는 Built-in 렌더 파이프라인으로 돈다 (URP 패키지는 있지만 지정돼 있지 않음) → 기본 스프라이트 머티리얼
        var unlit = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");

        // 카메라
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 20;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color32(0x1d, 0x1b, 0x2e, 255);
        cam.transform.position = new Vector3(0, 0, -10);
        // 같은 정렬 순서 안에서는 아래쪽(앞)에 있는 스프라이트가 위에 그려지게
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0, 1, 0);
        camGo.AddComponent<AudioListener>();

        // 전장
        var board = new GameObject("Board");
        var bg = new GameObject("Background").AddComponent<SpriteRenderer>();
        bg.transform.SetParent(board.transform, false);
        bg.sharedMaterial = unlit;
        bg.sortingOrder = 0;
        bg.sprite = catalog != null ? catalog.Field : null;
        var boardView = board.AddComponent<BoardView>();
        Set(boardView, "m_background", bg);
        ShapeBatch under = Batch(board.transform, "Under", unlit, 1);
        ShapeBatch mobOver = Batch(board.transform, "MobOver", unlit, 11);
        ShapeBatch over = Batch(board.transform, "Over", unlit, 30);
        ShapeBatch top = Batch(board.transform, "Top", unlit, 40);
        var actorsGo = new GameObject("Actors");
        actorsGo.transform.SetParent(board.transform, false);
        var actors = actorsGo.AddComponent<ActorViews>();
        Material flash = FlashMaterial();

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        RectTransform staticSafe = Canvas("HudStatic", 0, true);
        Canvas("HudDynamic", 1, false);
        Canvas("Overlay", 10, true);
        RectTransform boardArea = (RectTransform)staticSafe.Find("BoardArea");

        // 진행 루프
        var runner = new GameObject("Runner").AddComponent<BattleRunner>();
        Set(runner, "m_catalog", catalog);
        Set(runner, "m_camera", cam);
        Set(runner, "m_boardArea", boardArea);
        Set(runner, "m_board", boardView);
        Set(runner, "m_under", under);
        Set(runner, "m_mobOver", mobOver);
        Set(runner, "m_over", over);
        Set(runner, "m_top", top);
        Set(runner, "m_spriteMaterial", unlit);
        Set(runner, "m_flashMaterial", flash);
        Set(runner, "m_actors", actors);

        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
        AddToBuildSettingsFirst(ScenePath);
        Debug.Log("[BattleScene] " + ScenePath + " 저장");
    }

    /// <summary>피격 번쩍임: 같은 프레임을 가산(SrcAlpha One)으로 겹친다 (render.ts globalCompositeOperation 'lighter')</summary>
    static Material FlashMaterial()
    {
        const string path = "Assets/Art/DefenseBattle/Generated/FlashAdditive.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        mat = new Material(Shader.Find("Legacy Shaders/Particles/Additive")) { name = "FlashAdditive" };
        mat.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f)); // 기본값: 정점 색 × 텍스처 그대로
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static ShapeBatch Batch(Transform parent, string name, Material mat, int order)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        var b = go.AddComponent<ShapeBatch>();
        b.Configure(mat, "Default", order);
        return b;
    }

    /// <summary>캔버스 + 안전 영역 + 칸 배치 (상단바·전장·진행 바·하단 조작부). 안전 영역 RectTransform 을 돌려준다</summary>
    static RectTransform Canvas(string name, int order, bool raycast)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.matchWidthOrHeight = 0.5f;
        if (raycast) go.AddComponent<GraphicRaycaster>();

        RectTransform safe = Rt(go.transform, "SafeArea");
        Stretch(safe, 0, 0);
        safe.gameObject.AddComponent<SafeAreaFitter>();

        RectTransform topBar = Rt(safe, "TopBar");
        Edge(topBar, true, TopBarH, 0);
        RectTransform area = Rt(safe, "BoardArea");
        Stretch(area, TopBarH, ProgressH + ControlsH);
        RectTransform progress = Rt(safe, "ProgressRow");
        Edge(progress, false, ProgressH, ControlsH);
        RectTransform controls = Rt(safe, "Controls");
        Edge(controls, false, ControlsH, 0);
        return safe;
    }

    public static RectTransform Rt(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    /// <summary>부모를 채우되 위 top, 아래 bottom 만큼 비운다</summary>
    public static void Stretch(RectTransform rt, float top, float bottom)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(0, bottom);
        rt.offsetMax = new Vector2(0, -top);
    }

    /// <summary>부모의 위(또는 아래) 가장자리에 붙는 가로 띠 (높이 h, 가장자리에서 offset 만큼 띄움)</summary>
    public static void Edge(RectTransform rt, bool atTop, float h, float offset)
    {
        rt.anchorMin = new Vector2(0, atTop ? 1 : 0);
        rt.anchorMax = new Vector2(1, atTop ? 1 : 0);
        rt.pivot = new Vector2(0.5f, atTop ? 1 : 0);
        rt.sizeDelta = new Vector2(0, h);
        rt.anchoredPosition = new Vector2(0, atTop ? -offset : offset);
    }

    public static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) { Debug.LogError("[BattleScene] 필드 없음: " + target.GetType().Name + "." + field); return; }
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void AddToBuildSettingsFirst(string path)
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == path);
        list.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = list.ToArray();
    }
}
