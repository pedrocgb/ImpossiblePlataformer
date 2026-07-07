using System;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class LaserObstacle : MonoBehaviour, ILevelResettable
{
    [Serializable]
    private sealed class LaserFirePoint
    {
        [SerializeField]
        [Required]
        private Transform firePoint;

        [SerializeField]
        [Required]
        private LineRenderer lineRenderer;

        [SerializeField]
        private GameObject laserHitObject;

        [SerializeField]
        private bool startsActive = true;

        [SerializeField]
        [MinValue(0.01f)]
        private float activationGrowSeconds = 0.2f;

        [SerializeField]
        private bool timedActivation;

        [SerializeField]
        [ShowIf(nameof(timedActivation))]
        [MinValue(0.01f)]
        private float activeSeconds = 2f;

        [SerializeField]
        [ShowIf(nameof(timedActivation))]
        [MinValue(0.01f)]
        private float inactiveSeconds = 0.5f;

        private bool active;
        private float activationTimer;
        private float currentRayDistance;

        /// <summary>
        /// Prepares the line renderer and optional hit object for laser drawing.
        /// </summary>
        public void Initialize()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 2;
            }

            SetHitObjectVisible(false);
        }

        /// <summary>
        /// Restores active state, timer state, growth distance, and line visibility to level start.
        /// </summary>
        public void ResetLevelState()
        {
            active = startsActive;
            activationTimer = active ? activeSeconds : inactiveSeconds;
            currentRayDistance = 0f;
            SetLineVisible(active);
            SetHitObjectVisible(false);
        }

        /// <summary>
        /// Hides the laser line and hit object while the obstacle is disabled.
        /// </summary>
        public void Stop()
        {
            currentRayDistance = 0f;
            SetLineVisible(false);
            SetHitObjectVisible(false);
        }

        /// <summary>
        /// Updates optional timed activation, laser drawing, and player hit detection.
        /// </summary>
        public void Tick(float deltaTime, float rayDistance, LayerMask playerLayer, LayerMask blockingLayer)
        {
            TickTimedActivation(deltaTime);

            if (!active)
            {
                currentRayDistance = 0f;
                SetLineVisible(false);
                SetHitObjectVisible(false);
                return;
            }

            UpdateRayGrowth(deltaTime, rayDistance);
            UpdateLaser(rayDistance, playerLayer, blockingLayer);
        }

        /// <summary>
        /// Advances the per-firepoint active and inactive timer when blinking is enabled.
        /// </summary>
        private void TickTimedActivation(float deltaTime)
        {
            if (!timedActivation)
            {
                return;
            }

            activationTimer -= deltaTime;

            if (activationTimer > 0f)
            {
                return;
            }

            active = !active;
            activationTimer = active ? activeSeconds : inactiveSeconds;
            SetLineVisible(active);

            if (!active)
            {
                currentRayDistance = 0f;
                SetHitObjectVisible(false);
            }
        }

        /// <summary>
        /// Advances the visible and damaging ray distance while the laser activates.
        /// </summary>
        private void UpdateRayGrowth(float deltaTime, float rayDistance)
        {
            if (activationGrowSeconds <= 0f)
            {
                currentRayDistance = rayDistance;
                return;
            }

            float growthSpeed = rayDistance / activationGrowSeconds;
            currentRayDistance = Mathf.MoveTowards(currentRayDistance, rayDistance, growthSpeed * deltaTime);
        }

        /// <summary>
        /// Draws the laser to the first blocking hit or maximum range and applies death on player hit.
        /// </summary>
        private void UpdateLaser(float rayDistance, LayerMask playerLayer, LayerMask blockingLayer)
        {
            if (firePoint == null || lineRenderer == null)
            {
                return;
            }

            SetLineVisible(true);

            Vector3 startPosition = firePoint.position;
            float castDistance = Mathf.Min(currentRayDistance, rayDistance);
            Vector3 endPosition = startPosition + firePoint.right * castDistance;

            if (castDistance <= 0f)
            {
                lineRenderer.SetPosition(0, startPosition);
                lineRenderer.SetPosition(1, startPosition);
                SetHitObjectVisible(false);
                return;
            }

            int rayMask = playerLayer.value | blockingLayer.value;
            RaycastHit2D hit = Physics2D.Raycast(startPosition, firePoint.right, castDistance, rayMask);

            if (hit.collider != null)
            {
                endPosition = hit.point;
                UpdateHitObject(hit.point, hit.normal);

                if (IsInLayer(hit.collider.gameObject, playerLayer))
                {
                    LevelGameManager.Current?.RegisterDeath(hit.collider.gameObject);
                }
            }
            else
            {
                SetHitObjectVisible(false);
            }

            lineRenderer.SetPosition(0, startPosition);
            lineRenderer.SetPosition(1, endPosition);
        }

        /// <summary>
        /// Shows or hides the assigned line renderer when it exists.
        /// </summary>
        private void SetLineVisible(bool visible)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }

        /// <summary>
        /// Moves and shows the reusable hit object at the latest laser impact point.
        /// </summary>
        private void UpdateHitObject(Vector3 position, Vector2 normal)
        {
            if (laserHitObject == null)
            {
                return;
            }

            Transform hitTransform = laserHitObject.transform;
            hitTransform.position = position;
            hitTransform.rotation = Quaternion.FromToRotation(Vector3.up, normal);
            SetHitObjectVisible(true);
        }

        /// <summary>
        /// Shows or hides the reusable laser hit object when it exists.
        /// </summary>
        private void SetHitObjectVisible(bool visible)
        {
            if (laserHitObject != null && laserHitObject.activeSelf != visible)
            {
                laserHitObject.SetActive(visible);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draws the authored maximum laser ray in the Scene view during play mode.
        /// </summary>
        public void DrawEditorDebugRay(float rayDistance, Color color)
        {
            if (firePoint == null)
            {
                return;
            }

            Vector3 startPosition = firePoint.position;
            Debug.DrawLine(startPosition, startPosition + firePoint.right * rayDistance, color);
        }

        /// <summary>
        /// Draws the authored maximum laser ray as an edit-mode Scene view gizmo.
        /// </summary>
        public void DrawEditorGizmoRay(float rayDistance)
        {
            if (firePoint == null)
            {
                return;
            }

            Vector3 startPosition = firePoint.position;
            Gizmos.DrawLine(startPosition, startPosition + firePoint.right * rayDistance);
        }
#endif

        /// <summary>
        /// Checks whether a game object belongs to the supplied layer mask.
        /// </summary>
        private static bool IsInLayer(GameObject target, LayerMask layerMask)
        {
            return (layerMask.value & (1 << target.layer)) != 0;
        }
    }

    [Title("Laser")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    [SerializeField]
    private LayerMask blockingLayer;

    [SerializeField]
    [MinValue(0.01f)]
    private float rayDistance = 20f;

    [Title("Auto Rotate")]
    [SerializeField]
    private ObstacleAutoRotation autoRotation = new ObstacleAutoRotation();

    [SerializeField]
    private LaserFirePoint[] firePoints = new LaserFirePoint[0];

#if UNITY_EDITOR
    [Title("Debug")]
    [SerializeField]
    private bool drawDebugRaycasts = true;

    [SerializeField]
    private Color debugRayColor = Color.cyan;
#endif

    /// <summary>
    /// Prepares all configured firepoints and caches root auto-rotation before gameplay starts.
    /// </summary>
    private void Awake()
    {
        autoRotation.Initialize(transform);
        InitializeFirePoints();
        ResetFirePoints();
    }

    /// <summary>
    /// Starts root auto-rotation when the laser obstacle becomes active.
    /// </summary>
    private void OnEnable()
    {
        autoRotation.Start();
    }

    /// <summary>
    /// Updates root auto-rotation and each active firepoint laser with one raycast per frame.
    /// </summary>
    private void Update()
    {
        autoRotation.Tick(Time.deltaTime);

        if (firePoints == null)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.Tick(deltaTime, rayDistance, playerLayer, blockingLayer);
        }

#if UNITY_EDITOR
        DrawEditorDebugRays();
#endif
    }

    /// <summary>
    /// Stops root auto-rotation and hides laser lines when the obstacle is disabled.
    /// </summary>
    private void OnDisable()
    {
        autoRotation.Stop();
        StopFirePoints();
    }

    /// <summary>
    /// Stops root auto-rotation and hides laser lines before destruction.
    /// </summary>
    private void OnDestroy()
    {
        autoRotation.Stop();
        StopFirePoints();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws configured laser rays in edit mode so they are visible in the Scene view.
    /// </summary>
    private void OnDrawGizmos()
    {
        DrawEditorDebugGizmos();
    }
#endif

    /// <summary>
    /// Restores root auto-rotation and every firepoint to level-start state.
    /// </summary>
    public void ResetLevelState()
    {
        autoRotation.ResetToStart();
        autoRotation.Start();
        ResetFirePoints();
    }

    /// <summary>
    /// Initializes all configured firepoint entries.
    /// </summary>
    private void InitializeFirePoints()
    {
        if (firePoints == null)
        {
            return;
        }

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.Initialize();
        }
    }

    /// <summary>
    /// Restores all configured firepoint entries to their authored start state.
    /// </summary>
    private void ResetFirePoints()
    {
        if (firePoints == null)
        {
            return;
        }

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.ResetLevelState();
        }
    }

    /// <summary>
    /// Stops every configured firepoint entry.
    /// </summary>
    private void StopFirePoints()
    {
        if (firePoints == null)
        {
            return;
        }

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.Stop();
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws each configured firepoint ray in the Scene view for editor debugging.
    /// </summary>
    private void DrawEditorDebugRays()
    {
        if (!drawDebugRaycasts || firePoints == null)
        {
            return;
        }

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.DrawEditorDebugRay(rayDistance, debugRayColor);
        }
    }

    /// <summary>
    /// Draws each configured firepoint ray as an edit-mode Scene view gizmo.
    /// </summary>
    private void DrawEditorDebugGizmos()
    {
        if (!drawDebugRaycasts || firePoints == null)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = debugRayColor;

        for (int i = 0; i < firePoints.Length; i++)
        {
            firePoints[i]?.DrawEditorGizmoRay(rayDistance);
        }

        Gizmos.color = previousColor;
    }
#endif
}
