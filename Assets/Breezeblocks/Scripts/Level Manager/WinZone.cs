using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(Collider2D))]
public sealed class WinZone : MonoBehaviour, ILevelResettable
{
    [Title("Player Filter")]
    [SerializeField]
    private LayerMask playerLayer = ~0;

    private Collider2D triggerCollider;
    private bool used;

    /// <summary>
    /// Caches and configures the same-object trigger collider.
    /// </summary>
    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
    }

    /// <summary>
    /// Completes the level when the player reaches the win zone.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used || !IsInPlayerLayer(other.gameObject) || LevelGameManager.Current == null || !LevelGameManager.Current.CanCompleteLevel)
        {
            return;
        }

        used = true;
        LevelGameManager.Current.RegisterWin();
    }

    /// <summary>
    /// Restores the win zone so it can be used again after a level reset.
    /// </summary>
    public void ResetLevelState()
    {
        used = false;
    }

    /// <summary>
    /// Checks whether the target object belongs to the configured player layer mask.
    /// </summary>
    private bool IsInPlayerLayer(GameObject target)
    {
        return (playerLayer.value & (1 << target.layer)) != 0;
    }
}
