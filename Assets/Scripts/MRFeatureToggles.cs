using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Toggles in the options menu. This is not a real user feature, I use it in the demo
// to show the scene with and without occlusion and light estimation.
public class MRFeatureToggles : MonoBehaviour
{
    [Tooltip("Boş bırakılırsa otomatik bulunur.")]
    [SerializeField]
    AROcclusionManager m_OcclusionManager;

    [Tooltip("Boş bırakılırsa otomatik bulunur.")]
    [SerializeField]
    LightEstimationApplier m_LightApplier;

    [SerializeField]
    Slider m_OcclusionSlider;

    [SerializeField]
    Slider m_LightSlider;

    bool m_OcclusionOn = true;
    bool m_LightOn = true;
    EnvironmentDepthMode m_OriginalDepthMode = EnvironmentDepthMode.Fastest;

    void Awake()
    {
        if (m_OcclusionManager == null)
            m_OcclusionManager = FindAnyObjectByType<AROcclusionManager>(FindObjectsInactive.Include);
        if (m_LightApplier == null)
            m_LightApplier = FindAnyObjectByType<LightEstimationApplier>(FindObjectsInactive.Include);

        if (m_OcclusionManager != null && m_OcclusionManager.requestedEnvironmentDepthMode != EnvironmentDepthMode.Disabled)
            m_OriginalDepthMode = m_OcclusionManager.requestedEnvironmentDepthMode;
    }

    void Start() => Apply();

    public void ToggleOcclusion()
    {
        m_OcclusionOn = !m_OcclusionOn;
        Apply();
    }

    public void ToggleLightEstimation()
    {
        m_LightOn = !m_LightOn;
        Apply();
    }

    void Apply()
    {
        if (m_OcclusionManager != null)
            m_OcclusionManager.requestedEnvironmentDepthMode = m_OcclusionOn ? m_OriginalDepthMode : EnvironmentDepthMode.Disabled;

        if (m_LightApplier != null)
            m_LightApplier.estimationEnabled = m_LightOn;

        if (m_OcclusionSlider != null)
            m_OcclusionSlider.value = m_OcclusionOn ? 1f : 0f;
        if (m_LightSlider != null)
            m_LightSlider.value = m_LightOn ? 1f : 0f;
    }
}
