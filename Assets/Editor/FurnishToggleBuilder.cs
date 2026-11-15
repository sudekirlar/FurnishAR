using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Templates.AR;

// Editor tool that copies the Visualize Surfaces row of the template and turns it into my two toggles.
public static class FurnishToggleBuilder
{
    const string k_SourceRow = "Debug Plane Toggle";
    const string k_OcclusionRow = "Occlusion Toggle";
    const string k_LightRow = "Light Estimation Toggle";
    const float k_RowHeight = 64f;

    [MenuItem("Tools/FurnishAR/Add MR Feature Toggles")]
    static void Build()
    {
        var menuManager = Object.FindAnyObjectByType<ARTemplateMenuManager>(FindObjectsInactive.Include);
        var modal = menuManager != null ? menuManager.modalMenu : null;
        var source = modal != null ? modal.transform.Find(k_SourceRow) : null;
        if (source == null)
        {
            EditorUtility.DisplayDialog("FurnishAR", "Options Modal veya içindeki 'Debug Plane Toggle' satırı bulunamadı.", "Tamam");
            return;
        }

        if (modal.transform.Find(k_OcclusionRow) != null || modal.transform.Find(k_LightRow) != null)
        {
            EditorUtility.DisplayDialog("FurnishAR", "Anahtarlar zaten eklenmiş. Yeniden kurmak için önce iki satırı silip menünün yüksekliğini eski hâline getir (ya da Ctrl+Z).", "Tamam");
            return;
        }

        Undo.SetCurrentGroupName("Add MR Feature Toggles");
        int undoGroup = Undo.GetCurrentGroup();

        var toggles = menuManager.GetComponent<MRFeatureToggles>();
        if (toggles == null)
            toggles = Undo.AddComponent<MRFeatureToggles>(menuManager.gameObject);

        float lowest = 0f;
        foreach (RectTransform child in modal.transform)
            lowest = Mathf.Min(lowest, child.anchoredPosition.y - child.sizeDelta.y);

        var occlusionSlider = CreateRow(source, k_OcclusionRow, "Occlusion", lowest, toggles.ToggleOcclusion);
        var lightSlider = CreateRow(source, k_LightRow, "Light Estimation", lowest - k_RowHeight, toggles.ToggleLightEstimation);

        var modalRect = (RectTransform)modal.transform;
        Undo.RecordObject(modalRect, "Resize Options Modal");
        modalRect.sizeDelta += new Vector2(0f, 2f * k_RowHeight);
        modalRect.anchoredPosition -= new Vector2(0f, k_RowHeight * (1f - modalRect.pivot.y) * 2f);

        var so = new SerializedObject(toggles);
        so.FindProperty("m_OcclusionSlider").objectReferenceValue = occlusionSlider;
        so.FindProperty("m_LightSlider").objectReferenceValue = lightSlider;
        so.ApplyModifiedProperties();

        Undo.CollapseUndoOperations(undoGroup);
        EditorSceneManager.MarkSceneDirty(menuManager.gameObject.scene);
        Debug.Log("[FurnishAR] Options menüsüne Occlusion ve Light Estimation anahtarları eklendi.");
    }

    static Slider CreateRow(Transform source, string name, string label, float y, UnityAction onClick)
    {
        var row = Object.Instantiate(source.gameObject, source.parent);
        Undo.RegisterCreatedObjectUndo(row, "Create " + name);
        row.name = name;

        var rect = (RectTransform)row.transform;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);

        var text = row.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
            text.text = label;

        var button = row.GetComponent<Button>();
        while (button.onClick.GetPersistentEventCount() > 0)
            UnityEventTools.RemovePersistentListener(button.onClick, 0);
        UnityEventTools.AddPersistentListener(button.onClick, onClick);

        return row.GetComponentInChildren<Slider>(true);
    }
}
