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
        private bool startsActive = true;

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

        /// <summary>
        /// Prepares the line renderer for two-point laser drawing.
        /// </summary>
        public void Initialize()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 2;
            }
        }

        /// <summary>
        /// Restores active state, timer state, and line visibility to level start.
        /// </summary>
        public void ResetLevelState()
        {
            active = startsActive;
            activationTimer = active ? activeSeconds : inactiveSeconds;
            SetLineVisible(active);
        }

        /// <summary>
        /// Hides the laser line while the obstacle is disabled.
        /// </summary>
        public void Stop()
        {
            SetLineVisible(false);
        }

        /// <summary>
        /// Updates optional timed activation, laser drawing, and player hit detection.
        /// </summary>
        public void Tick(float deltaTime, float rayDistance, LayerMask playerLayer, LayerMask blockingLayer)
        {
            TickTimedActivation(deltaTime);

            if (!active)
            {
                SetLineVisible(false);
                return;
            }

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
            Vector3 endPosition = startPosition + firePoint.right * rayDistance;
            int rayMask = playerLayer.value | blockingLayer.value;
            RaycastHit2D hit = Physics2D.Raycast(startPosition, firePoint.right, rayDistance, rayMask);

            if (hit.collider != null)
            {
                endPosition = hit.point;

                if (IsInLayer(hit.collider.gameObject, playerLayer))
                {
                    LevelGameManager.Current?.RegisterDeath(hit.collider.gameObject);
                }
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
}
