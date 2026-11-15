using TMPro;
using UnityEngine;
using UnityEngine.XR.Templates.AR;

// Info card for the selected item. It reads the same focus as the delete button,
// so the card and the delete button always show up together.
[RequireComponent(typeof(CanvasGroup))]
public class ProductInfoCard : MonoBehaviour
{
    [SerializeField]
    ARTemplateMenuManager m_MenuManager;

    [SerializeField]
    TMP_Text m_NameText;

    [SerializeField]
    TMP_Text m_DimensionsText;

    [SerializeField]
    TMP_Text m_PriceText;

    [Tooltip("Belirme/kaybolma hızı. Büyüdükçe daha hızlı.")]
    [SerializeField]
    float m_FadeSpeed = 10f;

    static readonly int k_ShowMenuParam = Animator.StringToHash("Show");

    CanvasGroup m_Group;
    PlacementController m_Placement;
    ProductInfo m_Current;

    void Awake()
    {
        m_Group = GetComponent<CanvasGroup>();
        m_Group.alpha = 0f;
        m_Group.blocksRaycasts = false;
        m_Group.interactable = false;

        if (m_MenuManager == null)
            m_MenuManager = FindAnyObjectByType<ARTemplateMenuManager>(FindObjectsInactive.Include);
        m_Placement = FindAnyObjectByType<PlacementController>(FindObjectsInactive.Include);
    }

    void Update()
    {
        ProductInfo target = null;

        bool blocked = (m_Placement != null && m_Placement.isPlacing) || IsMenuOpen();
        var group = m_MenuManager != null ? m_MenuManager.interactionGroup : null;
        var focus = group != null ? group.focusInteractable : null;
        if (!blocked && focus != null)
            target = focus.transform.GetComponentInParent<ProductInfo>();

        if (target != null && target != m_Current)
        {
            m_Current = target;
            m_NameText.text = target.displayName;
            m_DimensionsText.text = target.FormatDimensions();
            m_PriceText.text = target.FormatPrice();
        }

        float goal = target != null ? 1f : 0f;
        m_Group.alpha = Mathf.Lerp(m_Group.alpha, goal, 1f - Mathf.Exp(-m_FadeSpeed * Time.unscaledDeltaTime));
    }

    bool IsMenuOpen()
    {
        var animator = m_MenuManager != null ? m_MenuManager.objectMenuAnimator : null;
        return animator != null
            && animator.isActiveAndEnabled
            && animator.runtimeAnimatorController != null
            && animator.GetBool(k_ShowMenuParam);
    }
}
