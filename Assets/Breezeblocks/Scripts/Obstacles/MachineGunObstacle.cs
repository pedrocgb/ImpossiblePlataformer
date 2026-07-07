using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public sealed class MachineGunObstacle : MonoBehaviour, ILevelResettable
{
    private enum MachineGunState
    {
        Targeting,
        Activated,
        Deactivated
    }

    private enum ShootingMode
    {
        Raycast,
        Projectile
    }

    private enum IdleMode
    {
        Static,
        Cycle
    }

    [Title("References")]
    [SerializeField]
    private Transform firePoint;

    [SerializeField]
    private MachineGunProjectile projectilePrefab;

    [SerializeField]
    private GameObject muzzleFlashPrefab;

    [SerializeField]
    private GameObject bulletHitPrefab;

    [Title("Detection")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    [SerializeField]
    private LayerMask blockingLayer;

    [SerializeField, MinValue(0.01f)]
    private float rayDistance = 20f;

    [Title("Shooting")]
    [SerializeField]
    private ShootingMode shootingMode = ShootingMode.Raycast;

    [SerializeField, MinValue(0f)]
    private float activationShootDelay;

    [SerializeField, MinValue(1)]
    private int bulletsPerActivation = 1;

    [SerializeField]
    [ShowIf(nameof(ShowTimeBetweenShots))]
    [MinValue(0f)]
    private float timeBetweenShots = 0.1f;

    [SerializeField]
    [ShowIf(nameof(ShowProjectileSettings))]
    [MinValue(0.01f)]
    private float projectileSpeed = 14f;

    [SerializeField]
    [ShowIf(nameof(ShowProjectileSettings))]
    [MinValue(0.01f)]
    private float projectileLifetime = 4f;

    [SerializeField]
    private MachineGunBulletEffect bulletEffect = MachineGunBulletEffect.Kill;

    [SerializeField]
    [ShowIf(nameof(ShowPushSettings))]
    [MinValue(0f)]
    private float pushForce = 8f;

    [Title("Reactivation")]
    [SerializeField]
    private bool fullyDeactivateAfterShooting;

    [SerializeField]
    [HideIf(nameof(fullyDeactivateAfterShooting))]
    [MinValue(0f)]
    private float reactivateAfterSeconds = 1f;

    [Title("Idle")]
    [SerializeField]
    private IdleMode idleMode = IdleMode.Static;

    [SerializeField]
    [ShowIf(nameof(IsCycleMode))]
    private float minimumAngle;

    [SerializeField]
    [ShowIf(nameof(IsCycleMode))]
    private float maximumAngle = 180f;

    [SerializeField]
    [ShowIf(nameof(IsCycleMode))]
    [MinValue(0.01f)]
    private float idleDegreesPerSecond = 90f;

    [SerializeField]
    [ShowIf(nameof(IsCycleMode))]
    private bool followPlayer;

    [SerializeField]
    [ShowIf(nameof(ShowFollowTarget))]
    private Transform followTarget;

#if UNITY_EDITOR
    [Title("Debug")]
    [SerializeField]
    private bool drawDebugRaycasts = true;

    [SerializeField]
    private Color debugRayColor = Color.cyan;

    [SerializeField]
    private Color debugCycleLimitColor = Color.yellow;
#endif

    private LineRenderer targetingLine;
    private Coroutine activationRoutine;
    private Transform orbitPivot;
    private Vector3 startFirePointLocalPosition;
    private Quaternion startFirePointLocalRotation;
    private float cycleAngle;
    private float startCycleAngle;
    private float cycleDirection = 1f;
    private float orbitRadius;
    private Transform activatedTarget;
    private MachineGunState state = MachineGunState.Targeting;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private MachineGunState CurrentState => state;

    private bool IsCycleMode => idleMode == IdleMode.Cycle;
    private bool ShowProjectileSettings => shootingMode == ShootingMode.Projectile;
    private bool ShowTimeBetweenShots => bulletsPerActivation > 1;
    private bool ShowPushSettings => bulletEffect == MachineGunBulletEffect.Push;
    private bool ShowFollowTarget => IsCycleMode && followPlayer;

    /// <summary>
    /// Caches the same-object line renderer and the authored fire point state.
    /// </summary>
    private void Awake()
    {
        targetingLine = GetComponent<LineRenderer>();
        CacheFirePointStartState();
        ConfigureLineRenderer();
    }

    /// <summary>
    /// Updates the targeting ray during visible states and advances idle aim while the machinegun is searching.
    /// </summary>
    private void Update()
    {
        if (firePoint == null)
        {
            SetTargetingLineVisible(false);
            return;
        }

        if (state == MachineGunState.Targeting)
        {
            UpdateIdleAim();
            UpdateTargetingRay(true);
            TryActivateFromTargetingRay();
        }
        else if (state == MachineGunState.Activated)
        {
            UpdateActivatedAim();
            UpdateTargetingRay(true);
        }

#if UNITY_EDITOR
        DrawEditorDebugRaycasts();
#endif
    }

    /// <summary>
    /// Stops active shooting routines when the object is disabled.
    /// </summary>
    private void OnDisable()
    {
        StopActivationRoutine();
    }

    /// <summary>
    /// Stops active shooting routines before the object is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        StopActivationRoutine();
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws configured machinegun rays in edit mode so they are visible in the Scene view.
    /// </summary>
    private void OnDrawGizmos()
    {
        DrawEditorDebugGizmos();
    }
#endif

    /// <summary>
    /// Restores fire point aim and targeting state to level-start values.
    /// </summary>
    public void ResetLevelState()
    {
        StopActivationRoutine();
        state = MachineGunState.Targeting;
        activatedTarget = null;
        RestoreFirePointStartState();
        SetTargetingLineVisible(true);
    }

    /// <summary>
    /// Captures authored fire point radius, angle, and local transform.
    /// </summary>
    private void CacheFirePointStartState()
    {
        if (firePoint == null)
        {
            return;
        }

        orbitPivot = firePoint.parent != null ? firePoint.parent : transform;
        startFirePointLocalPosition = firePoint.localPosition;
        startFirePointLocalRotation = firePoint.localRotation;
        Vector3 offset = firePoint.position - orbitPivot.position;
        orbitRadius = offset.magnitude;
        cycleAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
        startCycleAngle = cycleAngle;
    }

    /// <summary>
    /// Ensures the targeting line starts with two points.
    /// </summary>
    private void ConfigureLineRenderer()
    {
        targetingLine.positionCount = 2;
        SetTargetingLineVisible(true);
    }

    /// <summary>
    /// Restores the fire point transform captured at level start.
    /// </summary>
    private void RestoreFirePointStartState()
    {
        if (firePoint == null)
        {
            return;
        }

        firePoint.localPosition = startFirePointLocalPosition;
        firePoint.localRotation = startFirePointLocalRotation;
        cycleAngle = startCycleAngle;
        cycleDirection = 1f;
    }

    /// <summary>
    /// Updates cycle idle aiming or player-follow aiming within the configured angle limits.
    /// </summary>
    private void UpdateIdleAim()
    {
        if (idleMode != IdleMode.Cycle || orbitPivot == null)
        {
            return;
        }

        cycleAngle += idleDegreesPerSecond * cycleDirection * Time.deltaTime;

        if (cycleAngle >= maximumAngle)
        {
            cycleAngle = maximumAngle;
            cycleDirection = -1f;
        }
        else if (cycleAngle <= minimumAngle)
        {
            cycleAngle = minimumAngle;
            cycleDirection = 1f;
        }

        ApplyFirePointAngle(cycleAngle);
    }

    /// <summary>
    /// Rotates toward the activated player target while preserving cycle angle limits.
    /// </summary>
    private void UpdateActivatedAim()
    {
        if (idleMode != IdleMode.Cycle || !followPlayer || orbitPivot == null)
        {
            return;
        }

        Transform target = GetFollowTarget();

        if (target == null)
        {
            return;
        }

        Vector3 directionToPlayer = target.position - orbitPivot.position;
        float targetAngle = Mathf.Atan2(directionToPlayer.y, directionToPlayer.x) * Mathf.Rad2Deg;
        targetAngle = Mathf.Clamp(targetAngle, minimumAngle, maximumAngle);
        cycleAngle = Mathf.MoveTowardsAngle(cycleAngle, targetAngle, idleDegreesPerSecond * Time.deltaTime);
        ApplyFirePointAngle(cycleAngle);
    }

    /// <summary>
    /// Gets the assigned follow target or the player captured when the machinegun activated.
    /// </summary>
    private Transform GetFollowTarget()
    {
        return followTarget != null ? followTarget : activatedTarget;
    }

    /// <summary>
    /// Places and rotates the fire point at the requested orbit angle.
    /// </summary>
    private void ApplyFirePointAngle(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        firePoint.position = orbitPivot.position + direction * orbitRadius;
        firePoint.right = direction;
    }

    /// <summary>
    /// Draws the targeting ray to the first hit or to maximum range.
    /// </summary>
    private void UpdateTargetingRay(bool visible)
    {
        SetTargetingLineVisible(visible);

        if (!visible)
        {
            return;
        }

        RaycastHit2D hit = CastTargetingRay();
        Vector3 startPosition = firePoint.position;
        Vector3 endPosition = hit.collider != null
            ? (Vector3)hit.point
            : startPosition + firePoint.right * rayDistance;

        targetingLine.SetPosition(0, startPosition);
        targetingLine.SetPosition(1, endPosition);
    }

    /// <summary>
    /// Activates shooting when the targeting ray sees the player.
    /// </summary>
    private void TryActivateFromTargetingRay()
    {
        RaycastHit2D hit = CastTargetingRay();

        if (hit.collider == null || !IsInLayer(hit.collider.gameObject, playerLayer))
        {
            return;
        }

        StartActivation(hit.collider.transform);
    }

    /// <summary>
    /// Starts the active shooting routine and stores the target that triggered it.
    /// </summary>
    private void StartActivation(Transform target)
    {
        if (activationRoutine != null)
        {
            return;
        }

        activatedTarget = target;
        state = MachineGunState.Activated;
        activationRoutine = StartCoroutine(ShootActivationRoutine());
    }

    /// <summary>
    /// Fires configured shots, then enters deactivated state before optional reactivation.
    /// </summary>
    private IEnumerator ShootActivationRoutine()
    {
        if (activationShootDelay > 0f)
        {
            yield return new WaitForSeconds(activationShootDelay);
        }

        for (int i = 0; i < bulletsPerActivation; i++)
        {
            FireShot();

            if (i < bulletsPerActivation - 1 && timeBetweenShots > 0f)
            {
                yield return new WaitForSeconds(timeBetweenShots);
            }
        }

        state = MachineGunState.Deactivated;
        activatedTarget = null;
        UpdateTargetingRay(false);

        if (!fullyDeactivateAfterShooting)
        {
            yield return new WaitForSeconds(reactivateAfterSeconds);
            state = MachineGunState.Targeting;
            activatedTarget = null;
            SetTargetingLineVisible(true);
        }

        activationRoutine = null;
    }

    /// <summary>
    /// Fires one configured shot from the fire point.
    /// </summary>
    private void FireShot()
    {
        SpawnMuzzleFlash();

        if (shootingMode == ShootingMode.Raycast)
        {
            FireRaycastShot();
            return;
        }

        FireProjectileShot();
    }

    /// <summary>
    /// Resolves an instant raycast shot against the first player or blocking hit.
    /// </summary>
    private void FireRaycastShot()
    {
        RaycastHit2D hit = CastTargetingRay();

        if (hit.collider == null)
        {
            return;
        }

        MachineGunBulletEffectUtility.SpawnHitEffect(bulletHitPrefab, hit.point, hit.normal);

        if (IsInLayer(hit.collider.gameObject, playerLayer))
        {
            MachineGunBulletEffectUtility.ApplyToPlayer(hit.collider.gameObject, bulletEffect, firePoint.right, pushForce);
        }
    }

    /// <summary>
    /// Spawns and initializes a projectile shot.
    /// </summary>
    private void FireProjectileShot()
    {
        if (projectilePrefab == null)
        {
            return;
        }

        MachineGunProjectile projectile = Instantiate(projectilePrefab, firePoint.position, firePoint.rotation);
        projectile.Initialize(firePoint.right, projectileSpeed, projectileLifetime, playerLayer, blockingLayer, bulletEffect, pushForce, bulletHitPrefab);
    }

    /// <summary>
    /// Spawns a muzzle flash at the fire point.
    /// </summary>
    private void SpawnMuzzleFlash()
    {
        if (muzzleFlashPrefab == null)
        {
            return;
        }

        Instantiate(muzzleFlashPrefab, firePoint.position, firePoint.rotation);
    }

    /// <summary>
    /// Casts the machinegun ray against players and blocking layers.
    /// </summary>
    private RaycastHit2D CastTargetingRay()
    {
        int rayMask = playerLayer.value | blockingLayer.value;
        return Physics2D.Raycast(firePoint.position, firePoint.right, rayDistance, rayMask);
    }

    /// <summary>
    /// Shows or hides the targeting line renderer.
    /// </summary>
    private void SetTargetingLineVisible(bool visible)
    {
        if (targetingLine != null)
        {
            targetingLine.enabled = visible;
        }
    }

    /// <summary>
    /// Stops the active shooting routine when one exists.
    /// </summary>
    private void StopActivationRoutine()
    {
        if (activationRoutine == null)
        {
            return;
        }

        StopCoroutine(activationRoutine);
        activationRoutine = null;
        activatedTarget = null;
    }

    /// <summary>
    /// Checks whether a game object belongs to a layer mask.
    /// </summary>
    private static bool IsInLayer(GameObject target, LayerMask layerMask)
    {
        return (layerMask.value & (1 << target.layer)) != 0;
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws the current static ray or both configured cycle limit rays in the Scene view.
    /// </summary>
    private void DrawEditorDebugRaycasts()
    {
        if (!drawDebugRaycasts || firePoint == null)
        {
            return;
        }

        if (idleMode == IdleMode.Cycle && orbitPivot != null)
        {
            DrawCycleLimitDebugRay(minimumAngle);
            DrawCycleLimitDebugRay(maximumAngle);
            return;
        }

        DrawDebugRay(firePoint.position, firePoint.right, debugRayColor);
    }

    /// <summary>
    /// Draws one debug ray for a configured cycle endpoint angle.
    /// </summary>
    private void DrawCycleLimitDebugRay(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        Vector3 startPosition = orbitPivot.position + direction * orbitRadius;
        DrawDebugRay(startPosition, direction, debugCycleLimitColor);
    }

    /// <summary>
    /// Draws one editor debug ray from the supplied start position and direction.
    /// </summary>
    private void DrawDebugRay(Vector3 startPosition, Vector3 direction, Color color)
    {
        Debug.DrawLine(startPosition, startPosition + direction.normalized * rayDistance, color);
    }

    /// <summary>
    /// Draws the current static ray or both configured cycle limit rays as edit-mode Scene view gizmos.
    /// </summary>
    private void DrawEditorDebugGizmos()
    {
        if (!drawDebugRaycasts || firePoint == null)
        {
            return;
        }

        if (idleMode == IdleMode.Cycle)
        {
            Transform pivot = firePoint.parent != null ? firePoint.parent : transform;
            float radius = Vector3.Distance(firePoint.position, pivot.position);
            DrawCycleLimitGizmoRay(pivot, radius, minimumAngle);
            DrawCycleLimitGizmoRay(pivot, radius, maximumAngle);
            return;
        }

        DrawGizmoRay(firePoint.position, firePoint.right, debugRayColor);
    }

    /// <summary>
    /// Draws one edit-mode gizmo ray for a configured cycle endpoint angle.
    /// </summary>
    private void DrawCycleLimitGizmoRay(Transform pivot, float radius, float angle)
    {
        if (pivot == null)
        {
            return;
        }

        float radians = angle * Mathf.Deg2Rad;
        Vector3 direction = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0f);
        Vector3 startPosition = pivot.position + direction * radius;
        DrawGizmoRay(startPosition, direction, debugCycleLimitColor);
    }

    /// <summary>
    /// Draws one edit-mode Scene view gizmo ray with temporary color state.
    /// </summary>
    private void DrawGizmoRay(Vector3 startPosition, Vector3 direction, Color color)
    {
        Color previousColor = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawLine(startPosition, startPosition + direction.normalized * rayDistance);
        Gizmos.color = previousColor;
    }
#endif
}
