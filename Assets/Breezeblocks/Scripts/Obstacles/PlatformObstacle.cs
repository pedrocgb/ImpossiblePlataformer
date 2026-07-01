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

    [Title("Deactivate On Collision")]
    [SerializeField]
    private bool deactivateOnCollision;

    [SerializeField]
    [ShowIf(nameof(deactivateOnCollision))]
    [MinValue(0.01f)]
    private float collisionDeactivateDelay = 0.25f;

    private Tween loopTween;
    private Coroutine deactivateRoutine;

    protected override bool SupportsFollow => false;
    protected override bool SupportsTeleport => false;

    private bool ShowReactivateSettings => deactivateOnTime || deactivateOnCollision;
    private bool ShowReactivateDelay => ShowReactivateSettings && reactivates;

    /// <summary>
    /// Restores platform movement and deactivation state to level start.
    /// </summary>
    public override void ResetLevelState()
    {
        KillLoopTween();
        StopDeactivateRoutine();
        base.ResetLevelState();
    }

    /// <summary>
    /// Stops platform tweens and routines when the object is disabled.
    /// </summary>
    protected override void OnDisable()
    {
        base.OnDisable();
        KillLoopTween();
        StopDeactivateRoutine();
    }

    /// <summary>
    /// Stops platform tweens and routines before destruction.
    /// </summary>
    protected override void OnDestroy()
    {
        base.OnDestroy();
        KillLoopTween();
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
            StartDeactivateTimer(deactivateAfterSeconds);
        }
    }

    /// <summary>
    /// Starts collision-triggered deactivation when configured.
    /// </summary>
    protected override void OnPlayerContact(GameObject player)
    {
        if (deactivateOnCollision)
        {
            StartDeactivateTimer(collisionDeactivateDelay);
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
    private void StartDeactivateTimer(float delay)
    {
        StopDeactivateRoutine();
        deactivateRoutine = StartCoroutine(DeactivateAfterDelay(delay));
    }

    /// <summary>
    /// Deactivates the platform after a delay and optionally reactivates it.
    /// </summary>
    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        ApplyVisibility(false);
        KillLoopTween();

        if (!reactivates)
        {
            deactivateRoutine = null;
            yield break;
        }

        yield return new WaitForSeconds(reactivateAfterSeconds);
        ApplyVisibility(true);
        StartLoopMovement();
        deactivateRoutine = null;
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
