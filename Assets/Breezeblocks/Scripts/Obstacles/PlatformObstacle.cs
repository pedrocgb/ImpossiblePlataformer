using System.Collections;
using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

public sealed class PlatformObstacle : RevealableObstacle
{
    [Title("Loop Movement")]
    [SerializeField]
    private bool loopMovement;

    [SerializeField]
    [ShowIf(nameof(loopMovement))]
    private Transform loopDestination;

    [SerializeField]
    [ShowIf(nameof(loopMovement))]
    [MinValue(0.01f)]
    private float loopMoveDuration = 1f;

    [SerializeField]
    [ShowIf(nameof(loopMovement))]
    [MinValue(0f)]
    private float loopMovementDelay;

    [SerializeField]
    [ShowIf(nameof(loopMovement))]
    private Ease loopEase = Ease.InOutSine;

    [Title("Deactivate On Time")]
    [SerializeField]
    private bool deactivateOnTime;

    [SerializeField]
    [ShowIf(nameof(deactivateOnTime))]
    [MinValue(0.01f)]
    private float deactivateAfterSeconds = 2f;

    [SerializeField]
    [ShowIf(nameof(ShowReactivateSettings))]
    private bool reactivates;

    [SerializeField]
    [ShowIf(nameof(ShowReactivateDelay))]
    [MinValue(0.01f)]
    private float reactivateAfterSeconds = 2f;

    [SerializeField]
    [ShowIf(nameof(ShowTimedDeactivationLoop))]
    private bool loopTimedDeactivation;

    [SerializeField]
    [ShowIf(nameof(ShowReactivateSettings))]
    [LabelText("Deactivate Selected Colliders")]
    private bool deactivateColliders = true;

    [SerializeField]
    [ShowIf(nameof(ShowColliderDeactivationTargets))]
    [InfoBox("Assign the ground/platform colliders that should turn off during deactivate and back on during reactivate.")]
    private Collider2D[] collidersToDeactivate = new Collider2D[0];

    [Title("Deactivate On Collision")]
    [SerializeField]
    private bool deactivateOnCollision;

    [SerializeField]
    [ShowIf(nameof(deactivateOnCollision))]
    [MinValue(0.01f)]
    private float collisionDeactivateDelay = 0.25f;

    [Title("Player Attachment")]
    [SerializeField]
    private bool attachPlayerOnTop;

    [SerializeField]
    [ShowIf(nameof(attachPlayerOnTop))]
    [Range(0f, 1f)]
    private float topContactNormalThreshold = 0.5f;

    [Title("Auto Rotate")]
    [SerializeField]
    private ObstacleAutoRotation autoRotation = new ObstacleAutoRotation();

    private Tween loopTween;
    private Coroutine deactivateRoutine;
    private Transform attachedPlayer;
    private Rigidbody2D attachedPlayerBody;
    private PlayerMovement attachedPlayerMovement;
    private Vector3 lastAttachTargetPosition;
    private bool[] colliderDeactivationStartStates = new bool[0];

    protected override bool SupportsFollow => false;
    protected override bool SupportsTeleport => false;

    private bool ShowReactivateSettings => deactivateOnTime || deactivateOnCollision;
    private bool ShowReactivateDelay => ShowReactivateSettings && reactivates;
    private bool ShowTimedDeactivationLoop => deactivateOnTime && reactivates;
    private bool ShowColliderDeactivationTargets => ShowReactivateSettings && deactivateColliders;

    /// <summary>
    /// Caches base obstacle state and the initial platform position used for player attachment.
    /// </summary>
    protected override void Awake()
    {
        base.Awake();
        lastAttachTargetPosition = GetMoveTarget().position;
        colliderDeactivationStartStates = CaptureColliderDeactivationStartStates();
        autoRotation.Initialize(GetMoveTarget());
    }

    /// <summary>
    /// Updates base obstacle behavior and active auto-rotation.
    /// </summary>
    protected override void Update()
    {
        base.Update();
        autoRotation.Tick(Time.deltaTime);
    }

    /// <summary>
    /// Carries an attached player by the platform delta during physics ticks.
    /// </summary>
    private void FixedUpdate()
    {
        MoveAttachedPlayerWithPlatform();
    }

    /// <summary>
    /// Restores platform movement and deactivation state to level start.
    /// </summary>
    public override void ResetLevelState()
    {
        DetachPlayer();
        KillLoopTween();
        StopDeactivateRoutine();
        base.ResetLevelState();
        autoRotation.ResetToStart();

        if (IsRevealed)
        {
            autoRotation.Start();
        }

        RestoreSelectedColliderStates();
        lastAttachTargetPosition = GetMoveTarget().position;
    }

    /// <summary>
    /// Stops platform tweens and routines when the object is disabled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        DetachPlayer();
        KillLoopTween();
        StopDeactivateRoutine();
        autoRotation.Stop();
    }

    /// <summary>
    /// Stops platform tweens and routines before destruction.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        DetachPlayer();
        KillLoopTween();
        autoRotation.Stop();
    }

    /// <summary>
    /// Starts reveal-time platform behavior.
    /// </summary>
    protected override void OnRevealed(GameObject revealingPlayer)
    {
        base.OnRevealed(revealingPlayer);

        if (!StartsHidden || MoveOnStartEnabled)
        {
            StartLoopMovement();
        }

        if (deactivateOnTime)
        {
            StartDeactivateTimer(deactivateAfterSeconds, true);
        }

        autoRotation.Start();
    }

    /// <summary>
    /// Stops auto-rotation when a reveal zone hides this platform again.
    /// </summary>
    protected override void OnHidden()
    {
        base.OnHidden();
        autoRotation.Stop();
    }

    /// <summary>
    /// Starts collision-triggered deactivation when configured.
    /// </summary>
    protected override void OnPlayerContact(GameObject player)
    {
        if (deactivateOnCollision)
        {
            StartDeactivateTimer(collisionDeactivateDelay, false);
        }
    }

    /// <summary>
    /// Attaches the player while collision data shows they are standing on top of this platform.
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (!attachPlayerOnTop || !IsPlayer(collision.gameObject))
        {
            return;
        }

        if (IsPlayerStandingOnPlatform(collision))
        {
            AttachPlayer(collision.gameObject);
            return;
        }

        if (attachedPlayer == collision.transform)
        {
            DetachPlayer();
        }
    }

    /// <summary>
    /// Detaches the player when they leave this platform.
    /// </summary>
    private void OnCollisionExit2D(Collision2D collision)
    {
        if (attachedPlayer == collision.transform)
        {
            DetachPlayer();
        }
    }

    /// <summary>
    /// Starts the loop movement tween after the configured delay.
    /// </summary>
    private void StartLoopMovement()
    {
        if (!loopMovement || loopDestination == null)
        {
            return;
        }

        KillLoopTween();
        Transform moveTarget = GetMoveTarget();
        loopTween = moveTarget.DOMove(loopDestination.position, loopMoveDuration)
            .SetDelay(loopMovementDelay)
            .SetEase(loopEase)
            .SetLoops(-1, LoopType.Yoyo);
    }

    /// <summary>
    /// Starts a delayed deactivate sequence.
    /// </summary>
    private void StartDeactivateTimer(float delay, bool allowLoop)
    {
        StopDeactivateRoutine();
        deactivateRoutine = StartCoroutine(DeactivateAfterDelay(delay, allowLoop));
    }

    /// <summary>
    /// Deactivates the platform after a delay and optionally reactivates it.
    /// </summary>
    private IEnumerator DeactivateAfterDelay(float delay, bool allowLoop)
    {
        bool shouldLoop;

        do
        {
            yield return new WaitForSeconds(delay);
            DetachPlayer();
            ApplyDeactivationVisibility(false);
            KillLoopTween();

            if (!reactivates)
            {
                deactivateRoutine = null;
                yield break;
            }

            yield return new WaitForSeconds(reactivateAfterSeconds);
            ApplyDeactivationVisibility(true);
            StartLoopMovement();
            delay = deactivateAfterSeconds;
            shouldLoop = allowLoop && loopTimedDeactivation;
        }
        while (shouldLoop);

        deactivateRoutine = null;
    }

    /// <summary>
    /// Applies platform deactivation visibility and optionally toggles selected colliders.
    /// </summary>
    private void ApplyDeactivationVisibility(bool visible)
    {
        ApplyControlledRendererVisibility(visible);

        if (deactivateColliders)
        {
            ApplySelectedColliderVisibility(visible);
        }
    }

    /// <summary>
    /// Enables or disables the explicitly assigned platform colliders for deactivate/reactivate cycles.
    /// </summary>
    private void ApplySelectedColliderVisibility(bool visible)
    {
        if (collidersToDeactivate == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDeactivate.Length; i++)
        {
            if (collidersToDeactivate[i] == null)
            {
                continue;
            }

            bool initialState = i < colliderDeactivationStartStates.Length && colliderDeactivationStartStates[i];
            collidersToDeactivate[i].enabled = visible && initialState;
        }
    }

    /// <summary>
    /// Restores assigned platform colliders to their level-start enabled states.
    /// </summary>
    private void RestoreSelectedColliderStates()
    {
        if (collidersToDeactivate == null)
        {
            return;
        }

        for (int i = 0; i < collidersToDeactivate.Length; i++)
        {
            if (collidersToDeactivate[i] == null || i >= colliderDeactivationStartStates.Length)
            {
                continue;
            }

            collidersToDeactivate[i].enabled = colliderDeactivationStartStates[i];
        }
    }

    /// <summary>
    /// Captures initial enabled states for colliders assigned to platform deactivation.
    /// </summary>
    private bool[] CaptureColliderDeactivationStartStates()
    {
        if (collidersToDeactivate == null)
        {
            return new bool[0];
        }

        bool[] states = new bool[collidersToDeactivate.Length];

        for (int i = 0; i < collidersToDeactivate.Length; i++)
        {
            states[i] = collidersToDeactivate[i] != null && collidersToDeactivate[i].enabled;
        }

        return states;
    }

    /// <summary>
    /// Checks collision contacts and player grounded state to confirm top attachment.
    /// </summary>
    private bool IsPlayerStandingOnPlatform(Collision2D collision)
    {
        PlayerMovement playerMovement = collision.gameObject.GetComponent<PlayerMovement>();

        if (playerMovement == null || !playerMovement.IsGrounded || collision.transform.position.y < GetMoveTarget().position.y)
        {
            return false;
        }

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            if (Mathf.Abs(contact.normal.y) >= topContactNormalThreshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Stores the player transform and Rigidbody2D so platform deltas can carry them.
    /// </summary>
    private void AttachPlayer(GameObject player)
    {
        if (attachedPlayer == player.transform)
        {
            return;
        }

        attachedPlayer = player.transform;
        attachedPlayerBody = player.GetComponent<Rigidbody2D>();
        attachedPlayerMovement = player.GetComponent<PlayerMovement>();
        lastAttachTargetPosition = GetMoveTarget().position;
    }

    /// <summary>
    /// Clears the currently attached player references.
    /// </summary>
    private void DetachPlayer()
    {
        attachedPlayer = null;
        attachedPlayerBody = null;
        attachedPlayerMovement = null;
        lastAttachTargetPosition = GetMoveTarget().position;
    }

    /// <summary>
    /// Adds platform movement delta to the attached player without changing player input velocity.
    /// </summary>
    private void MoveAttachedPlayerWithPlatform()
    {
        Vector3 currentPosition = GetMoveTarget().position;
        Vector3 platformDelta = currentPosition - lastAttachTargetPosition;
        lastAttachTargetPosition = currentPosition;

        if (!attachPlayerOnTop || attachedPlayer == null)
        {
            return;
        }

        if (attachedPlayerMovement != null && !attachedPlayerMovement.IsGrounded)
        {
            DetachPlayer();
            return;
        }

        if (platformDelta.sqrMagnitude <= 0f)
        {
            return;
        }

        if (attachedPlayerBody != null)
        {
            attachedPlayerBody.position += (Vector2)platformDelta;
            return;
        }

        attachedPlayer.position += platformDelta;
    }

    /// <summary>
    /// Stops the active deactivate coroutine when one exists.
    /// </summary>
    private void StopDeactivateRoutine()
    {
        if (deactivateRoutine == null)
        {
            return;
        }

        StopCoroutine(deactivateRoutine);
        deactivateRoutine = null;
    }

    /// <summary>
    /// Kills the active loop movement tween when one exists.
    /// </summary>
    private void KillLoopTween()
    {
        if (loopTween == null || !loopTween.IsActive())
        {
            return;
        }

        loopTween.Kill();
        loopTween = null;
    }
}
