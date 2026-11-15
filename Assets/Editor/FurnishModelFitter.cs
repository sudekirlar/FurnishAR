using UnityEditor;
using UnityEngine;

// Editor tool for the furniture prefabs.
// The oil lamp got a 1x1x1 box collider because its mesh is in a child object, so I take the bounds of all child meshes.
// It centers the model on X/Z so it rotates around its middle, and puts the bottom at y = 0.
public static class FurnishModelFitter
{
    // Fitts' law from the lectures: a bigger target is faster to hit.
    // Small items like the candlestick get a touch area of at least 25 cm.
    const float k_SmallObjectMinFootprint = 0.25f;

    [MenuItem("Tools/FurnishAR/Fit Model")]
    static void FitModel() => Fit(0f);

    [MenuItem("Tools/FurnishAR/Fit Model (Small)")]
    static void FitSmallModel() => Fit(k_SmallObjectMinFootprint);

    [MenuItem("Tools/FurnishAR/Fit Model", true)]
    [MenuItem("Tools/FurnishAR/Fit Model (Small)", true)]
    static bool Validate() => Selection.activeTransform != null && Selection.activeTransform.parent != null;

    static void Fit(float minFootprint)
    {
        var model = Selection.activeTransform;
        var parent = model.parent;

        if (!TryGetMeshBounds(model, parent, out Bounds boundsInParent))
        {
            EditorUtility.DisplayDialog("FurnishAR", $"'{model.name}' altında mesh bulunamadı.", "Tamam");
            return;
        }

        Undo.RecordObject(model, "Fit Model");
        var offset = new Vector3(boundsInParent.center.x, boundsInParent.min.y, boundsInParent.center.z);
        model.localPosition -= offset;

        TryGetMeshBounds(model, model, out Bounds boundsInModel);

        var size = boundsInModel.size;
        size.x = Mathf.Max(size.x, minFootprint);
        size.z = Mathf.Max(size.z, minFootprint);

        var collider = model.GetComponent<BoxCollider>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider>(model.gameObject);
        else
            Undo.RecordObject(collider, "Fit Model");

        collider.center = boundsInModel.center;
        collider.size = size;

        Debug.Log($"[FurnishAR] {model.name}: boyut {Format(boundsInModel.size)} m, " +
                  $"collider {Format(size)} m, kaydırma {Format(-offset)} m");
    }

    static bool TryGetMeshBounds(Transform root, Transform space, out Bounds result)
    {
        result = default;
        bool hasAny = false;

        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            var b = filter.sharedMesh.bounds;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);

                var point = space.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!hasAny)
                {
                    result = new Bounds(point, Vector3.zero);
                    hasAny = true;
                }
                else
                {
                    result.Encapsulate(point);
                }
            }
        }

        return hasAny;
    }

    static string Format(Vector3 v) => $"({v.x:0.000}, {v.y:0.000}, {v.z:0.000})";
}
