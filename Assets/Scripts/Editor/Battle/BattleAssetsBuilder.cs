using System.Collections.Generic;
using System.IO;
using DefenseBattle.Core;
using DefenseBattle.View;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 전투 화면 에셋 만들기 (Tools → Defense Battle → Build Battle Assets). 다시 실행해도 같은 결과.
/// 1) SpriteCatalog: 잘린 유닛·몹 시트, 아이콘, 동전  2) 전장 배경 PNG (render.ts drawTiles + decorate 를 미리 굽기)
/// 3) Galmuri11 TMP 폰트 (Dynamic, 픽셀이라 Raster)  4) 아이콘 TMP Sprite Asset (Loc.Icon 의 &lt;sprite name&gt;) → TMP 기본 Sprite Asset
/// </summary>
public static class BattleAssetsBuilder
{
    const string Art = "Assets/Art/DefenseBattle/";
    const string Generated = Art + "Generated/";
    /// <summary>Tiles 폴더에 두면 임포터가 PPU 32·Point 로 맞춘다</summary>
    const string FieldPath = Art + "Tiles/field_bg.png";
    const string IconsPath = Generated + "Icons.asset";
    const string FontPath = "Assets/Fonts/DefenseBattle/Galmuri11 TMP.asset";
    const string BoldFontPath = "Assets/Fonts/DefenseBattle/Galmuri11-Bold TMP.asset";

    [MenuItem("Tools/Defense Battle/Build Battle Assets")]
    public static void BuildAll()
    {
        Directory.CreateDirectory(Generated);
        BakeField();
        TMP_FontAsset font = BuildFont("Assets/Fonts/DefenseBattle/Galmuri11.ttf", FontPath);
        TMP_FontAsset bold = BuildFont("Assets/Fonts/DefenseBattle/Galmuri11-Bold.ttf", BoldFontPath);
        BuildIconSpriteAsset();
        BuildCatalog(font, bold);
        AssetDatabase.SaveAssets();
        Debug.Log("[BattleAssets] 완료");
    }

    // ---------------- 카탈로그 ----------------
    static void BuildCatalog(TMP_FontAsset font, TMP_FontAsset bold)
    {
        var units = new SpriteCatalog.Sheet[Units.All.Length * Grades.Count];
        foreach (UnitDef u in Units.All)
            for (int g = 0; g < Grades.Count; g++)
                units[(int)u.Job * Grades.Count + g] = LoadSheet(u.Sheet + "_" + g);

        var mobs = new SpriteCatalog.RaceSheets[Races.All.Length];
        for (int r = 0; r < Races.All.Length; r++)
        {
            var list = new List<SpriteCatalog.Sheet>();
            for (int v = 0; File.Exists(Art + "Sprites/" + Races.Sprite[r] + "_" + v + ".png") || v < Races.Variants[r]; v++)
                list.Add(LoadSheet(Races.Sprite[r] + "_" + v));
            // 카드 전용 변형은 번호가 건너뛸 수 있다 - 그 뒤 번호까지 채운다
            for (int v = list.Count; v < 16; v++)
            {
                if (!File.Exists(Art + "Sprites/" + Races.Sprite[r] + "_" + v + ".png")) continue;
                while (list.Count < v) list.Add(new SpriteCatalog.Sheet());
                list.Add(LoadSheet(Races.Sprite[r] + "_" + v));
            }
            mobs[r] = new SpriteCatalog.RaceSheets { Variants = list.ToArray() };
        }

        var icons = new List<Sprite>();
        foreach (string p in Directory.GetFiles(Art + "UI/Icons", "*.png"))
            icons.Add(AssetDatabase.LoadAssetAtPath<Sprite>(p.Replace('\\', '/')));

        var catalog = AssetDatabase.LoadAssetAtPath<SpriteCatalog>(SpriteCatalog.AssetPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<SpriteCatalog>();
            AssetDatabase.CreateAsset(catalog, SpriteCatalog.AssetPath);
        }
        catalog.EditorFill(units, mobs,
            AssetDatabase.LoadAssetAtPath<Sprite>(FieldPath),
            AssetDatabase.LoadAssetAtPath<Sprite>(Art + "UI/Common/coin.png"),
            icons.ToArray(), font, bold);
        EditorUtility.SetDirty(catalog);
    }

    /// <summary>시트 PNG 의 잘린 스프라이트 12장을 방향*3+열 순서로</summary>
    static SpriteCatalog.Sheet LoadSheet(string file)
    {
        string path = Art + "Sprites/" + file + ".png";
        var frames = new Sprite[12];
        foreach (Object o in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
        {
            if (!(o is Sprite s)) continue;
            // 이름 = {파일}_{방향}_{열}
            string[] parts = s.name.Substring(file.Length + 1).Split('_');
            if (parts.Length != 2 || !int.TryParse(parts[0], out int dir) || !int.TryParse(parts[1], out int col)) continue;
            if (dir < 4 && col < 3) frames[dir * 3 + col] = s;
        }
        return new SpriteCatalog.Sheet { Frames = frames };
    }

    // ---------------- 전장 배경 (render.ts drawTiles + decorate) ----------------
    const int Size = 720;   // MAP.size
    const int Tile = 45;    // render.ts TILE = 720 / 16
    const int Src = 64;     // render.ts SRC
    const int PathInset = 70, BandHalf = 30, DecoCount = 6; // render.ts PATH_INSET, BAND_HALF, DECO_COUNT

    static void BakeField()
    {
        Texture2D terrain = ReadPng(Art + "Tiles/terrain.png");
        Texture2D dirt = ReadPng(Art + "Tiles/dirtpath.png");
        Texture2D deco = ReadPng(Art + "Tiles/deco.png");
        var px = new Color[Size * Size]; // 위→아래 행 순서 (캔버스 좌표)

        int n = (Size + Tile - 1) / Tile;
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
                Blit(px, terrain, Src, Src, gx * Tile, gy * Tile);
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
            {
                if (!IsPath(gx, gy)) continue;
                AutoTile(IsPath(gx, gy - 1), IsPath(gx, gy + 1), IsPath(gx - 1, gy), IsPath(gx + 1, gy), out int cx, out int cy);
                Blit(px, dirt, cx * Src, cy * Src, gx * Tile, gy * Tile);
            }
        int o1 = PathInset - BandHalf, oFar = Size - o1;
        for (int gy = 0; gy < n; gy++)
            for (int gx = 0; gx < n; gx++)
            {
                if (IsPath(gx, gy)) continue;
                float cx = gx * Tile + Tile / 2f, cy = gy * Tile + Tile / 2f;
                if (cx > o1 && cx < oFar && cy > o1 && cy < oFar) continue; // 길 안쪽은 유닛 구역
                if (Hash2(gx, gy) > 0.4) continue;
                int idx = (int)System.Math.Floor(Hash2(gx + 91, gy + 17) * DecoCount);
                Blit(px, deco, idx * Src, 0, gx * Tile, gy * Tile);
            }
        // 비네트: 중심 반경 0.25 → 0.75 에서 검정 알파 0 → 0.28
        float c0 = Size / 2f, r0 = Size * 0.25f, r1 = Size * 0.75f;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float d = Mathf.Sqrt((x + 0.5f - c0) * (x + 0.5f - c0) + (y + 0.5f - c0) * (y + 0.5f - c0));
                float a = 0.28f * Mathf.Clamp01((d - r0) / (r1 - r0));
                Color c = px[y * Size + x];
                px[y * Size + x] = new Color(c.r * (1 - a), c.g * (1 - a), c.b * (1 - a), 1);
            }

        var outTex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var flipped = new Color[Size * Size];
        for (int y = 0; y < Size; y++) System.Array.Copy(px, y * Size, flipped, (Size - 1 - y) * Size, Size);
        outTex.SetPixels(flipped);
        File.WriteAllBytes(FieldPath, outTex.EncodeToPNG());
        Object.DestroyImmediate(outTex);
        Object.DestroyImmediate(terrain);
        Object.DestroyImmediate(dirt);
        Object.DestroyImmediate(deco);
        AssetDatabase.ImportAsset(FieldPath, ImportAssetOptions.ForceUpdate);
    }

    static bool IsPath(int gx, int gy)
    {
        int o1 = PathInset - BandHalf, i1 = PathInset + BandHalf, oFar = Size - o1, iFar = Size - i1;
        float cx = gx * Tile + Tile / 2f, cy = gy * Tile + Tile / 2f;
        bool inOuter = cx >= o1 && cx <= oFar && cy >= o1 && cy <= oFar;
        bool inInner = cx > i1 && cx < iFar && cy > i1 && cy < iFar;
        return inOuter && !inInner;
    }

    /// <summary>render.ts autoTile: 3x3 오토타일에서 이웃 유무로 칸 고르기</summary>
    static void AutoTile(bool up, bool down, bool left, bool right, out int cx, out int cy)
    {
        cx = left && right ? 1 : left ? 2 : right ? 0 : 3;
        cy = up && down ? 1 : up ? 2 : down ? 0 : 3;
    }

    /// <summary>render.ts hash2 - JS 32비트 연산 그대로 (장식 배치가 원본과 같게)</summary>
    static double Hash2(int a, int b)
    {
        long sum = a * 374761393L + b * 668265263L;
        int h = unchecked((int)sum) ^ (a << 13);
        h = unchecked((h ^ (int)((uint)h >> 15)) * 1274126177);
        h ^= (int)((uint)h >> 16);
        return (uint)h / 4294967295.0;
    }

    /// <summary>원본 64px 칸(sx, sy 는 위 기준)을 45px 칸에 최근접 샘플링으로 덮어 그린다 (source-over)</summary>
    static void Blit(Color[] dst, Texture2D src, int sx, int sy, int dx, int dy)
    {
        for (int y = 0; y < Tile; y++)
        {
            int ty = dy + y;
            if (ty >= Size) break;
            int srcY = src.height - 1 - (sy + y * Src / Tile);
            for (int x = 0; x < Tile; x++)
            {
                int tx = dx + x;
                if (tx >= Size) break;
                Color s = src.GetPixel(sx + x * Src / Tile, srcY);
                if (s.a <= 0) continue;
                Color d = dst[ty * Size + tx];
                dst[ty * Size + tx] = new Color(s.r * s.a + d.r * (1 - s.a), s.g * s.a + d.g * (1 - s.a), s.b * s.a + d.b * (1 - s.a), 1);
            }
        }
    }

    static Texture2D ReadPng(string path)
    {
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(path));
        return t;
    }

    // ---------------- 폰트 ----------------
    static TMP_FontAsset BuildFont(string ttfPath, string outPath)
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
        if (existing != null) return existing;
        var ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
        // 픽셀 폰트: Raster (번지지 않게), 표본 크기는 원본 글자 크기 12 의 배수
        TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(ttf, 24, 2, GlyphRenderMode.RASTER_HINTED, 1024, 1024, AtlasPopulationMode.Dynamic, true);
        fa.name = Path.GetFileNameWithoutExtension(outPath);
        AssetDatabase.CreateAsset(fa, outPath);
        fa.atlasTextures[0].name = fa.name + " Atlas";
        fa.atlasTextures[0].filterMode = FilterMode.Point;
        AssetDatabase.AddObjectToAsset(fa.atlasTextures[0], fa);
        fa.material.name = fa.name + " Material";
        AssetDatabase.AddObjectToAsset(fa.material, fa);
        EditorUtility.SetDirty(fa);
        return fa;
    }

    static void SetVersion(TMP_SpriteAsset asset, string version)
    {
        for (System.Type t = asset.GetType(); t != null; t = t.BaseType)
        {
            System.Reflection.FieldInfo f = t.GetField("m_Version", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (f == null) continue;
            f.SetValue(asset, version);
            return;
        }
    }

    // ---------------- 아이콘 TMP Sprite Asset ----------------
    static void BuildIconSpriteAsset()
    {
        // 픽셀 아이콘(16px) + 재화 그림(동전·보석, 원본 icon('coin')/icon('gem'))
        var fileList = new List<string>(Directory.GetFiles(Art + "UI/Icons", "*.png"));
        fileList.Add(Art + "UI/Common/coin.png");
        fileList.Add(Art + "UI/Common/gem.png");
        string[] files = fileList.ToArray();
        var textures = new Texture2D[files.Length];
        for (int i = 0; i < files.Length; i++) textures[i] = ReadPng(files[i]);
        var atlas = new Texture2D(256, 256, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Icons Atlas" };
        Rect[] rects = atlas.PackTextures(textures, 2, 512, false);
        atlas.filterMode = FilterMode.Point;

        var asset = AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(IconsPath);
        if (asset != null) AssetDatabase.DeleteAsset(IconsPath);
        asset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        asset.name = "Icons";
        // 버전이 비어 있으면 TMP 가 옛 형식으로 보고 표를 지우는 변환을 돌린다 - 지금 형식임을 표시
        SetVersion(asset, "1.1.0");
        asset.spriteInfoList = new List<TMP_Sprite>();
        AssetDatabase.CreateAsset(asset, IconsPath);
        AssetDatabase.AddObjectToAsset(atlas, asset);
        asset.spriteSheet = atlas;

        for (int i = 0; i < files.Length; i++)
        {
            Texture2D t = textures[i];
            var rect = new GlyphRect((int)(rects[i].x * atlas.width), (int)(rects[i].y * atlas.height), t.width, t.height);
            // 그림 크기가 달라도(16·18·64px) 글자 줄에서는 같은 크기로: 16 단위 정사각, 아래로 조금 내림
            const float Em = 16f;
            var glyph = new TMP_SpriteGlyph((uint)i, new GlyphMetrics(Em, Em, 0, Em * 0.8f, Em + 1), rect, 1f, 0);
            asset.spriteGlyphTable.Add(glyph);
            var ch = new TMP_SpriteCharacter(0xFFFE, glyph) { name = Path.GetFileNameWithoutExtension(files[i]), scale = 1f };
            asset.spriteCharacterTable.Add(ch);
            Object.DestroyImmediate(t);
        }
        var mat = new Material(Shader.Find("TextMeshPro/Sprite")) { name = "Icons Material" };
        mat.SetTexture(ShaderUtilities.ID_MainTex, atlas);
        asset.material = mat;
        AssetDatabase.AddObjectToAsset(mat, asset);
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset);

        // TMP 기본 Sprite Asset 으로 (Loc.Icon 의 <sprite name="x"> 가 여기서 찾는다)
        var settings = new SerializedObject(TMP_Settings.instance);
        settings.FindProperty("m_defaultSpriteAsset").objectReferenceValue = asset;
        settings.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(TMP_Settings.instance);
    }
}
