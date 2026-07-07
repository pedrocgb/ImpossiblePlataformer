using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Collider2D))]
public sealed class ObstacleTriggerZone : MonoBehaviour, ILevelResettable
{
    private enum TriggerAction
    {
        Reveal,
        Move,
        Follow,
        Teleport,
        Hide,
        Shoot,
        ChangeDirection,
        MoveAway,
        Return
    }

    [Title("Target")]
    [SerializeField]
    private RevealableObstacle obstacle;

    [SerializeField]
    [ShowIf(nameof(IsMoveAction))]
    private DoorScript door;

    [SerializeField]
    private TriggerAction action = TriggerAction.Reveal;

    [Title("Player Filter")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    [Title("One Shot")]
    [SerializeField]
    private bool deactivateAfterActivation;

    private Collider2D triggerCollider;
    private bool startActiveState;
    private bool startColliderEnabled;

    private bool IsMoveAction => action == TriggerAction.Move;

    /// <summary>
    /// Caches and configures the same-object trigger collider and reset state.
    /// </summary>
    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        startActiveState = gameObject.activeSelf;
        startColliderEnabled = triggerCollider.enabled;
        triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Runs the selected obstacle action when the player enters the zone.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsInPlayerLayer(other.gameObject))
        {
            return;
        }

        bool handled = false;

        switch (action)
        {
            case TriggerAction.Reveal:
                handled = TryRevealObstacle(other.gameObject);
                break;
            case TriggerAction.Move:
                handled = TryMoveObstacle();
                handled |= TryMoveDoor();
                break;
            case TriggerAction.Follow:
                handled = TryStartObstacleFollow(other.transform);
                break;
            case TriggerAction.Teleport:
                handled = TryTeleportObstacle();
                break;
            case TriggerAction.Hide:
                handled = TryHideObstacle();
                break;
            case TriggerAction.Shoot:
                handled = TryShootObstacle(other.gameObject);
                break;
            case TriggerAction.ChangeDirection:
                handled = TryChangeObstacleDirection();
                break;
            case TriggerAction.MoveAway:
                handled = TryMoveObstacleAway();
                break;
            case TriggerAction.Return:
                handled = TryReturnObstacle();
                break;
        }

        if (!handled)
        {
            return;
        }

        DeactivateAfterActivationIfNeeded();
    }

    /// <summary>
    /// Hides revealed obstacles on exit when the obstacle is not configured to stay revealed.
    /// </summary>
    private void OnTriggerExit2D(Collider2D other)
    {
        if (obstacle == null || action != TriggerAction.Reveal || !IsInPlayerLayer(other.gameObject))
        {
            return;
        }

        obstacle.HideFromRevealZone();
    }

    /// <summary>
    /// Checks whether the target object belongs to the configured player layer mask.
    /// </summary>
    private bool IsInPlayerLayer(GameObject target)
    {
        return (playerLayer.value & (1 << target.layer)) != 0;
    }

    /// <summary>
    /// Reveals the configured obstacle when one exists.
    /// </summary>
    private bool TryRevealObstacle(GameObject player)
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.Reveal(player);
        return true;
    }

    /// <summary>
    /// Starts configured obstacle movement when one exists.
    /// </summary>
    private bool TryMoveObstacle()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerMove();
        return true;
    }

    /// <summary>
    /// Starts configured door movement when one exists.
    /// </summary>
    private bool TryMoveDoor()
    {
        if (door == null)
        {
            return false;
        }

        door.TriggerMove();
        return true;
    }

    /// <summary>
    /// Starts configured obstacle follow behavior when one exists.
    /// </summary>
    private bool TryStartObstacleFollow(Transform player)
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.StartFollowing(player);
        return true;
    }

    /// <summary>
    /// Teleports the configured obstacle when one exists.
    /// </summary>
    private bool TryTeleportObstacle()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerTeleport();
        return true;
    }

    /// <summary>
    /// Hides the configured obstacle when one exists.
    /// </summary>
    private bool TryHideObstacle()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.Hide();
        return true;
    }

    /// <summary>
    /// Requests configured obstacle shooting behavior when one exists.
    /// </summary>
    private bool TryShootObstacle(GameObject player)
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerShoot(player);
        return true;
    }

    /// <summary>
    /// Requests configured obstacle direction changes when one exists.
    /// </summary>
    private bool TryChangeObstacleDirection()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerDirectionChange();
        return true;
    }

    /// <summary>
    /// Requests configured obstacle move-away behavior when one exists.
    /// </summary>
    private bool TryMoveObstacleAway()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerMoveAway();
        return true;
    }

    /// <summary>
    /// Requests configured obstacle return behavior when one exists.
    /// </summary>
    private bool TryReturnObstacle()
    {
        if (obstacle == null)
        {
            return false;
        }

        obstacle.TriggerReturn();
        return true;
    }

    /// <summary>
    /// Restores this trigger zone to its level-start active and collider state.
    /// </summary>
    public void ResetLevelState()
    {
        gameObject.SetActive(startActiveState);

        if (triggerCollider == null)
        {
            triggerCollider = GetComponent<Collider2D>();
        }

        triggerCollider.enabled = startColliderEnabled;
        triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Fully disables this trigger zone after it fires when configured as one-shot.
    /// </summary>
    private void DeactivateAfterActivationIfNeeded()
    {
        if (deactivateAfterActivation)
        {
            gameObject.SetActive(false);
        }
    }
}
