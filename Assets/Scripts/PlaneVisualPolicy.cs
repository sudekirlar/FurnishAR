using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Templates.AR;

// Cleans up the plane visuals. ARCore merges planes over time and the old ones
// were overlapping, so I hide merged and small planes.
[RequireComponent(typeof(ARPlane))]
public class PlaneVisualPolicy : MonoBehaviour
{
    public static bool ShowPlanes = true;

    [Tooltip("Bu alandan (m²) küçük plane'lerin görseli gösterilmez.")]
    [SerializeField]
    float m_MinVisibleArea = 0.25f;

    ARPlane m_Plane;
    ARPlaneMeshVisualizer m_MeshVisualizer;
    ARPlaneMeshVisualizerFader m_Fader;

    int m_LastVisualState = -1;

    void Awake()
    {
        m_Plane = GetComponent<ARPlane>();
        m_MeshVisualizer = GetComponent<ARPlaneMeshVisualizer>();
    }

    void Update()
    {
        if (m_Fader == null)
            m_Fader = GetComponent<ARPlaneMeshVisualizerFader>();

        if (m_Plane.subsumedBy != null)
        {
            if (m_MeshVisualizer != null && m_MeshVisualizer.enabled)
                m_MeshVisualizer.enabled = false;
            return;
        }

        if (m_Fader == null)
            return;

        float area = m_Plane.size.x * m_Plane.size.y;
        bool isUsableFloor = m_Plane.alignment == PlaneAlignment.HorizontalUp && area >= m_MinVisibleArea;
        int desiredState = (ShowPlanes && isUsableFloor) ? 1 : 0;

        if (desiredState != m_LastVisualState)
        {
            m_Fader.visualizeSurfaces = desiredState == 1;
            m_LastVisualState = desiredState;
        }
    }
}
