using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Reference-inspired cafe doors, using the saloon's existing timber and pixel lighting.</summary>
public static class ReferenceSaloonDoorBuilder
{
    private const string Folder = "Assets/_Game/Environment/Saloon Doors";
    private static Material oak, slatOak, slatVariation, darkOak, iron;

    public static string Install()
    {
        if (EditorApplication.isPlaying) return "Exit Play Mode first.";
        var root = GameObject.Find("Slatted saloon doors");
        if (!root || !root.transform.Find("West leaf") || !root.transform.Find("East leaf")) return "Entrance leaves not found; no changes made.";
        EnsureFolder(Folder);
        UpdateWoodFinish();
        iron = AssetDatabase.LoadAssetAtPath<Material>("Packages/com.unity.render-pipelines.universal/Editor/SceneTemplates/Materials/Metal_Black.mat");
        Undo.RegisterFullObjectHierarchyUndo(root, "Restyle saloon doors from reference");
        BuildLeaf(root.transform.Find("West leaf"), 1);
        BuildLeaf(root.transform.Find("East leaf"), -1);
        // Save a reusable copy without changing the original vendor prefab.
        var copy = Object.Instantiate(root);
        copy.name = "Louvred Oak Saloon Doors";
        copy.transform.SetParent(null); copy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        copy.transform.localScale = Vector3.one;
        PrefabUtility.SaveAsPrefabAsset(copy, Folder + "/Louvred Oak Saloon Doors.prefab");
        Object.DestroyImmediate(copy);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(root.scene); EditorSceneManager.SaveScene(root.scene);
        Selection.activeGameObject = root;
        return "Saved reference-style oak saloon doors in Level 1 and as a reusable prefab.";
    }

    private static void BuildLeaf(Transform leaf, int direction)
    {
        while (leaf.childCount > 0) Undo.DestroyObjectImmediate(leaf.GetChild(leaf.childCount - 1).gameObject);
        var outer = Box("Outer oak upright", leaf, new Vector3(direction*.105f,1.96f,0), new Vector3(2.32f,.21f,.15f), oak);
        outer.localRotation = Quaternion.Euler(0,0,90);
        var inner = Box("Meeting oak upright", leaf, new Vector3(direction*1.635f,2.12f,0), new Vector3(2.46f,.21f,.15f), oak);
        inner.localRotation = Quaternion.Euler(0,0,90);
        Rail("Curved crown", leaf, direction, true);
        Rail("Curved lower rail", leaf, direction, false);
        foreach (float z in new[]{-.081f,.081f})
        {
            Box("Upper recessed beading", leaf, new Vector3(direction*.87f,2.85f,z), new Vector3(1.31f,.065f,.032f),darkOak);
            Box("Lower recessed beading", leaf, new Vector3(direction*.87f,1.205f,z), new Vector3(1.31f,.065f,.032f),darkOak);
            Box("Hinge-side inset moulding", leaf, new Vector3(direction*.23f,2.025f,z), new Vector3(.048f,1.66f,.032f),darkOak);
            Box("Meeting-side inset moulding", leaf, new Vector3(direction*1.51f,2.025f,z), new Vector3(.048f,1.66f,.032f),darkOak);
        }
        var louverMesh = BevelledLouver();
        for (int i=0;i<13;i++)
        {
            var obj = new GameObject("Overlapping oak louver " + (i+1).ToString("00"), typeof(MeshFilter), typeof(MeshRenderer));
            obj.transform.SetParent(leaf,false);
            obj.transform.localPosition = new Vector3(direction*.87f,1.30f+i*.12f,0);
            obj.transform.localRotation = Quaternion.Euler(-28,0,0);
            obj.GetComponent<MeshFilter>().sharedMesh = louverMesh;
            obj.GetComponent<MeshRenderer>().sharedMaterial = i%3==1?slatVariation:slatOak;
        }
        foreach(float y in new[]{1.09f,2.86f})
        {
            var pin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pin.name="Iron hinge barrel"; pin.transform.SetParent(leaf,false);
            pin.transform.localPosition=new Vector3(-direction*.025f,y,0);
            pin.transform.localScale=new Vector3(.065f,.13f,.065f);
            Object.DestroyImmediate(pin.GetComponent<Collider>()); pin.GetComponent<MeshRenderer>().sharedMaterial=iron;
            foreach(float z in new[]{-.09f,.09f})
            {
                Box("Small iron hinge plate",leaf,new Vector3(direction*.095f,y,z),new Vector3(.17f,.16f,.025f),iron);
                foreach(float offset in new[]{-.045f,.045f})
                {
                    var screw=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    screw.name="Hinge screw"; screw.transform.SetParent(leaf,false);
                    screw.transform.localPosition=new Vector3(direction*.12f,y+offset,z+Mathf.Sign(z)*.016f);
                    screw.transform.localRotation=Quaternion.Euler(90,0,0);
                    screw.transform.localScale=new Vector3(.026f,.007f,.026f);
                    Object.DestroyImmediate(screw.GetComponent<Collider>()); screw.GetComponent<MeshRenderer>().sharedMaterial=iron;
                }
            }
        }
    }

    private static Transform Box(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube); obj.name=name;
        obj.transform.SetParent(parent,false); obj.transform.localPosition=position; obj.transform.localScale=scale;
        Object.DestroyImmediate(obj.GetComponent<Collider>()); obj.GetComponent<MeshRenderer>().sharedMaterial=material;
        return obj.transform;
    }

    private static void Rail(string name, Transform parent, int direction, bool top)
    {
        const int segments=24;
        const float left=.18f,right=1.56f,depth=.075f;
        var verts=new List<Vector3>(); var uv=new List<Vector2>(); var indices=new List<int>();
        for(int i=0;i<segments;i++)
        {
            float t0=(float)i/segments,t1=(float)(i+1)/segments;
            float x0=Mathf.Lerp(left,right,t0),x1=Mathf.Lerp(left,right,t1);
            float curve0=.5f-.5f*Mathf.Cos(t0*Mathf.PI),curve1=.5f-.5f*Mathf.Cos(t1*Mathf.PI);
            float bottom0=top?2.885f:.80f+.16f*curve0,bottom1=top?2.885f:.80f+.16f*curve1;
            float top0=top?3.08f+.21f*curve0:1.18f,top1=top?3.08f+.21f*curve1:1.18f;
            var a=new Vector3(x0,bottom0,depth);var b=new Vector3(x1,bottom1,depth);
            var c=new Vector3(x1,top1,depth);var d=new Vector3(x0,top0,depth);
            Quad(verts,uv,indices,a,b,c,d,direction);
            a.z=b.z=c.z=d.z=-depth; Quad(verts,uv,indices,b,a,d,c,direction);
            Quad(verts,uv,indices,new Vector3(x0,top0,-depth),new Vector3(x0,top0,depth),new Vector3(x1,top1,depth),new Vector3(x1,top1,-depth),direction);
            Quad(verts,uv,indices,new Vector3(x0,bottom0,depth),new Vector3(x0,bottom0,-depth),new Vector3(x1,bottom1,-depth),new Vector3(x1,bottom1,depth),direction);
            if(i==0)Quad(verts,uv,indices,new Vector3(x0,bottom0,-depth),new Vector3(x0,bottom0,depth),new Vector3(x0,top0,depth),new Vector3(x0,top0,-depth),direction);
            if(i==segments-1)Quad(verts,uv,indices,new Vector3(x1,bottom1,depth),new Vector3(x1,bottom1,-depth),new Vector3(x1,top1,-depth),new Vector3(x1,top1,depth),direction);
        }
        var generated=new Mesh{name=(direction>0?"West ":"East ")+name};generated.SetVertices(verts);generated.SetUVs(0,uv);generated.SetTriangles(indices,0);
        generated.RecalculateNormals();generated.RecalculateTangents();generated.RecalculateBounds();
        string path=Folder+"/"+generated.name+".asset";
        var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=generated;AssetDatabase.CreateAsset(mesh,path);}
        else{mesh.Clear();mesh.vertices=generated.vertices;mesh.uv=generated.uv;mesh.triangles=generated.triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();Object.DestroyImmediate(generated);EditorUtility.SetDirty(mesh);}
        var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);
        obj.GetComponent<MeshFilter>().sharedMesh=mesh;obj.GetComponent<MeshRenderer>().sharedMaterial=oak;
    }

    private static void Quad(List<Vector3> v,List<Vector2> uv,List<int> t,Vector3 a,Vector3 b,Vector3 c,Vector3 d,int direction)
    {
        int i=v.Count;
        foreach(var point in new[]{a,b,c,d}){v.Add(new Vector3(point.x*direction,point.y,point.z));uv.Add(new Vector2(point.x*.7f,point.y*2f));}
        if(direction>0){t.AddRange(new[]{i,i+1,i+2,i,i+2,i+3});}
        else{t.AddRange(new[]{i,i+2,i+1,i,i+3,i+2});}
    }

    public static string MatchRoomPalette()
    {
        UpdateWoodFinish();
        AssetDatabase.SaveAssets();
        return "Matched door timber to the wall palette while retaining its louver geometry.";
    }

    private static void UpdateWoodFinish()
    {
        var wall = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Testing/Art/Main Menu/Saloon Warm Timber Walls.mat");
        var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Saloon worn oak grain.png");
        Color wallMean = AverageLinear(wall.GetTexture("_BaseMap")), grainMean = AverageLinear(grain);
        Color target = wall.GetColor("_BaseColor").linear;
        // Match the room's reflected color, accounting for sRGB material colors and the grain's brown albedo.
        Color matched = new Color(target.r * wallMean.r / Mathf.Max(.001f, grainMean.r),
            target.g * wallMean.g / Mathf.Max(.001f, grainMean.g),
            target.b * wallMean.b / Mathf.Max(.001f, grainMean.b), 1).gamma;
        oak = Timber("Warm oak frame", Shade(matched,.90f), new Vector2(.65f,.18f), .12f);
        slatOak = Timber("Warm oak louvers", matched, new Vector2(.65f,.12f), .21f);
        slatVariation = Timber("Warm oak louver variation", Shade(matched,.94f), new Vector2(.65f,.12f), .65f);
        darkOak = Timber("Recessed oak beading", Shade(matched,.62f), new Vector2(.65f,.18f), .37f);
    }

    private static Color Shade(Color color,float amount) { return new Color(color.r*amount,color.g*amount,color.b*amount,1); }

    private static Color AverageLinear(Texture texture)
    {
        var before = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(32,32,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        var image = new Texture2D(32,32,TextureFormat.RGBAFloat,false,true);
        try
        {
            Graphics.Blit(texture,rt); RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,32,32),0,0); image.Apply();
            var pixels=image.GetPixels(); Color total=Color.clear;
            foreach(var pixel in pixels) total+=pixel;
            return total/pixels.Length;
        }
        finally { RenderTexture.active=before; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(image); }
    }

    private static Material Timber(string name,Color color,Vector2 tiling,float offset)
    {
        string path=Folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        var source=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Testing/Art/Main Menu/Saloon Warm Timber Walls.mat");
        if(!mat){mat=new Material(source);mat.name=name;AssetDatabase.CreateAsset(mat,path);}
        var grain = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Saloon worn oak grain.png");
        mat.SetTexture("_BaseMap",grain);mat.SetTexture("_MainTex",grain);
        mat.SetColor("_BaseColor",color);mat.SetColor("_Color",color);
        mat.SetTextureScale("_BaseMap",tiling);mat.SetTextureOffset("_BaseMap",new Vector2(0,offset));
        foreach(var map in new[]{"_BumpMap","_NormalMap","_ParallaxMap","_OcclusionMap"}) if(mat.HasProperty(map))mat.SetTexture(map,null);
        foreach(var key in new[]{"_NORMALMAP","_PARALLAXMAP","_OCCLUSIONMAP"})mat.DisableKeyword(key);
        // This normal is converted from the same grain image during import, so the relief matches the color.
        mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Saloon worn oak grain normal.png"));
        mat.EnableKeyword("_NORMALMAP");
        mat.SetFloat("_BumpScale",.38f);mat.SetFloat("_Parallax",0);mat.SetFloat("_OcclusionStrength",0);
        mat.SetFloat("_Smoothness",.035f);EditorUtility.SetDirty(mat);return mat;
    }
    private static Mesh BevelledLouver()
    {
        string path = Folder + "/Bevelled overlapping louver.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) return existing;
        const float halfWidth = .6475f, halfHeight = .070f, halfDepth = .016f, bevel = .005f;
        var profile = new[]{
            new Vector2(-halfHeight,-halfDepth+bevel), new Vector2(-halfHeight+bevel,-halfDepth),
            new Vector2(halfHeight-bevel,-halfDepth), new Vector2(halfHeight,-halfDepth+bevel),
            new Vector2(halfHeight,halfDepth-bevel), new Vector2(halfHeight-bevel,halfDepth),
            new Vector2(-halfHeight+bevel,halfDepth), new Vector2(-halfHeight,halfDepth-bevel)
        };
        var vertices = new List<Vector3>(); var uvs = new List<Vector2>(); var triangles = new List<int>();
        for(int j=0;j<profile.Length;j++)
        {
            var a=profile[j]; var b=profile[(j+1)%profile.Length];
            int start=vertices.Count;
            vertices.Add(new Vector3(-halfWidth,a.x,a.y));vertices.Add(new Vector3(-halfWidth,b.x,b.y));
            vertices.Add(new Vector3(halfWidth,b.x,b.y));vertices.Add(new Vector3(halfWidth,a.x,a.y));
            uvs.Add(new Vector2(0,a.x/.14f+.5f));uvs.Add(new Vector2(0,b.x/.14f+.5f));
            uvs.Add(new Vector2(1,b.x/.14f+.5f));uvs.Add(new Vector2(1,a.x/.14f+.5f));
            triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
        }
        foreach(float side in new[]{-1f,1f})
        {
            int center=vertices.Count;vertices.Add(new Vector3(side*halfWidth,0,0));uvs.Add(new Vector2(.5f,.5f));
            for(int j=0;j<profile.Length;j++)
            {vertices.Add(new Vector3(side*halfWidth,profile[j].x,profile[j].y));uvs.Add(new Vector2(profile[j].y/.032f+.5f,profile[j].x/.14f+.5f));}
            for(int j=0;j<profile.Length;j++)
            {
                int a=center+1+j,b=center+1+(j+1)%profile.Length;
                triangles.AddRange(side<0?new[]{center,b,a}:new[]{center,a,b});
            }
        }
        var mesh=new Mesh{name="Thin bevelled timber louver"};mesh.SetVertices(vertices);mesh.SetUVs(0,uvs);mesh.SetTriangles(triangles,0);
        mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);return mesh;
    }

    private static void EnsureFolder(string path)
    {
        if(AssetDatabase.IsValidFolder(path))return;
        int cut=path.LastIndexOf('/');EnsureFolder(path.Substring(0,cut));AssetDatabase.CreateFolder(path.Substring(0,cut),path.Substring(cut+1));
    }
}
