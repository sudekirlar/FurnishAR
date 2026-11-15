using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;

// Editor tool that renders the catalog icons from the prefabs and puts them on the buttons.
public static class FurnishCatalogIcons
{
    const int k_Size = 256;
    const int k_RenderSize = 768;
    const float k_Padding = 0.08f;
    // empty layer, so the icon camera sees only the item
    const int k_IconLayer = 31;
    const string k_Folder = "Assets/Furniture/Icons/Generated";
    const float k_FieldOfView = 25f;
    const float k_Pitch = 22f;
    const float k_Yaw = 35f;

    [MenuItem("Tools/FurnishAR/Generate Catalog Icons")]
    static void Generate()
    {
        var spawner = Object.FindAnyObjectByType<ObjectSpawner>(FindObjectsInactive.Include);
        if (spawner == null)
        {
            EditorUtility.DisplayDialog("FurnishAR", "Sahnede ObjectSpawner bulunamadı.", "Tamam");
            return;
        }

        Directory.CreateDirectory(k_Folder);

        var sprites = new List<Sprite>();
        try
        {
            var prefabs = spawner.objectPrefabs;
            for (int i = 0; i < prefabs.Count; i++)
            {
                EditorUtility.DisplayProgressBar("FurnishAR", $"İkon: {prefabs[i]?.name}", (float)i / prefabs.Count);
                sprites.Add(prefabs[i] != null ? RenderIcon(prefabs[i]) : null);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        int assigned = AssignToButtons(sprites);
        Debug.Log($"[FurnishAR] {sprites.Count} ikon üretildi ({k_Folder}), {assigned} katalog butonuna atandı.");
    }

    static Sprite RenderIcon(GameObject prefab)
    {
        var instance = Object.Instantiate(prefab);
        instance.hideFlags = HideFlags.HideAndDontSave;
        instance.transform.SetPositionAndRotation(new Vector3(0f, -500f, 0f), Quaternion.identity);
        SetLayerRecursively(instance.transform, k_IconLayer);

        var cameraObject = new GameObject("FurnishIconCamera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = cameraObject.AddComponent<Camera>();
        var texture = new RenderTexture(k_RenderSize, k_RenderSize, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB)
        {
            antiAliasing = 4
        };

        try
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return null;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
                bounds.Encapsulate(r.bounds);

            camera.enabled = false;
            camera.cullingMask = 1 << k_IconLayer;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0f, 0f, 0f, 0f);
            camera.fieldOfView = k_FieldOfView;
            camera.targetTexture = texture;

            var direction = Quaternion.Euler(-k_Pitch, k_Yaw, 0f) * Vector3.forward;
            float radius = bounds.extents.magnitude;
            float distance = radius / Mathf.Sin(k_FieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            camera.transform.position = bounds.center + direction * distance;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = Mathf.Max(0.01f, distance - radius * 2f);
            camera.farClipPlane = distance + radius * 2f;

            camera.Render();

            var previous = RenderTexture.active;
            RenderTexture.active = texture;
            var render = new Texture2D(k_RenderSize, k_RenderSize, TextureFormat.RGBA32, false);
            render.ReadPixels(new Rect(0, 0, k_RenderSize, k_RenderSize), 0, 0);
            render.Apply();
            RenderTexture.active = previous;

            var image = CropToContent(render, prefab.name);
            Object.DestroyImmediate(render);

            string path = $"{k_Folder}/{prefab.name}.png";
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
        finally
        {
            camera.targetTexture = null;
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(instance);
        }
    }

    // My first version framed the bounding sphere. Tall or wide items looked tiny and off center,
    // so now I crop the image to the visible pixels.
    static Texture2D CropToContent(Texture2D source, string name)
    {
        var pixels = source.GetPixels32();
        int w = source.width, h = source.height;
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (pixels[y * w + x].a <= 5)
                    continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        var result = new Texture2D(k_Size, k_Size, TextureFormat.RGBA32, false);

        bool empty = maxX < 0;
        bool noAlpha = !empty && minX == 0 && minY == 0 && maxX == w - 1 && maxY == h - 1;
        if (empty || noAlpha)
        {
            if (noAlpha)
                Debug.LogWarning($"[FurnishAR] {name}: arka plan saydam değil, ikon kırpılmadan küçültüldü.");
            Resample(source, result, 0f, 0f, w);
            return result;
        }

        float centerX = (minX + maxX + 1) * 0.5f;
        float centerY = (minY + maxY + 1) * 0.5f;
        float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) / (1f - 2f * k_Padding);
        Resample(source, result, centerX - side * 0.5f, centerY - side * 0.5f, side);
        return result;
    }

    static void Resample(Texture2D source, Texture2D target, float x0, float y0, float side)
    {
        int size = target.width;
        var output = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float sx = x0 + (x + 0.5f) / size * side;
                float sy = y0 + (y + 0.5f) / size * side;
                bool inside = sx >= 0f && sy >= 0f && sx < source.width && sy < source.height;
                output[y * size + x] = inside
                    ? source.GetPixelBilinear(sx / source.width, sy / source.height)
                    : new Color(0f, 0f, 0f, 0f);
            }
        }
        target.SetPixels(output);
        target.Apply();
    }

    static int AssignToButtons(List<Sprite> sprites)
    {
        int assigned = 0;
        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            if (button.onClick.GetPersistentEventCount() == 0 ||
                button.onClick.GetPersistentMethodName(0) != "SetObjectToSpawn")
                continue;

            var so = new SerializedObject(button);
            int index = so.FindProperty("m_OnClick.m_PersistentCalls.m_Calls.Array.data[0].m_Arguments.m_IntArgument").intValue;
            if (index < 0 || index >= sprites.Count || sprites[index] == null)
                continue;

            var iconTransform = button.transform.Find("ObjectIcon");
            var icon = iconTransform != null ? iconTransform.GetComponent<Image>() : null;
            if (icon == null)
                continue;

            Undo.RecordObject(icon, "Assign Catalog Icon");
            icon.sprite = sprites[index];
            icon.preserveAspect = true;
            EditorUtility.SetDirty(icon);
            assigned++;
        }

        if (assigned > 0)
            EditorSceneManager.MarkAllScenesDirty();
        return assigned;
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }
}
