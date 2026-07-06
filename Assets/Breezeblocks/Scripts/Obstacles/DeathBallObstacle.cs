using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

public sealed class DeathBallObstacle : RevealableObstacle
{
    private enum DeathBallMode
    {
        Orbit,
        Shoot
    }

    private enum OrbitDirection
    {
        Clockwise,
        Counterclockwise
    }

    private enum DirectionChangeMode
    {
        None,
        Timed,
        ObstacleZone
    }

    private enum TargetLoopMode
    {
        None,
        LoopList,
        PingPong
    }

    [Title("DeathBall Mode")]
    [SerializeField]
    private DeathBallMode mode = DeathBallMode.Orbit;

    [Title("Orbit")]
    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    private Transform orbitCenter;

    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    private OrbitDirection orbitDirection = OrbitDirection.Clockwise;

    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    [MinValue(0.01f)]
    private float orbitDegreesPerSecond = 180f;

    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    private bool captureRadiusFromStart = true;

    [SerializeField]
    [ShowIf(nameof(ShowManualOrbitRadius))]
    [MinValue(0.01f)]
    private float orbitRadius = 2f;

    [Title("Orbit Direction Change")]
    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    private DirectionChangeMode directionChangeMode = DirectionChangeMode.None;

    [SerializeField]
    [ShowIf(nameof(ShowTimedDirectionChange))]
    [MinValue(0.01f)]
    private float directionChangeDelay = 3f;

    [SerializeField]
    [ShowIf(nameof(ShowTimedDirectionChange))]
    private bool loopDirectionChange = true;

    [Title("Orbit Distance")]
    [SerializeField]
    [ShowIf(nameof(IsOrbitMode))]
    private bool enableOrbitDistanceMovement;

    [SerializeField]
    [ShowIf(nameof(ShowOrbitDistanceSettings))]
    private bool moveAwayOnReveal;

    [SerializeField]
    [ShowIf(nameof(ShowOrbitDistanceSettings))]
    [MinValue(0f)]
    private float moveAwayVelocity = 1f;

    [SerializeField]
    [ShowIf(nameof(ShowOrbitDistanceSettings))]
    [MinValue(0.01f)]
    private float moveAwayTime = 1f;

    [SerializeField]
    [ShowIf(nameof(ShowOrbitDistanceSettings))]
    private bool returnAfterMoveAway = true;

    [SerializeField]
    [ShowIf(nameof(ShowReturnSettings))]
    [MinValue(0f)]
    private float returnDelay = 0.5f;

    [SerializeField]
    [ShowIf(nameof(ShowReturnSettings))]
    [MinValue(0.01f)]
    private float returnVelocity = 1f;

    [Title("Shoot")]
    [SerializeField]
    [ShowIf(nameof(IsShootMode))]
    [MinValue(0f)]
    private float shootDelay = 0.5f;

    [SerializeField]
    [ShowIf(nameof(IsShootMode))]
    [MinValue(0.01f)]
    private float shootSpeed = 14f;

    [SerializeField]
    [ShowIf(nameof(ShowPlayerShootSettings))]
    private bool keepShooting;

    [SerializeField]
    [ShowIf(nameof(ShowAdditionalShootDelay))]
    [MinValue(0f)]
    private float keepShootingAdditionalDelay = 0.5f;

    [SerializeField]
    [ShowIf(nameof(IsShootMode))]
    private bool shootOnTarget;

    [SerializeField]
    [ShowIf(nameof(ShowTargetShootSettings))]
    private TargetLoopMode targetLoopMode = TargetLoopMode.None;

    [SerializeField]
    [ShowIf(nameof(ShowTargetShootSettings))]
    private Transform[] shootTargets = new Transform[0];

    [Title("Shoot Shake")]
    [SerializeField]
    [ShowIf(nameof(IsShootMode))]
    [MinValue(0f)]
    private float shakeStrength = 0.25f;

    [SerializeField]
    [ShowIf(nameof(IsShootMode))]
    private Ease shakeRampEase = Ease.InQuad;

    [Title("Auto Rotate")]
    [SerializeField]
    private ObstacleAutoRotation autoRotation = new ObstacleAutoRotation();

    private Coroutine directionChangeRoutine;
    private Coroutine shootRoutine;
    private Tween radiusTween;
    private Tween shakeTween;
    private Tween shootTween;
    private Vector3 shootShakeAnchor;
    private OrbitDirection startOrbitDirection;
    private float currentOrbitRadius;
    private float startOrbitRadius;
    private float currentOrbitAngle;
    private float startOrbitAngle;
    private bool shootShakeActive;
    private int shootTargetIndex;
    private int shootTargetStep = 1;

    /// <summary>
    /// Gets whether orbit-only inspector fields should be visible.
    /// </summary>
    private bool IsOrbitMode => mode == DeathBallMode.Orbit;

    /// <summary>
    /// Gets whether shoot-only inspector fields should be visible.
    /// </summary>
    private bool IsShootMode => mode == DeathBallMode.Shoot;

    /// <summary>
    /// Gets whether manual orbit radius should be visible.
    /// </summary>
    private bool ShowManualOrbitRadius => IsOrbitMode && !captureRadiusFromStart;

    /// <summary>
    /// Gets whether timed direction-change options should be visible.
    /// </summary>
    private bool ShowTimedDirectionChange => IsOrbitMode && directionChangeMode == DirectionChangeMode.Timed;

    /// <summary>
    /// Gets whether move-away options should be visible.
    /// </summary>
    private bool ShowOrbitDistanceSettings => IsOrbitMode && enableOrbitDistanceMovement;

    /// <summary>
    /// Gets whether return-after-move-away options should be visible.
    /// </summary>
    private bool ShowReturnSettings => IsOrbitMode && enableOrbitDistanceMovement && returnAfterMoveAway;

    /// <summary>
    /// Gets whether player-repeat shooting options should be visible.
    /// </summary>
    private bool ShowPlayerShootSettings => IsShootMode && !shootOnTarget;

    /// <summary>
    /// Gets whether the extra delay between repeated or target-list shots should be visible.
    /// </summary>
    private bool ShowAdditionalShootDelay => IsShootMode && (keepShooting || shootOnTarget);

    /// <summary>
    /// Gets whether target shooting options should be visible.
    /// </summary>
    private bool ShowTargetShootSettings => IsShootMode && shootOnTarget;

    /// <summary>
    /// Captures orbit start data after base obstacle state is cached.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        CacheOrbitStartState();
        autoRotation.Initialize(GetMoveTarget());
    }

    /// <summary>
    /// Updates inherited follow movement and active orbit movement.
    /// </summary>
    protected override void Update()
    {
        base.Update();
        UpdateOrbit();
        autoRotation.Tick(Time.deltaTime);
    }

    /// <summary>
    /// Stops local tweens and coroutines before Unity disables this obstacle.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        StopDeathBallBehavior();
    }

    /// <summary>
    /// Stops local tweens and coroutines before Unity destroys this obstacle.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        StopDeathBallBehavior();
    }

    /// <summary>
    /// Kills the player when the player touches the DeathBall.
    /// </summary>
    protected override void OnPlayerContact(GameObject player)
    {
        LevelGameManager.Current?.RegisterDeath(player);
    }

    /// <summary>
    /// Starts mode-specific behavior when the DeathBall becomes revealed.
    /// </summary>
    protected override void OnRevealed(GameObject revealingPlayer)
    {
        base.OnRevealed(revealingPlayer);

        if (mode == DeathBallMode.Orbit)
        {
            StartOrbitBehavior();
        }

        autoRotation.Start();
    }

    /// <summary>
    /// Stops DeathBall-specific behavior when reveal zones hide it again.
    /// </summary>
    protected override void OnHidden()
    {
        base.OnHidden();
        StopDeathBallBehavior();
    }

    /// <summary>
    /// Starts shooting at the player when an obstacle zone requests it.
    /// </summary>
    public override void TriggerShoot(GameObject targetPlayer)
    {
        if (mode != DeathBallMode.Shoot || !IsRevealed)
        {
            return;
        }

        StartShootSequence(targetPlayer != null ? targetPlayer.transform : null);
    }

    /// <summary>
    /// Changes orbit direction when an obstacle zone requests it.
    /// </summary>
    public override void TriggerDirectionChange()
    {
        if (mode != DeathBallMode.Orbit || directionChangeMode != DirectionChangeMode.ObstacleZone)
        {
            return;
        }

        ToggleOrbitDirection();
    }

    /// <summary>
    /// Moves orbit radius away from its starting radius when an obstacle zone requests it.
    /// </summary>
    public override void TriggerMoveAway()
    {
        if (mode != DeathBallMode.Orbit || !enableOrbitDistanceMovement)
        {
            return;
        }

        MoveOrbitRadiusAway();
    }

    /// <summary>
    /// Moves orbit radius back to its starting radius when an obstacle zone requests it.
    /// </summary>
    public override void TriggerReturn()
    {
        if (mode != DeathBallMode.Orbit || !enableOrbitDistanceMovement)
        {
            return;
        }

        MoveOrbitRadiusBack();
    }

    /// <summary>
    /// Restores DeathBall behavior and base obstacle state to level-start values.
    /// </summary>
    public override void ResetLevelState()
    {
        StopDeathBallBehavior();
        orbitDirection = startOrbitDirection;
        currentOrbitRadius = startOrbitRadius;
        currentOrbitAngle = startOrbitAngle;
        shootTargetIndex = 0;
        shootTargetStep = 1;
        base.ResetLevelState();
        autoRotation.ResetToStart();

        if (IsRevealed)
        {
            autoRotation.Start();
        }
    }

    /// <summary>
    /// Captures orbit radius, angle, and direction from the authored scene placement.
    /// </summary>
    private void CacheOrbitStartState()
    {
        startOrbitDirection = orbitDirection;
        startOrbitRadius = GetConfiguredStartRadius();
        currentOrbitRadius = startOrbitRadius;
        startOrbitAngle = GetCurrentAngle();
        currentOrbitAngle = startOrbitAngle;
    }

    /// <summary>
    /// Starts timed orbit direction changes and optional reveal-time radius movement.
    /// </summary>
    private void StartOrbitBehavior()
    {
        StopDirectionChangeRoutine();

        if (directionChangeMode == DirectionChangeMode.Timed)
        {
            directionChangeRoutine = StartCoroutine(ChangeDirectionOverTime());
        }

        if (enableOrbitDistanceMovement && moveAwayOnReveal)
        {
            MoveOrbitRadiusAway();
        }
    }

    /// <summary>
    /// Advances the DeathBall around its configured orbit center.
    /// </summary>
    private void UpdateOrbit()
    {
        if (mode != DeathBallMode.Orbit || !IsRevealed || orbitCenter == null)
        {
            return;
        }

        float signedSpeed = orbitDirection == OrbitDirection.Clockwise ? -orbitDegreesPerSecond : orbitDegreesPerSecond;
        currentOrbitAngle += signedSpeed * Time.deltaTime;
        float angleRadians = currentOrbitAngle * Mathf.Deg2Rad;
        Vector3 orbitOffset = new Vector3(Mathf.Cos(angleRadians), Mathf.Sin(angleRadians), 0f) * currentOrbitRadius;
        GetMoveTarget().position = orbitCenter.position + orbitOffset;
    }

    /// <summary>
    /// Changes orbit direction after a delay, optionally looping until reset or disable.
    /// </summary>
    private IEnumerator ChangeDirectionOverTime()
    {
        do
        {
            yield return new WaitForSeconds(directionChangeDelay);
            ToggleOrbitDirection();
        }
        while (loopDirectionChange);

        directionChangeRoutine = null;
    }

    /// <summary>
    /// Flips the current orbit direction.
    /// </summary>
    private void ToggleOrbitDirection()
    {
        orbitDirection = orbitDirection == OrbitDirection.Clockwise
            ? OrbitDirection.Counterclockwise
            : OrbitDirection.Clockwise;
    }

    /// <summary>
    /// Tweens orbit radius outward using the configured velocity and time.
    /// </summary>
    private void MoveOrbitRadiusAway()
    {
        if (moveAwayVelocity <= 0f)
        {
            return;
        }

        KillRadiusTween();
        float targetRadius = currentOrbitRadius + moveAwayVelocity * moveAwayTime;
        radiusTween = DOTween.To(() => currentOrbitRadius, value => currentOrbitRadius = value, targetRadius, moveAwayTime)
            .SetEase(Ease.Linear)
            .OnComplete(() =>
            {
                radiusTween = null;

                if (returnAfterMoveAway)
                {
                    MoveOrbitRadiusBack();
                }
            });
    }

    /// <summary>
    /// Tweens orbit radius back to its starting value after the configured delay.
    /// </summary>
    private void MoveOrbitRadiusBack()
    {
        if (returnVelocity <= 0f)
        {
            return;
        }

        KillRadiusTween();
        float distance = Mathf.Abs(currentOrbitRadius - startOrbitRadius);
        float duration = Mathf.Max(0.01f, distance / returnVelocity);
        radiusTween = DOTween.To(() => currentOrbitRadius, value => currentOrbitRadius = value, startOrbitRadius, duration)
            .SetDelay(returnDelay)
            .SetEase(Ease.Linear)
            .OnComplete(() => radiusTween = null);
    }

    /// <summary>
    /// Starts a shoot sequence at the given player transform.
    /// </summary>
    private void StartShootSequence(Transform targetPlayer)
    {
        if (shootRoutine != null)
        {
            return;
        }

        shootRoutine = StartCoroutine(ShootAtPlayerSequence(targetPlayer));
    }

    /// <summary>
    /// Shakes before shooting, then moves rapidly toward the player's delayed position.
    /// </summary>
    private IEnumerator ShootAtPlayerSequence(Transform targetPlayer)
    {
        bool shouldContinue = false;

        do
        {
            yield return WaitForShootDelay();

            if (!TryGetShootTargetPosition(targetPlayer, out Vector3 targetPosition))
            {
                break;
            }

            yield return MoveToShootTarget(targetPosition);
            shouldContinue = GetShouldContinueShooting();

            if (shouldContinue && keepShootingAdditionalDelay > 0f)
            {
                yield return new WaitForSeconds(keepShootingAdditionalDelay);
            }
        }
        while (shouldContinue);

        shootRoutine = null;
    }

    /// <summary>
    /// Resolves the current shoot destination from target-list mode or the delayed player position.
    /// </summary>
    private bool TryGetShootTargetPosition(Transform targetPlayer, out Vector3 targetPosition)
    {
        if (!shootOnTarget)
        {
            targetPosition = targetPlayer != null ? targetPlayer.position : Vector3.zero;
            return targetPlayer != null;
        }

        Transform target = GetCurrentShootTarget();
        targetPosition = target != null ? target.position : Vector3.zero;
        return target != null;
    }

    /// <summary>
    /// Gets the currently selected target from the configured target list.
    /// </summary>
    private Transform GetCurrentShootTarget()
    {
        if (shootTargets == null || shootTargets.Length == 0)
        {
            return null;
        }

        shootTargetIndex = Mathf.Clamp(shootTargetIndex, 0, shootTargets.Length - 1);
        return shootTargets[shootTargetIndex];
    }

    /// <summary>
    /// Advances the target list index based on the selected loop style.
    /// </summary>
    private bool GetShouldContinueShooting()
    {
        if (!shootOnTarget)
        {
            return keepShooting;
        }

        if (shootTargets == null || shootTargets.Length <= 1)
        {
            return targetLoopMode != TargetLoopMode.None;
        }

        if (targetLoopMode == TargetLoopMode.None)
        {
            if (shootTargetIndex >= shootTargets.Length - 1)
            {
                return false;
            }

            shootTargetIndex++;
            return true;
        }

        if (targetLoopMode == TargetLoopMode.LoopList)
        {
            shootTargetIndex = (shootTargetIndex + 1) % shootTargets.Length;
            return true;
        }

        shootTargetIndex += shootTargetStep;

        if (shootTargetIndex >= shootTargets.Length)
        {
            shootTargetIndex = shootTargets.Length - 2;
            shootTargetStep = -1;
        }
        else if (shootTargetIndex < 0)
        {
            shootTargetIndex = 1;
            shootTargetStep = 1;
        }

        return true;
    }

    /// <summary>
    /// Waits through the configured shoot delay while playing buildup shake.
    /// </summary>
    private IEnumerator WaitForShootDelay()
    {
        if (shootDelay <= 0f)
        {
            yield break;
        }

        StartShootShake(shootDelay);
        yield return new WaitForSeconds(shootDelay);
        StopShootShake(true);
    }

    /// <summary>
    /// Moves the DeathBall to the captured shoot target position.
    /// </summary>
    private IEnumerator MoveToShootTarget(Vector3 targetPosition)
    {
        KillShootTween();
        float distance = Vector3.Distance(GetMoveTarget().position, targetPosition);
        float duration = Mathf.Max(0.01f, distance / shootSpeed);
        shootTween = GetMoveTarget().DOMove(targetPosition, duration).SetEase(Ease.Linear);
        yield return shootTween.WaitForCompletion();
        shootTween = null;
    }

    /// <summary>
    /// Plays a gradually stronger positional shake before the DeathBall shoots.
    /// </summary>
    private void StartShootShake(float duration)
    {
        StopShootShake(false);

        if (shakeStrength <= 0f || duration <= 0f)
        {
            return;
        }

        Transform moveTarget = GetMoveTarget();
        shootShakeAnchor = moveTarget.position;
        shootShakeActive = true;
        shakeTween = DOVirtual.Float(0f, shakeStrength, duration, strength =>
            {
                Vector2 offset = Random.insideUnitCircle * strength;
                moveTarget.position = shootShakeAnchor + new Vector3(offset.x, offset.y, 0f);
            })
            .SetEase(shakeRampEase);
    }

    /// <summary>
    /// Stops shoot shake and optionally restores the anchored position.
    /// </summary>
    private void StopShootShake(bool restoreAnchor)
    {
        bool shouldRestoreAnchor = restoreAnchor && shootShakeActive;

        if (shakeTween != null && shakeTween.IsActive())
        {
            shakeTween.Kill();
        }

        shakeTween = null;
        shootShakeActive = false;

        if (shouldRestoreAnchor)
        {
            GetMoveTarget().position = shootShakeAnchor;
        }
    }

    /// <summary>
    /// Gets the starting orbit radius from the authored placement or manual value.
    /// </summary>
    private float GetConfiguredStartRadius()
    {
        if (!captureRadiusFromStart || orbitCenter == null)
        {
            return Mathf.Max(0.01f, orbitRadius);
        }

        return Mathf.Max(0.01f, Vector3.Distance(GetMoveTarget().position, orbitCenter.position));
    }

    /// <summary>
    /// Gets the current orbit angle relative to the configured center.
    /// </summary>
    private float GetCurrentAngle()
    {
        if (orbitCenter == null)
        {
            return 0f;
        }

        Vector3 direction = GetMoveTarget().position - orbitCenter.position;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
    }

    /// <summary>
    /// Stops all DeathBall-specific runtime behavior.
    /// </summary>
    private void StopDeathBallBehavior()
    {
        StopDirectionChangeRoutine();
        StopShootRoutine();
        KillRadiusTween();
        StopShootShake(true);
        KillShootTween();
        autoRotation.Stop();
    }

    /// <summary>
    /// Stops timed direction changes when active.
    /// </summary>
    private void StopDirectionChangeRoutine()
    {
        if (directionChangeRoutine == null)
        {
            return;
        }

        StopCoroutine(directionChangeRoutine);
        directionChangeRoutine = null;
    }

    /// <summary>
    /// Stops active shoot looping when active.
    /// </summary>
    private void StopShootRoutine()
    {
        if (shootRoutine == null)
        {
            return;
        }

        StopCoroutine(shootRoutine);
        shootRoutine = null;
    }

    /// <summary>
    /// Kills the active orbit radius tween.
    /// </summary>
    private void KillRadiusTween()
    {
        if (radiusTween == null || !radiusTween.IsActive())
        {
            return;
        }

        radiusTween.Kill();
        radiusTween = null;
    }

    /// <summary>
    /// Kills the active shoot movement tween.
    /// </summary>
    private void KillShootTween()
    {
        if (shootTween == null || !shootTween.IsActive())
        {
            return;
        }

        shootTween.Kill();
        shootTween = null;
    }
}
