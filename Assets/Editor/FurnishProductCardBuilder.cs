using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;

// Editor tool that adds ProductInfo to the catalog prefabs and builds the info card.
public static class FurnishProductCardBuilder
{
    // prices are made up, it is a demo store
    static readonly Dictionary<string, (string name, int price)> k_Catalog = new Dictionary<string, (string, int)>
    {
        { "Vintage_ArmChair", ("Victorian Armchair", 9400) },
        { "Vintage_Sofa", ("Victorian Sofa", 18900) },
        { "Vintage_CoffeeTable", ("Painted Coffee Table", 7250) },
        { "Vintage_Console", ("Gothic Console", 12600) },
        { "Vintage_OilLamp", ("Antique Oil Lamp", 2150) },
        { "Vintage_Candlestick", ("Turned Wood Candlestick", 480) },
        { "Vintage_Plant", ("Potted Aloe", 320) },
        { "Vintage_Vase", ("Antique Ceramic Vase", 1350) },
    };

    static readonly Color k_Cream = Hex("F4EFE6");
    static readonly Color k_Ink = Hex("2F2620");
    static readonly Color k_Brass = Hex("A8834A");
    static readonly Color k_Muted = Hex("7A6C5F");

    const string k_CardName = "Product Card";

    [MenuItem("Tools/FurnishAR/Create Product Card")]
    static void Build()
    {
        var menuManager = Object.FindAnyObjectByType<ARTemplateMenuManager>(FindObjectsInactive.Include);
        var spawner = Object.FindAnyObjectByType<ObjectSpawner>(FindObjectsInactive.Include);
        if (menuManager == null || spawner == null)
        {
            EditorUtility.DisplayDialog("FurnishAR", "Sahnede ARTemplateMenuManager veya ObjectSpawner bulunamadı.", "Tamam");
            return;
        }

        int added = AddProductInfoToPrefabs(spawner);

        var canvas = menuManager.transform;
        var existing = canvas.Find(k_CardName);
        if (existing != null)
        {
            if (!EditorUtility.DisplayDialog("FurnishAR", "Zaten bir Product Card var. Silinip yeniden kurulsun mu?", "Evet", "Vazgeç"))
                return;
            Undo.DestroyObjectImmediate(existing.gameObject);
        }

        Undo.SetCurrentGroupName("Create Product Card");
        int undoGroup = Undo.GetCurrentGroup();

        var card = CreateUI(k_CardName, canvas);
        card.anchorMin = card.anchorMax = new Vector2(0.5f, 0f);
        card.pivot = new Vector2(0.5f, 0f);
        card.sizeDelta = new Vector2(540f, 118f);
        card.anchoredPosition = new Vector2(0f, 190f);

        var welcome = canvas.Find("Welcome Screen");
        card.SetSiblingIndex(welcome != null ? welcome.GetSiblingIndex() : canvas.childCount - 1);

        var background = card.gameObject.AddComponent<Image>();
        background.color = k_Cream;
        background.raycastTarget = false;
        var rounded = FindAsset<Sprite>("ActivationButtonOpaque", "t:Sprite");
        if (rounded != null)
        {
            background.sprite = rounded;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 10f;
        }
        card.gameObject.AddComponent<CanvasGroup>();

        var serif = FindAsset<TMP_FontAsset>("PlayfairDisplay", "t:TMP_FontAsset");

        var nameText = CreateText(card, "Name", 30, k_Ink, TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0.5f), new Vector2(0.72f, 1f));
        if (serif != null)
            nameText.font = serif;
        var dimensionsText = CreateText(card, "Dimensions", 20, k_Muted, TextAlignmentOptions.TopLeft,
            new Vector2(0f, 0f), new Vector2(0.72f, 0.5f));
        var priceText = CreateText(card, "Price", 28, k_Brass, TextAlignmentOptions.Right,
            new Vector2(0.72f, 0f), new Vector2(1f, 1f));

        nameText.text = "Victorian Sofa";
        dimensionsText.text = "W 157 × D 66 × H 80 cm";
        priceText.text = "18.900 TL";

        var component = card.gameObject.AddComponent<ProductInfoCard>();
        var so = new SerializedObject(component);
        so.FindProperty("m_MenuManager").objectReferenceValue = menuManager;
        so.FindProperty("m_NameText").objectReferenceValue = nameText;
        so.FindProperty("m_DimensionsText").objectReferenceValue = dimensionsText;
        so.FindProperty("m_PriceText").objectReferenceValue = priceText;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(menuManager.gameObject.scene);
        Selection.activeGameObject = card.gameObject;
        Debug.Log($"[FurnishAR] Product Card kuruldu. {added} prefab'a ProductInfo eklendi.");
    }

    static int AddProductInfoToPrefabs(ObjectSpawner spawner)
    {
        int added = 0;
        foreach (var prefab in spawner.objectPrefabs)
        {
            if (prefab == null || prefab.GetComponent<ProductInfo>() != null)
                continue;

            string path = AssetDatabase.GetAssetPath(prefab);
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var info = root.AddComponent<ProductInfo>();
                var so = new SerializedObject(info);
                if (k_Catalog.TryGetValue(prefab.name, out var entry))
                {
                    so.FindProperty("m_DisplayName").stringValue = entry.name;
                    so.FindProperty("m_Price").intValue = entry.price;
                }
                else
                {
                    so.FindProperty("m_DisplayName").stringValue = prefab.name.Replace("_", " ");
                }
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                added++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return added;
    }

    static RectTransform CreateUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        go.layer = parent.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    static TextMeshProUGUI CreateText(Transform parent, string name, float size, Color color,
        TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax)
    {
        var rect = CreateUI(name, parent);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(24f, 10f);
        rect.offsetMax = new Vector2(-24f, -10f);

        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        return tmp;
    }

    static T FindAsset<T>(string name, string typeFilter) where T : Object
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " " + typeFilter))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null)
                return asset;
        }
        return null;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
