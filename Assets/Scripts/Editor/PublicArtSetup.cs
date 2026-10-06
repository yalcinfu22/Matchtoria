using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.U2D.Animation;

/// <summary>Supplies original geometric artwork for checkouts without the private sprite pack.</summary>
[InitializeOnLoad]
public static class PublicArtSetup
{
    private const string Root = "Assets/Sprites/";
    private const string LibraryPath = Root + "SpriteLibrary/TileSprites.spriteLib";
    private const string MarkerPath = Root + ".matchtoria-public-art";
    private const string LibraryGuid = "3ca3355cfaef1a341a7a84195fe3f6bd";
    private static bool s_Running;

    // Only reference identifiers are retained from the private pack. All pixels and
    // sprite rectangles are constructed below, without source images or mesh outlines.
    private sealed class SpriteSpec
    {
        public string Path, Guid, SpriteId, Style, Category, Label;
        public long FileId;
        public int Width, Height, X;
        public bool Single;
        public string Name => Category.Length > 0 ? Category + "_" + Label : Style;

        public SpriteSpec(string path, string guid, long fileId, string spriteId, string style,
            string category = "", string label = "", int width = 128, int height = 128, int x = 0)
        {
            Path = Root + path; Guid = guid; FileId = fileId; SpriteId = spriteId;
            Style = style; Category = category; Label = label;
            Width = width; Height = height; X = x; Single = fileId == 21300000;
        }
    }

    private static readonly SpriteSpec[] Sprites =
    {
        new("Cubes/DefaultState/red.png", "d67592f22618d004e923c17abdf267df", 8166710274925509176, "83ab778d9bbf55170800000000000000", "red", "Matchable", "Red"),
        new("Cubes/DefaultState/blue.png", "38e7267f277b48245a3438cd9383878b", -2615874204119335, "9de494350e4b6fff0800000000000000", "blue", "Matchable", "Blue"),
        new("Cubes/DefaultState/green.png", "0fc088464a87f734ebdb9e73a8c1bdc5", -6099873399732076034, "ef18d122675e85ba0800000000000000", "green", "Matchable", "Green"),
        new("Cubes/DefaultState/yellow.png", "c57f288a9ca5478439af1838e5a7ea7e", -8425141836431729757, "3ab54395322e31b80800000000000000", "yellow", "Matchable", "Yellow"),
        new("Obstacles/Box/box.png", "65ac6e9e139954d448068417354a686f", 7499154508442906066, "2dd228f0b49521860800000000000000", "box1", "Box", "Box1"),
        new("Obstacles/Box/box2.png", "01faa380f5b48d4468a6ba38e653bcb2", -9148079100729670451, "dc445d7c68e7b0180800000000000000", "box2", "Box", "Box2"),
        new("Obstacles/Box/box3.png", "60b397c16fcf67c48850fac469eca8f6", 6879785910209034016, "0273fbca5d8e97f50800000000000000", "box3", "Box", "Box3"),
        new("Obstacles/Vase/vase_01.png", "f325ec41750c8f34092e559e198657e1", -4678962038349899921, "f67dd9af0dcf01fb0800000000000000", "vase2", "Vase", "Vase2"),
        new("Obstacles/Vase/vase_02.png", "94ffdca639311ca40b1c0476e17367b3", -1432975368721100300, "4f19e7f8e7c0d1ce0800000000000000", "vase1", "Vase", "Vase1"),
        new("Obstacles/Stone/stone.png", "196ab7dec693c16449d06c16dec8c80b", 5725184139431673129, "92d768834a0f37f40800000000000000", "stone", "Stone", "Stone"),
        new("Rocket/horizontal_rocket.png", "c81c3e71240cd184da79f0ab5c25ead1", 1184650260280522132, "495381b9229b07010800000000000000", "rocketH", "HorizontalRocket", "HorizontalRocket"),
        new("Rocket/horizontal_rocket_part_left.png", "c4231a492097d6f469c5d597f3cd0461", 8611318781296695245, "dc7d6de8ecc818770800000000000000", "rocketH", "HorizontalRocket", "HorizontalRocketLeft"),
        new("Rocket/horizontal_rocket_part_right.png", "4316e72ab74c32b4bb983ca3afa02415", -4731576832940087401, "797632d70ff065eb0800000000000000", "rocketH", "HorizontalRocket", "HorizontalRocketRight"),
        new("Rocket/vertical_rocket.png", "7189e56f60b32394d95909044855d8e5", 6449952862585347896, "83fd96884f5d28950800000000000000", "rocketV", "VerticalRocket", "VerticalRocket"),
        new("Rocket/vertical_rocket_part_bottom.png", "56a98cff4e6733543afbab9d8232c796", -4697989437084310921, "7726d0c7f736dceb0800000000000000", "rocketV", "VerticalRocket", "VerticalRocketBottom"),
        new("Rocket/vertical_rocket_part_top.png", "e65beeeb3f68cb24aa93d2e1bbeeccd8", 51141016974189086, "e12e236eb70b5b000800000000000000", "rocketV", "VerticalRocket", "VerticalRocketTop"),
        new("TNT/TNT.png", "72f53c4f9f27e764d870b89fb5b92487", -8136218936520485124, "cf6d3e91cf7561f80800000000000000", "tnt", "TNT", "TNT"),
        new("ColorBomb/ColorBomb.png", "4c5d1484f574f7a4d86ff2953326033a", -302002026788859076, "c335dc7b1c21fcbf0800000000000000", "bomb", "ColorBomb", "ColorBomb"),
        new("Load/DreamGamesLogo.jpg", "3cfc4da2f65420c48b1dca4d6c40fb54", 1319224796088813746, "2b8c2c566f3de4210800000000000000", "splash", width: 360, height: 780),
        new("Load/LevelLoad.jpg", "07510af4d6c241d43bb2a68bdd696793", 1319224796088813746, "2b8c2c566f3de4210800000000000000", "loading", width: 360, height: 780),
        new("Load/MainMenuLoad.jpg", "2fb447a4b9fa9db41abfbf4949848622", 6421537375873243299, "3a891d87832ed1950800000000000000", "splash", width: 360, height: 780),
        new("Menu/background.jpg", "7b5f6297161fd5648b58539c148632ae", 1319224796088813746, "2b8c2c566f3de4210800000000000000", "background", width: 360, height: 780),
        new("UI/Gameplay/Popup/popup_base.png", "ed0437d88c1a19943823fe58475e7a17", -5841618373693518811, "52c4a921a076eeea0800000000000000", "panel", width: 256, height: 320),
        new("UI/Gameplay/Top/goal_check.png", "a4e9f92e400df9040be9cb000bf9f554", 8096300645454206010, "a34501f7a86db5070800000000000000", "check"),
        new("UI/Gameplay/Top/top_ui.png", "a770504de121e2e4d91cad5cedd7b687", 21300000, "5e97eb03825dee720800000000000000", "header", width: 512, height: 192),
        new("UI/Menu/framed_button.png", "8adc97f0601bc5e44a8d395a49978015", -1457507993278373161, "7d2a95bc334e5cbe0800000000000000", "button", width: 256, height: 96),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -6210635282160400736, "0a64251cd146fc9a0800000000000000", "board0", width: 32, height: 32, x: 0),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", 1300264690, "2527c5385517bbc4498cc9e5945974f6", "board1", width: 32, height: 32, x: 32),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -456015124, "d464a564801282344bd4716f96900b84", "board2", width: 32, height: 32, x: 64),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -644225781, "a27a4d89a29a8b14ab53e80d1a8aa1a0", "board3", width: 32, height: 32, x: 96),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", 1244980847, "f0db09af5c8c279449837b58df836398", "board4", width: 32, height: 32, x: 128),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -1453034471, "43dfb557d8cbe9d40abbf9188253c968", "board5", width: 32, height: 32, x: 160),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -239891416, "5dbd05fab9ecbcb419737cc3f12e43b4", "board6", width: 32, height: 32, x: 192),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", -1244319714, "7d1c3ef1467e4de47b0a32e0a7ab58d0", "board7", width: 32, height: 32, x: 224),
        new("UI/Gameplay/grid_background.png", "c488bff5605635d47a5b20b4ada9074f", 1109838171, "c295dc1e9b1607e4c843832c2be7e315", "board8", width: 32, height: 32, x: 256)
    };

    static PublicArtSetup()
    {
        EditorApplication.delayCall += EnsureAssets;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) EnsureAssets();
        };
    }

    [MenuItem("Tools/Matchtoria/Create Placeholder Art")]
    public static void EnsureAssets()
    {
        if (s_Running || EditorApplication.isPlaying) return;
        // A private library marks an existing artwork installation. Leave it alone.
        if (File.Exists(LibraryPath) && !File.Exists(MarkerPath)) return;
        s_Running = true;
        try
        {
            // Preflight before writing anything, then mark the installation so an
            // interrupted first import can finish on the next editor launch.
            foreach (string path in Sprites.Select(sprite => sprite.Path).Distinct().Append(LibraryPath))
                if (!File.Exists(path) && File.Exists(path + ".meta") &&
                    !File.ReadAllText(path + ".meta").Contains("userData: Matchtoria public artwork"))
                    throw new InvalidOperationException("Cannot generate public artwork over existing metadata: " + path + ".meta");
            Directory.CreateDirectory(Root);
            if (!File.Exists(MarkerPath))
                File.WriteAllText(MarkerPath, "Generated geometric artwork. Existing files are never overwritten.\n");
            int created = 0;
            foreach (var group in Sprites.GroupBy(sprite => sprite.Path))
            {
                var specs = group.ToArray();
                string path = group.Key;
                if (File.Exists(path)) continue;
                // Public metadata can survive a deleted image. Reuse it during repair.
                bool existingMeta = File.Exists(path + ".meta");
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                Texture2D texture = DrawTexture(specs);
                try
                {
                    File.WriteAllBytes(path, path.EndsWith(".jpg", StringComparison.Ordinal)
                        ? texture.EncodeToJPG(95) : texture.EncodeToPNG());
                }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
                if (!existingMeta) File.WriteAllText(path + ".meta", TextureMeta(specs), new UTF8Encoding(false));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                created++;
            }
            if (!File.Exists(LibraryPath))
            {
                bool existingMeta = File.Exists(LibraryPath + ".meta");
                Directory.CreateDirectory(Path.GetDirectoryName(LibraryPath));
                File.WriteAllText(LibraryPath, LibrarySource(), new UTF8Encoding(false));
                if (!existingMeta) File.WriteAllText(LibraryPath + ".meta", "fileFormatVersion: 2\nguid: " + LibraryGuid +
                    "\nScriptedImporter:\n  internalIDToNameTable: []\n  externalObjects: {}\n  serializedVersion: 2\n" +
                    "  userData: Matchtoria public artwork\n  assetBundleName: \n  assetBundleVariant: \n" +
                    "  script: {fileID: 11500000, guid: db2778f6d440c47ddacff25997d7c062, type: 3}\n", new UTF8Encoding(false));
                created++;
            }
            if (created > 0)
            {
                // Import textures before the library so its sprite references resolve immediately.
                AssetDatabase.ImportAsset(LibraryPath, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                ValidateAssets();
                Debug.Log("Matchtoria: public artwork is ready. Created " + created + " assets.");
            }
        }
        finally { s_Running = false; }
    }

    /// <summary>Batch-friendly check that scene references and runtime library labels resolve.</summary>
    public static void ValidateAssets()
    {
        foreach (var group in Sprites.GroupBy(sprite => sprite.Path))
        {
            var imported = AssetDatabase.LoadAllAssetsAtPath(group.Key).OfType<Sprite>().ToArray();
            foreach (var spec in group)
            {
                bool found = imported.Any(sprite => AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite,
                    out string guid, out long id) && guid == spec.Guid && id == spec.FileId);
                if (!found) throw new InvalidOperationException("Missing public sprite reference: " + spec.Path + " #" + spec.FileId);
            }
        }
        var library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(LibraryPath);
        if (library == null) throw new InvalidOperationException("The tile sprite library did not import.");
        if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(library, out string libraryGuid, out long libraryId) ||
            libraryGuid != LibraryGuid || libraryId != 7108339969198208228)
            throw new InvalidOperationException("The tile sprite library reference does not match the scene prefabs.");
        foreach (var spec in Sprites.Where(sprite => sprite.Category.Length > 0))
            if (library.GetSprite(spec.Category, spec.Label) == null)
                throw new InvalidOperationException("Missing library sprite: " + spec.Category + "/" + spec.Label);
    }

    private static string TextureMeta(SpriteSpec[] specs)
    {
        var first = specs[0];
        var text = new StringBuilder("fileFormatVersion: 2\nguid: " + first.Guid + "\nTextureImporter:\n  internalIDToNameTable:");
        if (first.Single) text.Append(" []");
        else foreach (var spec in specs)
            text.Append($"\n  - first:\n      213: {spec.FileId.ToString(CultureInfo.InvariantCulture)}\n    second: {spec.Name}");
        text.Append("\n  externalObjects: {}\n  serializedVersion: 13\n  mipmaps:\n    enableMipMap: 0\n    sRGBTexture: 1\n" +
            "  isReadable: 0\n  maxTextureSize: 2048\n  textureSettings:\n    serializedVersion: 2\n    filterMode: 1\n    aniso: 1\n    wrapU: 1\n    wrapV: 1\n    wrapW: 1\n" +
            "  nPOTScale: 0\n  spriteMode: " + (first.Single ? "1" : "2") + "\n  spriteExtrude: 0\n  spriteMeshType: 0\n" +
            "  alignment: 0\n  spritePivot: {x: 0.5, y: 0.5}\n  spritePixelsToUnits: 100\n" +
            "  spriteBorder: {x: 0, y: 0, z: 0, w: 0}\n  spriteGenerateFallbackPhysicsShape: 0\n  alphaUsage: 1\n  alphaIsTransparency: 1\n" +
            "  textureType: 8\n  textureShape: 1\n  platformSettings:\n  - serializedVersion: 4\n    buildTarget: DefaultTexturePlatform\n" +
            "    maxTextureSize: 2048\n    textureFormat: -1\n    textureCompression: 0\n    compressionQuality: 100\n    overridden: 0\n" +
            "  spriteSheet:\n    serializedVersion: 2\n    sprites:");
        if (first.Single) text.Append(" []");
        else foreach (var spec in specs)
            text.Append($"\n    - serializedVersion: 2\n      name: {spec.Name}\n      rect:\n        serializedVersion: 2\n        x: {spec.X}\n        y: 0\n        width: {spec.Width}\n        height: {spec.Height}\n" +
                "      alignment: 0\n      pivot: {x: 0.5, y: 0.5}\n      border: {x: 0, y: 0, z: 0, w: 0}\n      customData: \n      outline: []\n      physicsShape: []\n      bones: []\n" +
                $"      spriteID: {spec.SpriteId}\n      internalID: {spec.FileId.ToString(CultureInfo.InvariantCulture)}\n      vertices: []\n      indices: \n      edges: []\n      weights: []");
        text.Append("\n    outline: []\n    physicsShape: []\n    bones: []\n    spriteID: " + (first.Single ? first.SpriteId : "") +
            "\n    internalID: 0\n    vertices: []\n    indices: \n    edges: []\n    weights: []\n    secondaryTextures: []\n    nameFileIdTable:");
        if (first.Single) text.Append(" {}");
        else foreach (var spec in specs) text.Append($"\n      {spec.Name}: {spec.FileId.ToString(CultureInfo.InvariantCulture)}");
        text.Append("\n  userData: Matchtoria public artwork\n  assetBundleName: \n  assetBundleVariant: \n");
        return text.ToString();
    }

    private static string LibrarySource()
    {
        var text = new StringBuilder("%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &1\nMonoBehaviour:\n" +
            "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n" +
            "  m_GameObject: {fileID: 0}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n" +
            "  m_Script: {fileID: 11500000, guid: a5e6fedc2472449cead18ef23b5cb30d, type: 3}\n  m_Name: \n" +
            "  m_EditorClassIdentifier: Unity.2D.Animation.Runtime::UnityEngine.U2D.Animation.SpriteLibrarySourceAsset\n  m_Library:\n");
        foreach (var group in Sprites.Where(sprite => sprite.Category.Length > 0).GroupBy(sprite => sprite.Category))
        {
            text.Append($"  - m_Name: {group.Key}\n    m_Hash: {Animator.StringToHash(group.Key) & 0x3fffffff}\n    m_CategoryList: []\n    m_OverrideEntries:\n");
            foreach (var spec in group)
            {
                string reference = $"{{fileID: {spec.FileId.ToString(CultureInfo.InvariantCulture)}, guid: {spec.Guid}, type: 3}}";
                text.Append($"    - m_Name: {spec.Label}\n      m_Hash: {Animator.StringToHash(spec.Label) & 0x3fffffff}\n      m_Sprite: {reference}\n      m_FromMain: 0\n      m_SpriteOverride: {reference}\n");
            }
            text.Append($"    m_FromMain: 0\n    m_EntryOverrideCount: {group.Count()}\n");
        }
        text.Append("  m_PrimaryLibraryGUID: \n  m_ModificationHash: 1\n  m_Version: 1\n");
        return text.ToString();
    }

    private static readonly Color Red = new Color32(244, 101, 114, 255);
    private static readonly Color Blue = new Color32(80, 177, 238, 255);
    private static readonly Color Green = new Color32(85, 212, 158, 255);
    private static readonly Color Yellow = new Color32(253, 204, 88, 255);
    private static readonly Color Navy = new Color32(23, 36, 58, 255);

    private static Texture2D DrawTexture(SpriteSpec[] specs)
    {
        int width = specs.Max(spec => spec.X + spec.Width), height = specs.Max(spec => spec.Height);
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var pixels = new Color[width * height];
        foreach (var spec in specs)
        {
            for (int y = 0; y < spec.Height; y++)
            for (int x = 0; x < spec.Width; x++)
                pixels[y * width + spec.X + x] = Pixel(spec.Style, (x + .5f) / spec.Width, (y + .5f) / spec.Height);
            if (spec.Style == "splash" || spec.Style == "loading")
            {
                DrawText(pixels, width, height, "MATCHTORIA", 4, height / 2, Color.white);
                if (spec.Style == "loading") DrawText(pixels, width, height, "LOADING", 2, height / 2 - 44, Green);
            }
            if (spec.Style == "tnt") DrawText(pixels, width, height, "TNT", 4, 49, Color.white);
        }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Color Pixel(string style, float u, float v)
    {
        float x = u * 2 - 1, y = v * 2 - 1, ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        Color color = Color.clear;
        switch (style)
        {
            case "red": if (ax < .76f && ay < .76f) color = Red; break;
            case "blue": if (x * x + y * y < .65f) color = Blue; break;
            case "green": if (y > -.76f && y < .84f && ax < (.84f - y) * .52f) color = Green; break;
            case "yellow": if (ax + ay < 1.03f) color = Yellow; break;
            case "stone": if (ax < .77f && ay < .73f && ax + ay < 1.15f) color = new Color(.57f, .64f, .7f); break;
            case "box1": case "box2": case "box3":
                if (ax < .8f && ay < .8f)
                {
                    color = new Color(.65f, .4f, .2f);
                    int health = style[3] - '0';
                    for (int line = 0; line < health; line++)
                        if (Mathf.Abs(y - (line - (health - 1) * .5f) * .42f) < .055f && ax < .62f) color = Yellow;
                }
                break;
            case "vase1": case "vase2":
                if ((y > -.8f && y < .35f && ax < .42f + (.35f - y) * .14f) || (y >= .35f && y < .72f && ax < .28f))
                    color = style == "vase2" ? Blue : new Color(.45f, .7f, .78f);
                if (color.a > 0 && ((ay < .055f) || (style == "vase2" && Mathf.Abs(y + .3f) < .045f))) color = Color.white;
                break;
            case "rocketH": case "rocketV":
                if (style == "rocketV") { float t = x; x = y; y = t; ax = Mathf.Abs(x); ay = Mathf.Abs(y); }
                if ((ax < .53f && ay < .25f) || (ax >= .4f && ax < .87f && ay < (.87f - ax) * 1.05f)) color = Yellow;
                if (ax < .3f && ay < .1f) color = Navy;
                break;
            case "tnt": if (ax < .78f && ay < .66f) color = Red; break;
            case "bomb":
                float radius = x * x + y * y;
                if (radius < .67f) color = x < 0 ? (y < 0 ? Red : Green) : (y < 0 ? Yellow : Blue);
                if (radius > .43f && radius < .55f) color = Color.white;
                break;
            case "check":
                if (DistanceToSegment(x, y, -.7f, -.02f, -.2f, -.5f) < .13f || DistanceToSegment(x, y, -.2f, -.5f, .75f, .58f) < .13f) color = Green;
                break;
            case "splash": case "loading": case "background":
                color = Color.Lerp(Navy, new Color(.13f, .28f, .34f), v);
                if (style != "background" && y > .22f && y < .39f)
                {
                    if (x > -.49f && x < -.28f) color = Red;
                    else if (x > -.23f && x < -.02f) color = Blue;
                    else if (x > .03f && x < .24f) color = Green;
                    else if (x > .29f && x < .5f) color = Yellow;
                }
                return color;
            case "panel": case "header": case "button":
                if (ax < .98f && ay < .98f)
                    color = style == "button" ? Color.Lerp(new Color(.09f, .47f, .38f), Green, v) : Color.Lerp(Navy, new Color(.19f, .33f, .43f), v);
                break;
            default:
                if (style.StartsWith("board", StringComparison.Ordinal)) return style == "board8" ? Navy : new Color(.2f, .43f, .49f);
                break;
        }
        if (color.a > 0) color = Color.Lerp(color, Color.white, Mathf.Clamp01(v - .4f) * .16f);
        return color;
    }

    private static float DistanceToSegment(float x, float y, float ax, float ay, float bx, float by)
    {
        var point = new Vector2(x, y); var start = new Vector2(ax, ay); var delta = new Vector2(bx - ax, by - ay);
        return (point - start - delta * Mathf.Clamp01(Vector2.Dot(point - start, delta) / delta.sqrMagnitude)).magnitude;
    }

    // A compact, independently drawn 5x7 alphabet for the title and tile labels.
    private static void DrawText(Color[] pixels, int width, int height, string word, int scale, int bottom, Color color)
    {
        const string alphabet = "MATCHORILNDG";
        string[] glyphs =
        {
            "10001/11011/10101/10101/10001/10001/10001", "01110/10001/10001/11111/10001/10001/10001",
            "11111/00100/00100/00100/00100/00100/00100", "01111/10000/10000/10000/10000/10000/01111",
            "10001/10001/10001/11111/10001/10001/10001", "01110/10001/10001/10001/10001/10001/01110",
            "11110/10001/10001/11110/10100/10010/10001", "11111/00100/00100/00100/00100/00100/11111",
            "10000/10000/10000/10000/10000/10000/11111", "10001/11001/11001/10101/10011/10011/10001",
            "11110/10001/10001/10001/10001/10001/11110", "01111/10000/10000/10111/10001/10001/01111"
        };
        int left = (width - (word.Length * 6 - 1) * scale) / 2;
        for (int letter = 0; letter < word.Length; letter++)
        {
            int index = alphabet.IndexOf(word[letter]);
            if (index < 0) continue;
            string[] rows = glyphs[index].Split('/');
            for (int row = 0; row < 7; row++)
            for (int column = 0; column < 5; column++)
            {
                if (rows[row][column] != '1') continue;
                for (int dy = 0; dy < scale; dy++)
                for (int dx = 0; dx < scale; dx++)
                {
                    int px = left + (letter * 6 + column) * scale + dx;
                    int py = bottom + (6 - row) * scale + dy;
                    if (px >= 0 && px < width && py >= 0 && py < height) pixels[py * width + px] = color;
                }
            }
        }
    }
}
