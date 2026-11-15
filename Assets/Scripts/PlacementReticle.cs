using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Ring that shows where the item will go.
// It runs before PlacementController so that the preview uses the position of the same frame.
[DefaultExecutionOrder(-10)]
public class PlacementReticle : MonoBehaviour
{
    [Header("Referanslar")]
    [Tooltip("Boş bırakılırsa sahnede otomatik aranır (XR Origin üzerinde olmalı).")]
    [SerializeField]
    ARRaycastManager m_RaycastManager;

    [Tooltip("Halkanın materyali (URP Unlit, Transparent, beyaz önerilir).")]
    [SerializeField]
    Material m_RingMaterial;

    [Header("Görünüm")]
    [SerializeField]
    float m_Radius = 0.2f;

    [SerializeField]
    float m_LineWidth = 0.015f;

    [SerializeField]
    int m_Segments = 64;

    [Header("Davranış")]
    [Tooltip("Parmak yokken nişanın ekrandaki yüksekliği (alttan, 0-1). 0.5 = tam orta. " +
             "Düşük değer, telefonu daha az eğerek zemini bulmayı sağlar.")]
    [SerializeField, Range(0.1f, 0.6f)]
    float m_DefaultAimHeight = 0.35f;

    [Tooltip("Bu alandan (m²) küçük plane'ler halka için yok sayılır.")]
    [SerializeField]
    float m_MinPlaneArea = 0.25f;

    [Tooltip("Büyüdükçe halka hedefe daha hızlı yetişir, küçüldükçe daha yumuşak ama gecikmeli olur.")]
    [SerializeField]
    float m_PositionSmoothing = 12f;

    [SerializeField]
    float m_RotationSmoothing = 8f;

    [Tooltip("Bu mesafeden (m) büyük sıçramalarda halka kaymak yerine doğrudan atlar.")]
    [SerializeField]
    float m_SnapDistance = 0.4f;

    [Tooltip("Halkanın zeminle çakışıp titrememesi için zeminden yükseklik (m). Sadece çizim içindir.")]
    [SerializeField]
    float m_SurfaceOffset = 0.003f;

    static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    Camera m_Camera;
    GameObject m_RingObject;
    bool m_HasPose;
    Vector3 m_SurfacePosition;
    Vector3 m_SurfaceNormal = Vector3.up;

    public bool visible { get; set; }

    public bool hasFloor { get; private set; }

    public Vector3 surfacePosition => m_SurfacePosition;

    public Vector3 surfaceNormal => m_SurfaceNormal;

    public Vector2? aimScreenPointOverride { get; set; }

    // I moved the default aim point lower than the center.
    // In a small room I had to tilt the phone a lot before the floor showed up in the middle of the screen.
    Vector2 DefaultAimPoint => new Vector2(Screen.width * 0.5f, Screen.height * m_DefaultAimHeight);

    void Awake()
    {
        m_Camera = Camera.main;

        if (m_RaycastManager == null)
            m_RaycastManager = FindAnyObjectByType<ARRaycastManager>();

        if (m_RaycastManager == null)
            Debug.LogError("[PlacementReticle] Sahnede ARRaycastManager bulunamadı. XR Origin'e ekleyin.");
        if (m_RingMaterial == null)
            Debug.LogWarning("[PlacementReticle] Ring Material atanmamış; halka pembe görünebilir.");

        BuildRing();
        m_RingObject.SetActive(false);
    }

    void BuildRing()
    {
        m_RingObject = new GameObject("ReticleRing");
        m_RingObject.transform.SetParent(transform, false);
        m_RingObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        m_RingObject.transform.localPosition = new Vector3(0f, m_SurfaceOffset, 0f);

        var line = m_RingObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.alignment = LineAlignment.TransformZ;
        line.widthMultiplier = m_LineWidth;
        line.positionCount = m_Segments;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        if (m_RingMaterial != null)
            line.sharedMaterial = m_RingMaterial;

        for (int i = 0; i < m_Segments; i++)
        {
            float angle = i * Mathf.PI * 2f / m_Segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * m_Radius, Mathf.Sin(angle) * m_Radius, 0f));
        }
    }

    void Update()
    {
        bool followingFinger = aimScreenPointOverride.HasValue;
        Vector2 aimPoint = aimScreenPointOverride ?? DefaultAimPoint;

        bool found = TryGetFloorPose(aimPoint, out Pose pose);
        if (found)
            MoveTowards(pose, snap: followingFinger);
        else
            m_HasPose = false;

        hasFloor = found;
        m_RingObject.SetActive(visible && found);
    }

    public bool SnapToScreenPoint(Vector2 screenPoint)
    {
        if (!TryGetFloorPose(screenPoint, out Pose pose))
            return false;

        MoveTowards(pose, snap: true);
        hasFloor = true;
        return true;
    }

    bool TryGetFloorPose(Vector2 screenPoint, out Pose pose)
    {
        pose = default;
        if (m_RaycastManager == null)
            return false;

        if (!m_RaycastManager.Raycast(screenPoint, s_Hits, TrackableType.PlaneWithinPolygon))
            return false;

        // Only upward planes that are big enough and not merged into another plane.
        foreach (var hit in s_Hits)
        {
            if (hit.trackable is ARPlane plane
                && plane.alignment == PlaneAlignment.HorizontalUp
                && plane.subsumedBy == null
                && plane.size.x * plane.size.y >= m_MinPlaneArea)
            {
                pose = hit.pose;
                return true;
            }
        }

        return false;
    }

    void MoveTowards(Pose pose, bool snap)
    {
        Vector3 targetPosition = pose.position;
        m_SurfaceNormal = pose.up;

        Vector3 forward = Vector3.ProjectOnPlane(m_Camera.transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.0001f)
            forward = transform.forward;
        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        // Big jumps (for example table to floor) snap instead of sliding.
        bool bigJump = Vector3.Distance(m_SurfacePosition, targetPosition) > m_SnapDistance;
        if (snap || !m_HasPose || bigJump)
        {
            m_SurfacePosition = targetPosition;
            transform.SetPositionAndRotation(targetPosition, targetRotation);
            m_HasPose = true;
            return;
        }

        // Exponential smoothing so it behaves the same at any frame rate.
        float tPos = 1f - Mathf.Exp(-m_PositionSmoothing * Time.deltaTime);
        float tRot = 1f - Mathf.Exp(-m_RotationSmoothing * Time.deltaTime);
        m_SurfacePosition = Vector3.Lerp(m_SurfacePosition, targetPosition, tPos);
        transform.SetPositionAndRotation(
            m_SurfacePosition,
            Quaternion.Slerp(transform.rotation, targetRotation, tRot));
    }
}
