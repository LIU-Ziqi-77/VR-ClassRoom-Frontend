using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public static class CreateRulerPrefab
{
    const string MeshPath = "Assets/High school classroom/Models/Ruler.asset";
    const string MaterialPath = "Assets/High school classroom/Models/Materials/Ruler.mat";
    const string TexturePath = "Assets/High school classroom/Textures/Ruler.png";
    const string PrefabPath = "Assets/High school classroom/Prefabs/Ruler.prefab";

    static readonly Color32 Plastic = new Color32(255, 230, 133, 255);
    static readonly Color32 Edge = new Color32(158, 119, 55, 255);
    static readonly Color32 Mark = new Color32(78, 61, 34, 255);

    [MenuItem("Tools/VR Classroom/Create Ruler Prefab")]
    public static void Generate()
    {
        GenerateInternal(false);
    }

    public static void GenerateBatch()
    {
        try
        {
            GenerateInternal(true);
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    static void GenerateInternal(bool exitOnComplete)
    {
        EnsureFolder("Assets/High school classroom/Models");
        EnsureFolder("Assets/High school classroom/Models/Materials");
        EnsureFolder("Assets/High school classroom/Textures");
        EnsureFolder("Assets/High school classroom/Prefabs");

        Mesh mesh = CreateRulerMesh();
        AssetDatabase.DeleteAsset(MeshPath);
        AssetDatabase.CreateAsset(mesh, MeshPath);

        Texture2D texture = CreateRulerTexture();
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporter(TexturePath);

        Material material = CreateRulerMaterial(AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
        AssetDatabase.DeleteAsset(MaterialPath);
        AssetDatabase.CreateAsset(material, MaterialPath);

        GameObject ruler = new GameObject("Ruler");
        ruler.transform.localScale = Vector3.one;

        MeshFilter meshFilter = ruler.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);

        MeshRenderer meshRenderer = ruler.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        BoxCollider collider = ruler.AddComponent<BoxCollider>();
        collider.size = new Vector3(0.3f, 0.003f, 0.035f);

        Rigidbody body = ruler.AddComponent<Rigidbody>();
        body.mass = 0.03f;
        body.useGravity = true;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        ruler.AddComponent<XRGrabInteractable>();

        ClassroomItemHandle itemHandle = ruler.AddComponent<ClassroomItemHandle>();
        itemHandle.itemType = ClassroomItemType.Ruler;

        PrefabUtility.SaveAsPrefabAsset(ruler, PrefabPath);
        UnityEngine.Object.DestroyImmediate(ruler);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"Created ruler prefab at {PrefabPath}");

        if (exitOnComplete)
        {
            EditorApplication.Exit(0);
        }
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
        string folder = Path.GetFileName(path);
        if (!string.IsNullOrEmpty(parent))
        {
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder);
        }
    }

    static Mesh CreateRulerMesh()
    {
        const float length = 0.3f;
        const float width = 0.035f;
        const float thickness = 0.01f;

        float x = length * 0.5f;
        float y = thickness * 0.5f;
        float z = width * 0.5f;

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var triangles = new List<int>();

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z),
            Vector3.up,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, -y, -z), new Vector3(-x, -y, -z),
            Vector3.down,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f));

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, -y, z),
            Vector3.forward,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(-x, y, -z), new Vector3(-x, -y, -z),
            Vector3.back,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(-x, -y, z),
            Vector3.left,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

        AddFace(vertices, normals, uvs, triangles,
            new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(x, y, -z), new Vector3(x, -y, -z),
            Vector3.right,
            new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f));

        Mesh mesh = new Mesh
        {
            name = "Ruler"
        };
        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddFace(
        List<Vector3> vertices,
        List<Vector3> normals,
        List<Vector2> uvs,
        List<int> triangles,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        Vector3 normal,
        Vector2 uvA,
        Vector2 uvB,
        Vector2 uvC,
        Vector2 uvD)
    {
        int start = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        normals.Add(normal);
        uvs.Add(uvA);
        uvs.Add(uvB);
        uvs.Add(uvC);
        uvs.Add(uvD);
        triangles.Add(start);
        triangles.Add(start + 2);
        triangles.Add(start + 1);
        triangles.Add(start);
        triangles.Add(start + 3);
        triangles.Add(start + 2);
    }

    static Texture2D CreateRulerTexture()
    {
        const int width = 1024;
        const int height = 128;
        const int margin = 34;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[width * height];
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Plastic;
        }
        texture.SetPixels32(pixels);

        DrawRect(texture, 0, 0, width, height, Edge);
        DrawRect(texture, 1, 1, width - 2, height - 2, Edge);

        int usable = width - margin * 2;
        for (int mm = 0; mm <= 300; mm += 5)
        {
            int px = Mathf.RoundToInt(margin + usable * (mm / 300f));
            bool isCentimeter = mm % 10 == 0;
            int tickHeight = isCentimeter ? 58 : 34;
            DrawLine(texture, px, height - 10, px, height - 10 - tickHeight, isCentimeter ? 3 : 1, Mark);

            if (isCentimeter && mm % 50 == 0 && mm < 300)
            {
                string label = (mm / 10).ToString();
                DrawText(texture, label, px - label.Length * 6, 16, 3, Mark);
            }
        }

        DrawText(texture, "cm", width - 58, 16, 3, Mark);
        texture.Apply(false, false);
        return texture;
    }

    static Material CreateRulerMaterial(Texture2D texture)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = "Ruler"
        };

        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
        if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
        if (material.HasProperty("_ReceiveShadows")) material.SetFloat("_ReceiveShadows", 0f);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.08f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);

        return material;
    }

    static void ConfigureTextureImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Default;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    static void DrawRect(Texture2D texture, int x, int y, int width, int height, Color32 color)
    {
        DrawLine(texture, x, y, x + width - 1, y, 1, color);
        DrawLine(texture, x, y + height - 1, x + width - 1, y + height - 1, 1, color);
        DrawLine(texture, x, y, x, y + height - 1, 1, color);
        DrawLine(texture, x + width - 1, y, x + width - 1, y + height - 1, 1, color);
    }

    static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, int thickness, Color32 color)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            DrawPoint(texture, x0, y0, thickness, color);
            if (x0 == x1 && y0 == y1) break;
            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }
    }

    static void DrawPoint(Texture2D texture, int x, int y, int thickness, Color32 color)
    {
        int radius = Mathf.Max(0, thickness / 2);
        for (int yy = y - radius; yy <= y + radius; yy++)
        {
            for (int xx = x - radius; xx <= x + radius; xx++)
            {
                if (xx >= 0 && xx < texture.width && yy >= 0 && yy < texture.height)
                {
                    texture.SetPixel(xx, yy, color);
                }
            }
        }
    }

    static void DrawText(Texture2D texture, string text, int x, int y, int scale, Color32 color)
    {
        int offset = 0;
        foreach (char c in text)
        {
            DrawGlyph(texture, c, x + offset, y, scale, color);
            offset += 5 * scale;
        }
    }

    static void DrawGlyph(Texture2D texture, char c, int x, int y, int scale, Color32 color)
    {
        string[] glyph = GetGlyph(c);
        for (int row = 0; row < glyph.Length; row++)
        {
            for (int col = 0; col < glyph[row].Length; col++)
            {
                if (glyph[row][col] != '1') continue;

                for (int yy = 0; yy < scale; yy++)
                {
                    for (int xx = 0; xx < scale; xx++)
                    {
                        int px = x + col * scale + xx;
                        int py = y + (glyph.Length - 1 - row) * scale + yy;
                        if (px >= 0 && px < texture.width && py >= 0 && py < texture.height)
                        {
                            texture.SetPixel(px, py, color);
                        }
                    }
                }
            }
        }
    }

    static string[] GetGlyph(char c)
    {
        switch (c)
        {
            case '0': return new[] { "111", "101", "101", "101", "111" };
            case '1': return new[] { "010", "110", "010", "010", "111" };
            case '2': return new[] { "111", "001", "111", "100", "111" };
            case '3': return new[] { "111", "001", "111", "001", "111" };
            case '4': return new[] { "101", "101", "111", "001", "001" };
            case '5': return new[] { "111", "100", "111", "001", "111" };
            case '6': return new[] { "111", "100", "111", "101", "111" };
            case '7': return new[] { "111", "001", "010", "010", "010" };
            case '8': return new[] { "111", "101", "111", "101", "111" };
            case '9': return new[] { "111", "101", "111", "001", "111" };
            case 'c': return new[] { "111", "100", "100", "100", "111" };
            case 'm': return new[] { "101", "111", "111", "101", "101" };
            default: return new[] { "000", "000", "000", "000", "000" };
        }
    }
}
