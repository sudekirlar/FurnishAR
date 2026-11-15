using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Templates.AR;

/// <summary>
/// Açılış ekranı: krem bir perde üzerinde marka, alt başlık ve "Start designing" butonu.
///
/// Animasyon:
/// - Giriş: öğeler sırayla (stagger) belirir; her biri saydamdan opağa geçerken birkaç piksel
///   aşağıdan yerine kayar. Easing "ease-out cubic": hızlı başlar, yumuşak durur.
/// - Çıkış: butona basılınca bütün perde yavaşça saydamlaşır ve arkadaki kamera görüntüsü ortaya
///   çıkar; sonra şablonun onboarding'i (GoalManager.StartCoaching) başlar.
///
/// GoalManager'ın "Greeting Prompt" alanı bu objeyi gösterir; StartCoaching onu kendisi kapatır.
/// Bu hiyerarşiyi elle kurmak yerine Tools > FurnishAR > Create Welcome Screen kullanılır.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class WelcomeScreen : MonoBehaviour
{
    [SerializeField]
    GoalManager m_GoalManager;

    [SerializeField]
    Button m_StartButton;

    [Tooltip("Sırayla belirecek öğeler (her birinde CanvasGroup olmalı).")]
    [SerializeField]
    CanvasGroup[] m_Elements;

    [Header("Giriş animasyonu")]
    [SerializeField]
    float m_InitialDelay = 0.3f;

    [Tooltip("Bir öğenin başlaması ile sonrakinin başlaması arasındaki süre (s).")]
    [SerializeField]
    float m_Stagger = 0.14f;

    [SerializeField]
    float m_FadeInDuration = 0.8f;

    [Tooltip("Öğelerin yerine kayarken aldığı yol (UI birimi).")]
    [SerializeField]
    float m_RiseDistance = 24f;

    [Header("Çıkış animasyonu")]
    [SerializeField]
    float m_FadeOutDuration = 0.5f;

    CanvasGroup m_Root;
    RectTransform[] m_ElementRects;
    Vector2[] m_BasePositions;
    bool m_Leaving;

    void Awake()
    {
        m_Root = GetComponent<CanvasGroup>();

        if (m_GoalManager == null)
            m_GoalManager = FindAnyObjectByType<GoalManager>(FindObjectsInactive.Include);

        m_ElementRects = new RectTransform[m_Elements.Length];
        m_BasePositions = new Vector2[m_Elements.Length];
        for (int i = 0; i < m_Elements.Length; i++)
        {
            m_ElementRects[i] = (RectTransform)m_Elements[i].transform;
            m_BasePositions[i] = m_ElementRects[i].anchoredPosition;
        }
    }

    void OnEnable()
    {
        m_Leaving = false;
        m_Root.alpha = 1f;
        m_Root.interactable = true;
        m_Root.blocksRaycasts = true;

        if (m_StartButton != null)
            m_StartButton.onClick.AddListener(OnStartPressed);

        StartCoroutine(PlayIntro());
    }

    void OnDisable()
    {
        StopAllCoroutines();
        if (m_StartButton != null)
            m_StartButton.onClick.RemoveListener(OnStartPressed);

        // Bir sonraki açılışta animasyon temiz başlasın.
        for (int i = 0; i < m_Elements.Length; i++)
        {
            m_Elements[i].alpha = 1f;
            m_ElementRects[i].anchoredPosition = m_BasePositions[i];
        }
    }

    IEnumerator PlayIntro()
    {
        for (int i = 0; i < m_Elements.Length; i++)
            ApplyIntro(i, 0f);

        float start = Time.unscaledTime;
        float total = m_InitialDelay + m_Stagger * (m_Elements.Length - 1) + m_FadeInDuration;

        while (true)
        {
            float elapsed = Time.unscaledTime - start;
            for (int i = 0; i < m_Elements.Length; i++)
            {
                float t = (elapsed - m_InitialDelay - m_Stagger * i) / m_FadeInDuration;
                ApplyIntro(i, EaseOutCubic(Mathf.Clamp01(t)));
            }

            if (elapsed >= total)
                yield break;
            yield return null;
        }
    }

    void ApplyIntro(int index, float progress)
    {
        m_Elements[index].alpha = progress;
        m_ElementRects[index].anchoredPosition =
            m_BasePositions[index] + Vector2.down * (m_RiseDistance * (1f - progress));
    }

    void OnStartPressed()
    {
        if (m_Leaving)
            return;
        m_Leaving = true;
        StartCoroutine(Leave());
    }

    IEnumerator Leave()
    {
        m_Root.interactable = false;

        float start = Time.unscaledTime;
        while (true)
        {
            float t = Mathf.Clamp01((Time.unscaledTime - start) / m_FadeOutDuration);
            m_Root.alpha = 1f - EaseInOutCubic(t);
            if (t >= 1f)
                break;
            yield return null;
        }

        m_Root.blocksRaycasts = false;

        if (m_GoalManager != null)
            m_GoalManager.StartCoaching(); // bu objeyi de kapatır
        else
            gameObject.SetActive(false);
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);

    static float EaseInOutCubic(float t) =>
        t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
}
