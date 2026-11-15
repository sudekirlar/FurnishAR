using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Applies ARCore light estimation (Environmental HDR) to the scene.
// This is the light estimation / visual coherence part from the course: the virtual
// furniture should get the same light as the real room.
[RequireComponent(typeof(Light))]
public class LightEstimationApplier : MonoBehaviour
{
    [Tooltip("Boş bırakılırsa sahnede otomatik aranır (Main Camera üzerinde).")]
    [SerializeField]
    ARCameraManager m_CameraManager;

    [Header("Uygulananlar")]
    [SerializeField]
    bool m_ApplyMainLight = true;

    [SerializeField]
    bool m_ApplyAmbient = true;

    [SerializeField]
    bool m_AdaptShadows = true;

    [SerializeField]
    bool m_AdaptReflections = true;

    [Header("Kalibrasyon")]
    [Tooltip("Ana ışık şiddeti çarpanı. Modeller genel olarak fazla parlak/karanlık görünürse buradan ayarla.")]
    [SerializeField]
    float m_MainLightMultiplier = 1f;

    [Tooltip("Ortam ışığı (SH) çarpanı. Gölgedeki yüzler fazla karanlıksa artır.")]
    [SerializeField]
    float m_AmbientMultiplier = 1f;

    [Header("Fotometrik uyum")]
    [Tooltip("γ: 1 = ARCore'un ham değerleri, 0 = her ortamda aynı parlaklık. " +
             "Kameranın otomatik pozlaması aradaki farkı büyük ölçüde sakladığı için 0.3–0.5 arası önerilir.")]
    [SerializeField, Range(0f, 1f)]
    float m_DynamicRangeCompression = 0.4f;

    [Tooltip("Sıkıştırmanın referans seviyesi: toplam ışık (ana + 4×ortam) bu değerdeyken ölçek 1'dir.")]
    [SerializeField]
    float m_ReferenceLevel = 1f;

    [Tooltip("Işık renginin ne kadarı uygulanır. 0 = gri (renksiz) ışık, 1 = ARCore'un ham rengi. " +
             "Kameranın beyaz dengesi rengi zaten nötrlediği için düşük tutulur.")]
    [SerializeField, Range(0f, 1f)]
    float m_ColorStrength = 0.3f;

    [Tooltip("Ortam parlaklığı (uygulanan SH DC luminansı) bu değere ulaşınca yansımalar tam şiddette olur.")]
    [SerializeField]
    float m_ReferenceAmbient = 0.2f;

    [Tooltip("Gölge koyuluğu aralığı: ışık tamamen dağınıkken min, tek güçlü kaynaktayken max.")]
    [SerializeField]
    Vector2 m_ShadowStrengthRange = new Vector2(0.25f, 0.8f);

    [Header("Yumuşatma")]
    [Tooltip("Büyüdükçe ışık değişimlere daha hızlı uyar, küçüldükçe daha yumuşak ama gecikmeli olur.")]
    [SerializeField]
    float m_Smoothing = 4f;

    Light m_Light;
    bool m_EstimationEnabled = true;

    bool m_HasTarget;
    bool m_HasCurrent;
    Quaternion m_TargetRotation, m_CurrentRotation;
    Color m_TargetColor, m_CurrentColor;
    SphericalHarmonicsL2 m_TargetAmbient, m_CurrentAmbient;
    bool m_HasDirection, m_HasColor, m_HasAmbient;

    Quaternion m_OriginalRotation;
    Color m_OriginalColor;
    float m_OriginalIntensity;
    float m_OriginalShadowStrength;
    AmbientMode m_OriginalAmbientMode;
    SphericalHarmonicsL2 m_OriginalAmbientProbe;
    float m_OriginalReflectionIntensity;

    public float appliedMainIntensity { get; private set; }

    public float appliedAmbientLuminance { get; private set; }

    public float exposureScale { get; private set; } = 1f;

    // Used by the toggle in the options menu, so I can show the difference with and without it.
    public bool estimationEnabled
    {
        get => m_EstimationEnabled;
        set
        {
            if (m_EstimationEnabled == value)
                return;
            m_EstimationEnabled = value;
            if (value)
                m_HasCurrent = false;
            else
                RestoreOriginal();
        }
    }

    void Awake()
    {
        m_Light = GetComponent<Light>();
        if (m_CameraManager == null)
            m_CameraManager = FindAnyObjectByType<ARCameraManager>();
        if (m_CameraManager == null)
            Debug.LogError("[LightEstimationApplier] ARCameraManager bulunamadı.");

        m_OriginalRotation = transform.rotation;
        m_OriginalColor = m_Light.color;
        m_OriginalIntensity = m_Light.intensity;
        m_OriginalShadowStrength = m_Light.shadowStrength;
        m_OriginalAmbientMode = RenderSettings.ambientMode;
        m_OriginalAmbientProbe = RenderSettings.ambientProbe;
        m_OriginalReflectionIntensity = RenderSettings.reflectionIntensity;
    }

    void OnEnable()
    {
        if (m_CameraManager == null)
            return;

        m_CameraManager.requestedLightEstimation |=
            LightEstimation.MainLightDirection |
            LightEstimation.MainLightIntensity |
            LightEstimation.AmbientSphericalHarmonics;

        m_CameraManager.frameReceived += OnCameraFrame;
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
            m_CameraManager.frameReceived -= OnCameraFrame;
        RestoreOriginal();
    }

    void OnCameraFrame(ARCameraFrameEventArgs args)
    {
        var estimate = args.lightEstimation;

        m_HasDirection = estimate.mainLightDirection.HasValue;
        if (m_HasDirection)
            m_TargetRotation = Quaternion.LookRotation(estimate.mainLightDirection.Value);

        m_HasColor = estimate.mainLightColor.HasValue;
        if (m_HasColor)
            m_TargetColor = estimate.mainLightColor.Value;

        m_HasAmbient = estimate.ambientSphericalHarmonics.HasValue;
        if (m_HasAmbient)
            m_TargetAmbient = estimate.ambientSphericalHarmonics.Value;

        m_HasTarget = m_HasDirection || m_HasColor || m_HasAmbient;
    }

    void Update()
    {
        if (!m_EstimationEnabled || !m_HasTarget)
            return;

        // ARCore values jitter every frame, so I smooth them.
        float t = m_HasCurrent ? 1f - Mathf.Exp(-m_Smoothing * Time.deltaTime) : 1f;
        if (!m_HasCurrent)
        {
            m_CurrentRotation = m_HasDirection ? m_TargetRotation : transform.rotation;
            m_CurrentColor = m_HasColor ? m_TargetColor : m_Light.color * m_Light.intensity;
            m_CurrentAmbient = m_HasAmbient ? m_TargetAmbient : RenderSettings.ambientProbe;
            m_HasCurrent = true;
        }

        if (m_HasDirection)
            m_CurrentRotation = Quaternion.Slerp(m_CurrentRotation, m_TargetRotation, t);
        if (m_HasColor)
            m_CurrentColor = Color.Lerp(m_CurrentColor, m_TargetColor, t);
        if (m_HasAmbient)
            m_CurrentAmbient = m_CurrentAmbient * (1f - t) + m_TargetAmbient * t;

        // My first version used the raw values. In the bright hallway the chair looked white and blue.
        // That is because the phone camera has auto exposure and auto white balance, but my light did not.
        // So I compress the total light level (K^0.4) and use only 30% of the light color.
        Color mainColor = Desaturate(m_CurrentColor, m_ColorStrength);
        SphericalHarmonicsL2 ambient = Desaturate(m_CurrentAmbient, m_ColorStrength);

        float rawMain = Luminance(mainColor);
        float rawAmbient = AmbientLuminance(ambient);
        float level = rawMain + rawAmbient * 4f;
        float scale = 1f;
        if (level > 1e-4f)
        {
            float reference = Mathf.Max(m_ReferenceLevel, 1e-4f);
            scale = reference * Mathf.Pow(level / reference, m_DynamicRangeCompression) / level;
        }
        exposureScale = scale;
        mainColor *= scale;
        ambient *= scale;

        // ARCore does not give the intensity separately, it is inside the color.
        // I split it into color and intensity.
        float mainIntensity = Mathf.Max(mainColor.r, Mathf.Max(mainColor.g, mainColor.b));
        if (m_ApplyMainLight)
        {
            if (m_HasDirection)
                transform.rotation = m_CurrentRotation;
            if (m_HasColor)
            {
                m_Light.color = mainIntensity > 1e-4f ? mainColor / mainIntensity : Color.black;
                m_Light.intensity = mainIntensity * m_MainLightMultiplier;
            }
        }
        appliedMainIntensity = m_Light.intensity;

        float ambientLuminance = AmbientLuminance(ambient) * m_AmbientMultiplier;
        // Spherical harmonics go to the ambient probe, so the dark sides get the room light, not the blue sky.
        if (m_ApplyAmbient && m_HasAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientProbe = ambient * m_AmbientMultiplier;
        }
        appliedAmbientLuminance = ambientLuminance;

        // Shadow darkness comes from the direct / total light ratio.
        // One strong lamp gives dark shadows, light from everywhere gives soft shadows.
        if (m_AdaptShadows && m_HasColor && m_HasAmbient)
        {
            float mainLuminance = Luminance(mainColor);
            float directRatio = mainLuminance / Mathf.Max(mainLuminance + ambientLuminance * 4f, 1e-4f);
            m_Light.shadowStrength = Mathf.Lerp(m_ShadowStrengthRange.x, m_ShadowStrengthRange.y, directRatio);
        }

        // The default reflection is the Unity sky. The wood was still shiny in a dark room,
        // so I scale the reflection with the room brightness.
        if (m_AdaptReflections && m_HasAmbient)
            RenderSettings.reflectionIntensity = Mathf.Clamp01(ambientLuminance / Mathf.Max(m_ReferenceAmbient, 1e-4f));
    }

    void RestoreOriginal()
    {
        if (m_Light == null)
            return;

        transform.rotation = m_OriginalRotation;
        m_Light.color = m_OriginalColor;
        m_Light.intensity = m_OriginalIntensity;
        m_Light.shadowStrength = m_OriginalShadowStrength;
        RenderSettings.ambientMode = m_OriginalAmbientMode;
        RenderSettings.ambientProbe = m_OriginalAmbientProbe;
        RenderSettings.reflectionIntensity = m_OriginalReflectionIntensity;
        m_HasCurrent = false;
    }

    static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

    static Color Desaturate(Color c, float strength)
    {
        float y = Luminance(c);
        return new Color(
            Mathf.LerpUnclamped(y, c.r, strength),
            Mathf.LerpUnclamped(y, c.g, strength),
            Mathf.LerpUnclamped(y, c.b, strength),
            c.a);
    }

    static SphericalHarmonicsL2 Desaturate(SphericalHarmonicsL2 sh, float strength)
    {
        for (int i = 0; i < 9; i++)
        {
            float y = 0.2126f * sh[0, i] + 0.7152f * sh[1, i] + 0.0722f * sh[2, i];
            for (int c = 0; c < 3; c++)
                sh[c, i] = Mathf.LerpUnclamped(y, sh[c, i], strength);
        }
        return sh;
    }

    static float AmbientLuminance(SphericalHarmonicsL2 sh) =>
        0.2126f * sh[0, 0] + 0.7152f * sh[1, 0] + 0.0722f * sh[2, 0];
}
