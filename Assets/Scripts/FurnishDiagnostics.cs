using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Debug overlay. I used it to check depth support, light estimation values and
// the color space on my phone before I started building the features. It is off in the final build.
public class FurnishDiagnostics : MonoBehaviour
{
    [Tooltip("Boş bırakılırsa sahnede otomatik aranır (Main Camera üzerinde).")]
    [SerializeField]
    ARCameraManager m_CameraManager;

    [Tooltip("Boş bırakılırsa sahnede otomatik aranır. Hiç yoksa panel 'bileşen yok' yazar.")]
    [SerializeField]
    AROcclusionManager m_OcclusionManager;

    [Tooltip("Boş bırakılırsa sahnede otomatik aranır (Directional Light üzerinde).")]
    [SerializeField]
    LightEstimationApplier m_LightApplier;

    [SerializeField]
    int m_FontSize = 30;

    int m_CameraFrames;
    int m_DepthFrames;
    bool m_HasLight;
    ARLightEstimationData m_Light;
    float m_SmoothedDeltaTime = 1f / 30f;

    readonly StringBuilder m_Text = new StringBuilder(1024);
    GUIStyle m_Style;

    void Awake()
    {
        if (m_CameraManager == null)
            m_CameraManager = FindAnyObjectByType<ARCameraManager>();
        if (m_OcclusionManager == null)
            m_OcclusionManager = FindAnyObjectByType<AROcclusionManager>();
        if (m_LightApplier == null)
            m_LightApplier = FindAnyObjectByType<LightEstimationApplier>();
    }

    void OnEnable()
    {
        if (m_CameraManager != null)
            m_CameraManager.frameReceived += OnCameraFrame;
        if (m_OcclusionManager != null)
            m_OcclusionManager.frameReceived += OnOcclusionFrame;
    }

    void OnDisable()
    {
        if (m_CameraManager != null)
            m_CameraManager.frameReceived -= OnCameraFrame;
        if (m_OcclusionManager != null)
            m_OcclusionManager.frameReceived -= OnOcclusionFrame;
    }

    void OnCameraFrame(ARCameraFrameEventArgs args)
    {
        m_CameraFrames++;
        m_Light = args.lightEstimation;
        m_HasLight = true;
    }

    void OnOcclusionFrame(AROcclusionFrameEventArgs args)
    {
        m_DepthFrames++;
    }

    void Update()
    {
        m_SmoothedDeltaTime = Mathf.Lerp(m_SmoothedDeltaTime, Time.unscaledDeltaTime, 0.05f);
    }

    void OnGUI()
    {
        if (m_Style == null)
        {
            m_Style = new GUIStyle(GUI.skin.box)
            {
                fontSize = m_FontSize,
                alignment = TextAnchor.UpperLeft,
                wordWrap = true,
                padding = new RectOffset(16, 16, 12, 12)
            };
            m_Style.normal.textColor = Color.white;
        }

        BuildText();
        var rect = new Rect(20f, Screen.height * 0.10f, Screen.width - 40f, Screen.height * 0.56f);
        GUI.Box(rect, m_Text.ToString(), m_Style);
    }

    void BuildText()
    {
        m_Text.Clear();

        m_Text.AppendLine("[ORTAM]");
        m_Text.AppendLine($"Grafik API: {SystemInfo.graphicsDeviceType}");
        m_Text.AppendLine($"Color space: {QualitySettings.activeColorSpace}");
        m_Text.AppendLine($"Oturum: {ARSession.state}  ({ARSession.notTrackingReason})");
        m_Text.AppendLine($"FPS: {1f / m_SmoothedDeltaTime:0}   Kamera karesi: {m_CameraFrames}");

        m_Text.AppendLine();
        m_Text.AppendLine("[OCCLUSION]");
        if (m_OcclusionManager == null)
        {
            m_Text.AppendLine("AROcclusionManager bileşeni yok");
        }
        else if (m_OcclusionManager.descriptor == null)
        {
            m_Text.AppendLine("Subsystem yok (loader yüklemedi)");
        }
        else
        {
            m_Text.AppendLine($"Env. depth destek: {m_OcclusionManager.descriptor.environmentDepthImageSupported}");
            m_Text.AppendLine($"İstenen/çalışan mod: {m_OcclusionManager.requestedEnvironmentDepthMode} / {m_OcclusionManager.currentEnvironmentDepthMode}");
            m_Text.AppendLine($"Tercih: {m_OcclusionManager.currentOcclusionPreferenceMode}");
            m_Text.AppendLine($"Depth karesi: {m_DepthFrames}");
        }

        m_Text.AppendLine();
        m_Text.AppendLine("[IŞIK TAHMİNİ]");
        if (m_CameraManager == null)
        {
            m_Text.AppendLine("ARCameraManager bulunamadı");
            return;
        }

        m_Text.AppendLine($"İstenen: {m_CameraManager.requestedLightEstimation}");
        m_Text.AppendLine($"Çalışan: {m_CameraManager.currentLightEstimation}");

        if (!m_HasLight)
        {
            m_Text.AppendLine("Henüz kare gelmedi");
            return;
        }

        m_Text.AppendLine($"Ort. parlaklık: {F(m_Light.averageBrightness)}   Renk sıcaklığı: {F(m_Light.averageColorTemperature, "0")}");
        m_Text.AppendLine($"Renk düzeltme: {C(m_Light.colorCorrection)}");
        m_Text.AppendLine($"Ana ışık yönü: {V(m_Light.mainLightDirection)}");
        m_Text.AppendLine($"Ana ışık rengi: {C(m_Light.mainLightColor)}");
        m_Text.AppendLine($"Ana ışık parlaklığı: {F(m_Light.averageMainLightBrightness)}   Lümen: {F(m_Light.mainLightIntensityLumens, "0")}");

        if (m_Light.ambientSphericalHarmonics.HasValue)
        {
            var sh = m_Light.ambientSphericalHarmonics.Value;
            m_Text.AppendLine($"SH: var  (DC r/g/b: {sh[0, 0]:0.00} / {sh[1, 0]:0.00} / {sh[2, 0]:0.00})");
        }
        else
        {
            m_Text.AppendLine("SH: —");
        }

        if (m_LightApplier != null)
        {
            m_Text.AppendLine();
            m_Text.AppendLine(m_LightApplier.estimationEnabled ? "[UYGULANAN]" : "[UYGULANAN] kapalı (sabit ışık)");
            m_Text.AppendLine($"Pozlama ölçeği: ×{m_LightApplier.exposureScale:0.00}");
            m_Text.AppendLine($"Ana ışık: {m_LightApplier.appliedMainIntensity:0.00}   Ortam: {m_LightApplier.appliedAmbientLuminance:0.00}");
        }
    }

    static string F(float? value, string format = "0.00") =>
        value.HasValue ? value.Value.ToString(format) : "—";

    static string V(Vector3? value) =>
        value.HasValue ? $"({value.Value.x:0.00}, {value.Value.y:0.00}, {value.Value.z:0.00})" : "—";

    static string C(Color? value) =>
        value.HasValue ? $"({value.Value.r:0.00}, {value.Value.g:0.00}, {value.Value.b:0.00})" : "—";
}
