using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Collider2D))]
public sealed class ObstacleTriggerZone : MonoBehaviour
{
    private enum TriggerAction
    {
        Reveal,
        Move,
        Follow,
        Teleport,
        Hide
    }

    [Title("Target")]
    [SerializeField]
    private RevealableObstacle obstacle;

    [SerializeField]
    private TriggerAction action = TriggerAction.Reveal;

    [Title("Player Filter")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    private Collider2D triggerCollider;

    /// <summary>
    /// Caches and configures the same-object trigger collider.
    /// </summary>
    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Runs the selected obstacle action when the player enters the zone.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (obstacle == null || !IsInPlayerLayer(other.gameObject))
        {
            return;
        }

        switch (action)
        {
            case TriggerAction.Reveal:
                obstacle.Reveal(other.gameObject);
                break;
            case TriggerAction.Move:
                obstacle.TriggerMove();
                break;
            case TriggerAction.Follow:
                obstacle.StartFollowing(other.transform);
                break;
            case TriggerAction.Teleport:
                obstacle.TriggerTeleport();
                break;
            case TriggerAction.Hide:
                obstacle.Hide();
                break;
        }
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
}
