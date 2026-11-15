using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.ARStarterAssets;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using UnityEngine.XR.Templates.AR;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;

// Placement mode for the catalog.
// I added this because the template spawned a new object on every tap. When I started to rotate
// a chair with two fingers, the first finger counted as a tap and a second chair appeared.
// Now an item can be placed only after I pick it from the menu, and only once.
public class PlacementController : MonoBehaviour
{
    [Header("Referanslar (boşsa otomatik bulunur)")]
    [SerializeField]
    ObjectSpawner m_ObjectSpawner;

    [SerializeField]
    ARTemplateMenuManager m_MenuManager;

    [SerializeField]
    PlacementReticle m_Reticle;

    [Header("Şablon")]
    [Tooltip("Şablonun 'boş yere dokununca obje yarat' bileşenini kapatır. Yerleştirmeyi artık bu script yapıyor.")]
    [SerializeField]
    bool m_DisableTemplateSpawnTrigger = true;

    static readonly int k_ShowMenuParam = Animator.StringToHash("Show");
    const int k_IgnoreRaycastLayer = 2;

    Camera m_Camera;
    int m_PlacingIndex = -1;
    bool m_BeginRequested;

    GameObject m_Preview;
    GameObject m_PreviewHolder;

    readonly List<Button> m_ItemButtons = new List<Button>();
    Button m_CancelButton;

    int m_TrackedTouchId = -1;
    bool m_TouchInvalid;

    PointerEventData m_PointerData;
    readonly List<RaycastResult> m_UIHits = new List<RaycastResult>();

    public bool isPlacing => m_PlacingIndex >= 0;

    void Awake()
    {
        m_Camera = Camera.main;

        if (m_ObjectSpawner == null)
            m_ObjectSpawner = FindAnyObjectByType<ObjectSpawner>();
        if (m_MenuManager == null)
            m_MenuManager = FindAnyObjectByType<ARTemplateMenuManager>();
        if (m_Reticle == null)
            m_Reticle = FindAnyObjectByType<PlacementReticle>();

        if (m_ObjectSpawner == null || m_MenuManager == null || m_Reticle == null)
            Debug.LogError("[PlacementController] ObjectSpawner, ARTemplateMenuManager veya PlacementReticle bulunamadı.");

        // Placement is handled here now, so I turn off the template spawner.
        if (m_DisableTemplateSpawnTrigger)
        {
            var triggers = FindObjectsByType<ARInteractorSpawnTrigger>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var trigger in triggers)
                trigger.enabled = false;
        }
    }

    void OnEnable()
    {
        EnhancedTouchSupport.Enable();
        HookMenuButtons();
    }

    void OnDisable()
    {
        UnhookMenuButtons();
        EndPlacement();
        EnhancedTouchSupport.Disable();
    }

    // I connect the catalog buttons from code, so I don't have to edit every button in the inspector.
    void HookMenuButtons()
    {
        if (m_MenuManager == null || m_MenuManager.objectMenu == null)
            return;

        m_CancelButton = m_MenuManager.cancelButton;
        foreach (var button in m_MenuManager.objectMenu.GetComponentsInChildren<Button>(true))
        {
            if (button == m_CancelButton)
                continue;
            button.onClick.AddListener(RequestBeginPlacement);
            m_ItemButtons.Add(button);
        }

        if (m_CancelButton != null)
            m_CancelButton.onClick.AddListener(EndPlacement);
    }

    void UnhookMenuButtons()
    {
        foreach (var button in m_ItemButtons)
        {
            if (button != null)
                button.onClick.RemoveListener(RequestBeginPlacement);
        }
        m_ItemButtons.Clear();

        if (m_CancelButton != null)
            m_CancelButton.onClick.RemoveListener(EndPlacement);
    }

    void RequestBeginPlacement() => m_BeginRequested = true;

    void Update()
    {
        if (m_ObjectSpawner == null || m_Reticle == null)
            return;

        if (m_BeginRequested)
        {
            m_BeginRequested = false;
            BeginPlacement(m_ObjectSpawner.spawnOptionIndex);
        }

        bool menuOpen = IsMenuOpen();
        int placedCount = m_ObjectSpawner.transform.childCount;

        // Planes are shown only when they help: empty scene, open menu or while placing.
        // Before this, planes were hidden after the first item and placing the second one was hard.
        PlaneVisualPolicy.ShowPlanes = isPlacing || menuOpen || placedCount == 0;
        m_Reticle.visible = isPlacing && !menuOpen;

        if (!isPlacing || menuOpen)
        {
            ResetTouch();
            UpdatePreview(menuOpen);
            return;
        }

        if (UpdatePlacementTouch(out Vector2 releasePosition))
            TryPlace(releasePosition);

        UpdatePreview(menuOpen);
    }

    bool IsMenuOpen()
    {
        var animator = m_MenuManager != null ? m_MenuManager.objectMenuAnimator : null;
        return animator != null
            && animator.isActiveAndEnabled
            && animator.runtimeAnimatorController != null
            && animator.GetBool(k_ShowMenuParam);
    }

    void BeginPlacement(int index)
    {
        if (m_ObjectSpawner.isSpawnOptionRandomized)
            return;

        m_PlacingIndex = index;
        CreatePreview(index);
        ResetTouch();
    }

    void EndPlacement()
    {
        m_PlacingIndex = -1;
        DestroyPreview();
        ResetTouch();
    }

    void TryPlace(Vector2 releasePosition)
    {
        // On a quick tap the smoothed reticle was not at the finger yet, so the item landed a few cm behind.
        // That is why I snap to the release point first.
        if (!m_Reticle.SnapToScreenPoint(releasePosition))
            return;

        m_ObjectSpawner.spawnOptionIndex = m_PlacingIndex;
        if (m_ObjectSpawner.TrySpawnObject(m_Reticle.surfacePosition, m_Reticle.surfaceNormal))
            EndPlacement();
    }

    void CreatePreview(int index)
    {
        DestroyPreview();

        var prefabs = m_ObjectSpawner.objectPrefabs;
        if (index < 0 || index >= prefabs.Count)
            return;

        // The preview is created under an inactive parent.
        // That way I can disable the grab components before they register to the interaction manager.
        if (m_PreviewHolder == null)
        {
            m_PreviewHolder = new GameObject("[PlacementPreviewHolder]");
            m_PreviewHolder.SetActive(false);
        }

        m_Preview = Instantiate(prefabs[index], m_PreviewHolder.transform);
        m_Preview.name = prefabs[index].name + " (Preview)";

        foreach (var behaviour in m_Preview.GetComponentsInChildren<MonoBehaviour>(true))
            behaviour.enabled = false;
        foreach (var col in m_Preview.GetComponentsInChildren<Collider>(true))
            col.enabled = false;
        foreach (var body in m_Preview.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
        }
        SetLayerRecursively(m_Preview.transform, k_IgnoreRaycastLayer);

        m_Preview.SetActive(false);
        m_Preview.transform.SetParent(null, false);
    }

    void DestroyPreview()
    {
        if (m_Preview != null)
            Destroy(m_Preview);
        m_Preview = null;
    }

    void UpdatePreview(bool menuOpen)
    {
        if (m_Preview == null)
            return;

        bool show = !menuOpen && m_Reticle.hasFloor;
        if (m_Preview.activeSelf != show)
            m_Preview.SetActive(show);
        if (!show)
            return;

        Vector3 position = m_Reticle.surfacePosition;
        Vector3 normal = m_Reticle.surfaceNormal;
        m_Preview.transform.SetPositionAndRotation(position, FacingRotation(position, normal));
    }

    // Same rotation as ObjectSpawner, so the preview and the placed item face the same way.
    Quaternion FacingRotation(Vector3 position, Vector3 normal)
    {
        var faceCamera = m_ObjectSpawner.cameraToFace != null ? m_ObjectSpawner.cameraToFace : m_Camera;
        Vector3 forward = Vector3.ProjectOnPlane(faceCamera.transform.position - position, normal);
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.ProjectOnPlane(-faceCamera.transform.forward, normal);
        return Quaternion.LookRotation(forward.normalized, normal);
    }

    static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
            SetLayerRecursively(root.GetChild(i), layer);
    }

    // A touch does not place anything if a second finger joins (rotate gesture),
    // if it starts or ends on the UI, or if it starts on a placed item (that is selecting, not placing).
    bool UpdatePlacementTouch(out Vector2 releasePosition)
    {
        releasePosition = default;
        var touches = Touch.activeTouches;

        if (touches.Count == 0)
        {
            ResetTouch();
            return false;
        }

        if (touches.Count > 1)
            m_TouchInvalid = true;

        bool trackedTouchFound = false;
        foreach (var touch in touches)
        {
            if (m_TrackedTouchId < 0)
            {
                if (touch.phase == TouchPhase.Began && touches.Count == 1)
                {
                    m_TrackedTouchId = touch.touchId;
                    m_TouchInvalid = IsOverUI(touch.screenPosition) || IsOverPlacedObject(touch.screenPosition);
                    trackedTouchFound = true;
                    UpdateAim(touch.screenPosition);
                }
                continue;
            }

            if (touch.touchId != m_TrackedTouchId)
                continue;

            trackedTouchFound = true;

            if (touch.phase == TouchPhase.Canceled)
            {
                ResetTouch();
                return false;
            }

            if (touch.phase == TouchPhase.Ended)
            {
                bool isValid = !m_TouchInvalid && !IsOverUI(touch.screenPosition);
                releasePosition = touch.screenPosition;
                ResetTouch();
                return isValid;
            }

            UpdateAim(touch.screenPosition);
        }

        if (m_TrackedTouchId >= 0 && !trackedTouchFound)
            ResetTouch();

        return false;
    }

    void UpdateAim(Vector2 screenPosition)
    {
        m_Reticle.aimScreenPointOverride = m_TouchInvalid ? (Vector2?)null : screenPosition;
    }

    void ResetTouch()
    {
        m_TrackedTouchId = -1;
        m_TouchInvalid = false;
        if (m_Reticle != null)
            m_Reticle.aimScreenPointOverride = null;
    }

    bool IsOverPlacedObject(Vector2 screenPosition)
    {
        var ray = m_Camera.ScreenPointToRay(screenPosition);
        return Physics.Raycast(ray, out RaycastHit hit, 20f)
            && hit.collider.transform.IsChildOf(m_ObjectSpawner.transform);
    }

    bool IsOverUI(Vector2 screenPosition)
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        m_PointerData ??= new PointerEventData(eventSystem);
        m_PointerData.position = screenPosition;
        m_UIHits.Clear();
        eventSystem.RaycastAll(m_PointerData, m_UIHits);
        return m_UIHits.Count > 0;
    }
}
