using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.AR;

// Editor tool that builds the welcome screen and connects it to GoalManager
// as the greeting prompt. Colors are cream, dark brown and brass for the vintage look.
public static class FurnishWelcomeScreenBuilder
{
    static readonly Color k_Cream = Hex("F4EFE6");
    static readonly Color k_Ink = Hex("2F2620");
    static readonly Color k_Brass = Hex("A8834A");
    static readonly Color k_Muted = Hex("7A6C5F");

    const string k_RootName = "Welcome Screen";

    [MenuItem("Tools/FurnishAR/Create Welcome Screen")]
    static void Create()
    {
        var goalManager = Object.FindAnyObjectByType<GoalManager>(FindObjectsInactive.Include);
        if (goalManager == null)
        {
            EditorUtility.DisplayDialog("FurnishAR", "Sahnede GoalManager bulunamadı.", "Tamam");
            return;
        }

        var canvas = goalManager.transform;
        var existing = canvas.Find(k_RootName);
        if (existing != null &&
            !EditorUtility.DisplayDialog("FurnishAR", "Zaten bir Welcome Screen var. Silinip yeniden kurulsun mu?", "Evet", "Vazgeç"))
            return;
        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);

        Undo.SetCurrentGroupName("Create Welcome Screen");
        int undoGroup = Undo.GetCurrentGroup();

        var root = CreateUI(k_RootName, canvas);
        Stretch(root);
        root.SetAsLastSibling();
        var background = root.gameObject.AddComponent<Image>();
        background.color = k_Cream;
        root.gameObject.AddComponent<CanvasGroup>();

        var label = CreateText(root, "Collection Label", "VINTAGE COLLECTION", 22, k_Brass, 165f);
        label.characterSpacing = 18f;

        var title = CreateText(root, "Title", "FurnishAR", 78, k_Ink, 95f);
        title.fontStyle = FontStyles.Normal;

        var divider = CreateUI("Divider", root);
        divider.sizeDelta = new Vector2(72f, 2f);
        divider.anchoredPosition = new Vector2(0f, 32f);
        divider.gameObject.AddComponent<Image>().color = k_Brass;
        divider.gameObject.AddComponent<CanvasGroup>();

        var tagline = CreateText(root, "Tagline", "Style your room with timeless pieces.", 26, k_Muted, -22f);

        var button = CreateUI("Start Button", root);
        button.anchorMin = button.anchorMax = new Vector2(0.5f, 0.13f);
        button.sizeDelta = new Vector2(340f, 84f);
        var buttonImage = button.gameObject.AddComponent<Image>();
        buttonImage.color = k_Ink;
        var rounded = FindSprite("ActivationButtonOpaque");
        if (rounded != null)
        {
            buttonImage.sprite = rounded;
            buttonImage.type = Image.Type.Sliced;
            buttonImage.pixelsPerUnitMultiplier = 10f;
        }
        var startButton = button.gameObject.AddComponent<Button>();
        startButton.targetGraphic = buttonImage;
        button.gameObject.AddComponent<CanvasGroup>();

        var buttonLabel = CreateText(button, "Label", "Start designing", 28, k_Cream, 0f, addCanvasGroup: false);
        Stretch((RectTransform)buttonLabel.transform);

        var credit = CreateText(root, "Credit", "3D models: Poly Haven · CC0", 16, k_Muted, 0f);
        var creditRect = (RectTransform)credit.transform;
        creditRect.anchorMin = creditRect.anchorMax = new Vector2(0.5f, 0.045f);
        creditRect.anchoredPosition = Vector2.zero;

        var welcome = root.gameObject.AddComponent<WelcomeScreen>();
        var so = new SerializedObject(welcome);
        so.FindProperty("m_GoalManager").objectReferenceValue = goalManager;
        so.FindProperty("m_StartButton").objectReferenceValue = startButton;
        var elements = so.FindProperty("m_Elements");
        var order = new Component[] { label, title, divider, tagline, button, credit };
        elements.arraySize = order.Length;
        for (int i = 0; i < order.Length; i++)
            elements.GetArrayElementAtIndex(i).objectReferenceValue = order[i].GetComponent<CanvasGroup>();
        so.ApplyModifiedPropertiesWithoutUndo();

        var gm = new SerializedObject(goalManager);
        var greeting = gm.FindProperty("m_GreetingPrompt");
        var oldGreeting = greeting.objectReferenceValue as GameObject;
        greeting.objectReferenceValue = root.gameObject;
        gm.ApplyModifiedProperties();

        if (oldGreeting != null && oldGreeting != root.gameObject)
        {
            Undo.RecordObject(oldGreeting, "Disable old greeting");
            oldGreeting.SetActive(false);
        }

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(goalManager.gameObject.scene);
        Selection.activeGameObject = root.gameObject;
        Debug.Log("[FurnishAR] Welcome Screen kuruldu. Eski karşılama ekranı kapatıldı, GoalManager bağlandı.");
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

    static TextMeshProUGUI CreateText(Transform parent, string name, string text, float size, Color color,
        float y, bool addCanvasGroup = true)
    {
        var rect = CreateUI(name, parent);
        rect.sizeDelta = new Vector2(560f, size * 1.6f);
        rect.anchoredPosition = new Vector2(0f, y);

        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;

        if (addCanvasGroup)
            rect.gameObject.AddComponent<CanvasGroup>();
        return tmp;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static Sprite FindSprite(string name)
    {
        foreach (var guid in AssetDatabase.FindAssets(name + " t:Sprite"))
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid));
            if (sprite != null && sprite.name == name)
                return sprite;
        }
        return null;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        return c;
    }
}
