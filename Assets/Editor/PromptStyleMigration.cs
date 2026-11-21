using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TextCore;
using UnityEngine.TextCore.LowLevel;

/// <summary>Rebuilds the independent prompt assets and applies them to saved UI.</summary>
public static class PromptStyleMigration
{
    private const string Folder = "Assets/_Game/UI/Resources/PromptStyle";
    private const string Source = "Assets/_Game/UI/Art/Input Prompts/Bit Input Source.png";
    private struct Icon
    {
        public string Name; public int X, Y, W, H;
        public Icon(string name, int x, int y, int w = 1, int h = 1)
        { Name = name; X = x; Y = y; W = w; H = h; }
    }

    [MenuItem("Hold My Beer/UI/Apply Prompt Style")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play mode before migrating prompts.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        CreateFont();
        CreateIcons();
        int changed = 0;
        var prefabs = new[] { "Assets/_Testing/Prefabs/Persistents.prefab" };
        foreach (var path in prefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try { changed += Style(root.GetComponentsInChildren<TMP_Text>(true)); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        // Change loaded scenes in place, keeping the user's current Editor setup.
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || string.IsNullOrEmpty(scene.path)) continue;
            changed += Style(scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<TMP_Text>(true)));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Prompt style applied to " + changed + " saved text components.");
    }

    private static int Style(IEnumerable<TMP_Text> texts)
    {
        int count = 0;
        foreach (var t in texts)
        {
            if (t.name != "PromptText") continue;
            Undo.RecordObject(t, "Style interaction prompt");
            PromptAppearance.Apply(t);
            t.textWrappingMode = TextWrappingModes.Normal;
            t.lineSpacing = 4f;
            t.rectTransform.sizeDelta = new Vector2(1100f,120f);
            t.rectTransform.anchoredPosition = new Vector2(0f,-40f);
            t.alignment = TextAlignmentOptions.Center;
            EditorUtility.SetDirty(t);
            if (PrefabUtility.IsPartOfPrefabInstance(t)) PrefabUtility.RecordPrefabInstancePropertyModifications(t);
            count++;
        }
        return count;
    }

    private static void CreateFont()
    {
        string path = Folder + "/NotJamOldStyle14 SDF.asset";
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if (!font)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Testing/Art/Fonts/NotJamOldStyle14.ttf");
            font = TMP_FontAsset.CreateFontAsset(source, 84, 12, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, false);
            font.name = "NotJamOldStyle14 SDF";
            AssetDatabase.CreateAsset(font, path);
            AssetDatabase.AddObjectToAsset(font.material, font);
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
            // Bake the game's Latin text and punctuation; keep its own source font available for new text.
            var chars = new string(Enumerable.Range(32, 224).Select(i => (char)i).ToArray()) + "\u2013\u2014\u2018\u2019\u201c\u201d\u2022\u2026";
            string missing;
            font.TryAddCharacters(chars, out missing);
        }
        // This pixel font omits typographic punctuation. Use its own matching glyphs
        // so these characters retain the requested font and exterior border.
        var aliases = new Dictionary<uint,uint> { {0x2022,183}, {0x2013,45}, {0x2014,45},
            {0x2018,39}, {0x2019,39}, {0x201c,34}, {0x201d,34} };
        foreach (var alias in aliases)
            if (!font.HasCharacter((int)alias.Key) && font.characterLookupTable.ContainsKey(alias.Value))
                font.characterTable.Add(new TMP_Character(alias.Key,font,font.characterLookupTable[alias.Value].glyph));
        font.ReadFontAssetDefinition();
        var material = font.material;
        material.SetColor("_FaceColor", Color.white);
        material.SetFloat("_FaceDilate", 0f);
        material.SetFloat("_OutlineWidth", 0f);
        material.DisableKeyword("OUTLINE_ON");
        material.SetFloat("_OutlineSoftness", 0f);
        // Underlay is composited behind the untouched face: the visible border is strictly outside.
        material.EnableKeyword("UNDERLAY_ON");
        material.DisableKeyword("UNDERLAY_INNER");
        material.SetColor("_UnderlayColor", new Color32(0x85, 0x28, 0x28, 0xFF));
        material.SetFloat("_UnderlayOffsetX", 0f);
        material.SetFloat("_UnderlayOffsetY", 0f);
        material.SetFloat("_UnderlayDilate", .5f);
        material.SetFloat("_UnderlaySoftness", 0f);
        EditorUtility.SetDirty(material);
        EditorUtility.SetDirty(font);
    }

    private static void CreateIcons()
    {
        var list = new List<Icon>();
        Action<string,int,int> add = (n,x,y) => list.Add(new Icon(n,x,y));
        add("keyboard_escape",17,0);
        for (int i=1;i<=12;i++) add("keyboard_f"+i,17+i,0);
        for (int i=1;i<=9;i++) add("keyboard_"+i,16+i,1);
        add("keyboard_0",26,1);
        string[] rows = { "qwertyuiop", "asdfghjkl", "zxcvbnm" };
        int[] starts = {17,18,19};
        for(int r=0;r<rows.Length;r++)
            for(int c=0;c<rows[r].Length;c++) add("keyboard_"+rows[r][c],starts[r]+c,r+2);
        add("keyboard_arrow_up",30,4); add("keyboard_arrow_right",31,4);
        add("keyboard_arrow_down",32,4); add("keyboard_arrow_left",33,4);
        add("keyboard_minus",27,1); add("keyboard_equals",29,1);
        add("keyboard_tilde",30,0); add("keyboard_bracket_open",27,2);
        add("keyboard_bracket_close",28,2); add("keyboard_slash_back",31,2);
        add("keyboard_semicolon",29,3); add("keyboard_apostrophe",27,3);
        add("keyboard_comma",26,4); add("keyboard_period",27,4);
        add("keyboard_slash_forward",29,4); add("keyboard_win",18,4);
        list.Add(new Icon("keyboard_enter",32,2,2,2));
        list.Add(new Icon("keyboard_numpad_enter",19,7,2));
        list.Add(new Icon("keyboard_space",31,6,3));
        list.Add(new Icon("keyboard_backspace",32,1,2));
        list.Add(new Icon("keyboard_shift",17,7,2));
        list.Add(new Icon("keyboard_ctrl",17,6,2));
        list.Add(new Icon("keyboard_alt",17,5,2));
        list.Add(new Icon("keyboard_tab",19,5,2));
        list.Add(new Icon("keyboard_delete",21,5,2));
        list.Add(new Icon("keyboard_end",23,5,2));
        list.Add(new Icon("keyboard_home",21,6,2));
        list.Add(new Icon("keyboard_page_up",23,6,2));
        list.Add(new Icon("keyboard_page_down",25,6,2));
        list.Add(new Icon("keyboard_insert",19,6,2));
        list.Add(new Icon("keyboard_capslock",23,7,2));
        list.Add(new Icon("keyboard_scroll_lock",21,7,2));
        add("keyboard_numpad_plus",31,4);
        add("mouse_left",9,2); add("mouse_right",10,2); add("mouse_scroll",13,2);
        add("mouse_side_forward",12,2); add("mouse_side_back",12,2);
        add("gamepad_south",4,0); add("gamepad_east",5,0);
        add("gamepad_west",6,0); add("gamepad_north",7,0);
        add("gamepad_leftstick",8,4); add("gamepad_rightstick",8,4);
        add("gamepad_dpad_up",0,1); add("gamepad_dpad_down",1,1);
        add("gamepad_dpad_left",2,1); add("gamepad_dpad_right",3,1);
        var source = new Texture2D(2,2,TextureFormat.RGBA32,false);
        source.LoadImage(File.ReadAllBytes(Source));
        const int cellW=64, cellH=48, columns=8;
        int atlasHeight=Mathf.NextPowerOfTwo(((list.Count+columns-1)/columns)*cellH);
        var atlas = new Texture2D(columns*cellW,atlasHeight,TextureFormat.RGBA32,false);
        atlas.SetPixels32(new Color32[atlas.width*atlas.height]);
        var rects = new List<GlyphRect>();
        var fill = new Color32(0xFB,0xFF,0x8A,0xFF);
        var stroke = new Color32(0x85,0x28,0x28,0xFF);
        for(int i=0;i<list.Count;i++)
        {
            var icon=list[i];
            // Rows 8-15 contain the filled keyboard variants. Mouse and gamepad
            // filled artwork is adjacent to their outlined versions in this pack.
            if (icon.Name.StartsWith("keyboard_")) icon.Y += 8;
            // Mouse row 2 fills only the selected button; row 3 fills the body.
            else if (icon.Name == "gamepad_south" || icon.Name == "gamepad_east" ||
                     icon.Name == "gamepad_west" || icon.Name == "gamepad_north") icon.X += 4;
            int w=icon.W*17-1, h=icon.H*17-1;
            var pixels=source.GetPixels(icon.X*17,source.height-(icon.Y*17+h),w,h);
            int dx=(i%columns)*cellW+2, dy=(i/columns)*cellH+2;
            // Dilate only transparent pixels; never paint the border over the original icon.
            for(int y=-1;y<=h;y++) for(int x=-1;x<=w;x++)
            {
                bool face=x>=0&&x<w&&y>=0&&y<h&&pixels[y*w+x].a>.5f;
                bool near=false;
                if(!face) for(int oy=-1;oy<=1;oy++) for(int ox=-1;ox<=1;ox++)
                { int sx=x+ox,sy=y+oy; if(sx>=0&&sx<w&&sy>=0&&sy<h&&pixels[sy*w+sx].a>.5f)near=true; }
                if(face||near)atlas.SetPixel(dx+x,dy+y,face?fill:stroke);
            }
            rects.Add(new GlyphRect(dx-2,dy-2,w+4,h+4));
        }
        atlas.Apply();
        string texturePath=Folder+"/Bit Input Icons.png";
        File.WriteAllBytes(texturePath,atlas.EncodeToPNG());
        AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceSynchronousImport);
        var importer=(TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType=TextureImporterType.Default;
        importer.alphaIsTransparency=true; importer.mipmapEnabled=false;
        importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.wrapMode=TextureWrapMode.Clamp; importer.SaveAndReimport();
        var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        string assetPath=Folder+"/Bit Input Icons.asset";
        var asset=AssetDatabase.LoadAssetAtPath<TMP_SpriteAsset>(assetPath);
        if(!asset)
        {
            asset=ScriptableObject.CreateInstance<TMP_SpriteAsset>();
            asset.name="Bit Input Icons"; AssetDatabase.CreateAsset(asset,assetPath);
            asset.material=new Material(Shader.Find("TextMeshPro/Sprite"));
            asset.material.name="Bit Input Icons Material";
            AssetDatabase.AddObjectToAsset(asset.material,asset);
        }
        asset.spriteSheet=texture; asset.material.mainTexture=texture;
        asset.material.SetColor("_Color",Color.white);
        var so=new SerializedObject(asset); so.FindProperty("m_Version").stringValue="1.1.0"; so.ApplyModifiedPropertiesWithoutUndo();
        asset.faceInfo=new FaceInfo { pointSize=16,scale=1,lineHeight=20,ascentLine=16,capLine=14,meanLine=10,baseline=0,descentLine=-4 };
        asset.spriteGlyphTable.Clear(); asset.spriteCharacterTable.Clear();
        for(int i=0;i<list.Count;i++)
        {
            var r=rects[i];
            float scale = 1.15f*16f/(list[i].H*17-1);
            if (list[i].Name == "keyboard_e") scale *= 1.21f;
            // Center the icon's ink on the font's capital-letter center (48 / 84).
            float bearingY = (r.height-4)*.5f + 2f + (16f*(48f/84f)*.5f)/scale;
            var glyph=new TMP_SpriteGlyph((uint)i,new GlyphMetrics(r.width,r.height,-2,bearingY,r.width),r,1,0);
            asset.spriteGlyphTable.Add(glyph);
            asset.spriteCharacterTable.Add(new TMP_SpriteCharacter((uint)(0xE000+i),asset,glyph)
                {name=list[i].Name,scale=scale});
        }
        asset.UpdateLookupTables();
        EditorUtility.SetDirty(asset); EditorUtility.SetDirty(asset.material);
        UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(atlas);
    }
}
