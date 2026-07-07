using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DoorScript : MonoBehaviour, ILevelResettable
{
    private struct TransformSnapshot
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
    }

    [Title("Player Filter")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    [Title("Win")]
    [SerializeField]
    private bool completeLevelOnPlayerTouch = true;

    [Title("Movement Target")]
    [SerializeField]
    [InfoBox("Leave empty to move this door transform.")]
    private Transform transformToMove;

    [Title("Move To Position")]
    [SerializeField]
    private Transform moveDestination;

    [SerializeField]
    [MinValue(0.01f)]
    private float moveDuration = 0.5f;

    [SerializeField]
    private Ease moveEase = Ease.Linear;

    private Collider2D triggerCollider;
    private Tween moveTween;
    private TransformSnapshot startTransformSnapshot;
    private bool used;

    /// <summary>
    /// Caches the same-object trigger collider and the authored door transform.
    /// </summary>
    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
        startTransformSnapshot = CaptureTransform(GetMoveTarget());
    }

    /// <summary>
    /// Completes the level when configured and the player reaches the door.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!completeLevelOnPlayerTouch
            || used
            || !IsInPlayerLayer(other.gameObject)
            || LevelGameManager.Current == null
            || !LevelGameManager.Current.CanCompleteLevel)
        {
            return;
        }

        used = true;
        LevelGameManager.Current.RegisterWin();
    }

    /// <summary>
    /// Moves the configured door target to its destination when requested by an obstacle zone.
    /// </summary>
    public void TriggerMove()
    {
        if (moveDestination == null)
        {
            return;
        }

        KillMoveTween();
        moveTween = GetMoveTarget().DOMove(moveDestination.position, moveDuration).SetEase(moveEase);
    }

    /// <summary>
    /// Restores the door movement target and win state to level-start values.
    /// </summary>
    public void ResetLevelState()
    {
        KillMoveTween();
        used = false;
        RestoreTransform(GetMoveTarget(), startTransformSnapshot);

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Stops active door movement tweens when Unity disables this door.
    /// </summary>
    private void OnDisable()
    {
        KillMoveTween();
    }

    /// <summary>
    /// Stops active door movement tweens before Unity destroys this door.
    /// </summary>
    private void OnDestroy()
    {
        KillMoveTween();
    }

    /// <summary>
    /// Returns the configured transform to move, falling back to this door transform.
    /// </summary>
    private Transform GetMoveTarget()
    {
        return transformToMove != null ? transformToMove : transform;
    }

    /// <summary>
    /// Checks whether the target object belongs to the configured player layer mask.
    /// </summary>
    private bool IsInPlayerLayer(GameObject target)
    {
        return (playerLayer.value & (1 << target.layer)) != 0;
    }

    /// <summary>
    /// Kills the active door movement tween when one exists.
    /// </summary>
    private void KillMoveTween()
    {
        if (moveTween == null || !moveTween.IsActive())
        {
            return;
        }

        moveTween.Kill();
        moveTween = null;
    }

    /// <summary>
    /// Captures a transform's world position, world rotation, and local scale.
    /// </summary>
    private static TransformSnapshot CaptureTransform(Transform target)
    {
        return new TransformSnapshot
        {
            Position = target.position,
            Rotation = target.rotation,
            Scale = target.localScale
        };
    }

    /// <summary>
    /// Restores a transform snapshot captured at level start.
    /// </summary>
    private static void RestoreTransform(Transform target, TransformSnapshot snapshot)
    {
        target.SetPositionAndRotation(snapshot.Position, snapshot.Rotation);
        target.localScale = snapshot.Scale;
    }
}
