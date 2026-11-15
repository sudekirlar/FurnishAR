using UnityEngine;

// Name and price for the info card. The size is calculated from the meshes,
// so I don't need to type it and it stays correct if the model changes.
public class ProductInfo : MonoBehaviour
{
    [SerializeField]
    string m_DisplayName = "Product";

    [Tooltip("Kurgusal fiyat (TL).")]
    [SerializeField]
    int m_Price;

    Vector3 m_SizeMeters;
    bool m_HasSize;

    public string displayName => m_DisplayName;
    public int price => m_Price;

    public Vector3 sizeMeters
    {
        get
        {
            if (!m_HasSize)
            {
                m_SizeMeters = ComputeLocalSize();
                m_HasSize = true;
            }
            return m_SizeMeters;
        }
    }

    public string FormatDimensions()
    {
        var cm = sizeMeters * 100f;
        return $"W {Mathf.RoundToInt(cm.x)} × D {Mathf.RoundToInt(cm.z)} × H {Mathf.RoundToInt(cm.y)} cm";
    }

    // I add the thousands separator by hand so it does not depend on culture data on the phone.
    public string FormatPrice()
    {
        string digits = Mathf.Abs(m_Price).ToString();
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
                result.Append('.');
            result.Append(digits[i]);
        }
        return result + " TL";
    }

    Vector3 ComputeLocalSize()
    {
        bool hasAny = false;
        var bounds = new Bounds();
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
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
                var point = transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
                if (!hasAny)
                {
                    bounds = new Bounds(point, Vector3.zero);
                    hasAny = true;
                }
                else
                {
                    bounds.Encapsulate(point);
                }
            }
        }
        return hasAny ? bounds.size : Vector3.zero;
    }
}
