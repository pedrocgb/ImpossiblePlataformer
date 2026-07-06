using DG.Tweening;
using UnityEngine;
using Sirenix.OdinInspector;

public abstract class RevealableObstacle : MonoBehaviour, ILevelResettable
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

    [Title("Visibility")]
    [SerializeField]
    private bool startsHidden;

    [SerializeField]
    private bool stayRevealed = true;

    [SerializeField]
    private Transform visibilityRoot;

    [SerializeField]
    private bool controlRenderers = true;

    [SerializeField]
    private bool controlColliders = true;

    [Title("Movement Target")]
    [SerializeField]
    [InfoBox("Leave empty to move this obstacle transform.")]
    private Transform transformToMove;

    [Title("Move To Position")]
    [SerializeField]
    [ShowIf(nameof(startsHidden))]
    private bool moveOnStart;

    [SerializeField]
    [ShowIf(nameof(SupportsMoveOnZone))]
    private bool moveOnZone;

    [SerializeField]
    [ShowIf(nameof(ShowMoveDestination))]
    private Transform moveDestination;

    [SerializeField]
    [ShowIf(nameof(ShowMoveDestination))]
    [MinValue(0.01f)]
    private float moveDuration = 0.5f;

    [SerializeField]
    [ShowIf(nameof(ShowMoveDestination))]
    private Ease moveEase = Ease.Linear;

    [Title("Follow Player")]
    [SerializeField]
    [ShowIf(nameof(SupportsFollow))]
    private bool followPlayer;

    [SerializeField]
    [ShowIf(nameof(ShowFollowSettings))]
    private bool alwaysFollow;

    [SerializeField]
    [ShowIf(nameof(ShowTimedFollowDuration))]
    [MinValue(0.01f)]
    private float followDuration = 2f;

    [SerializeField]
    [ShowIf(nameof(ShowFollowSettings))]
    [MinValue(0.01f)]
    private float followSpeed = 4f;

    [Title("Teleport")]
    [SerializeField]
    [ShowIf(nameof(SupportsTeleport))]
    private bool teleportOnZone;

    [SerializeField]
    [ShowIf(nameof(ShowTeleportDestination))]
    private Transform teleportDestination;

    private Collider2D[] controlledColliders;
    private Renderer[] controlledRenderers;
    private Tween moveTween;
    private Transform followTarget;
    private float followTimer;
    private bool revealed;
    private bool[] initialColliderEnabledStates;
    private bool[] initialRendererEnabledStates;
    private TransformSnapshot startTransformSnapshot;

    protected bool IsRevealed => revealed;
    protected bool StartsHidden => startsHidden;
    protected bool MoveOnStartEnabled => moveOnStart;

    private bool ShowMoveDestination => moveOnStart || moveOnZone;
    private bool ShowFollowSettings => SupportsFollow && followPlayer;
    private bool ShowTimedFollowDuration => ShowFollowSettings && !alwaysFollow;
    private bool ShowTeleportDestination => SupportsTeleport && teleportOnZone;
    protected virtual bool SupportsMoveOnZone => true;
    protected virtual bool SupportsFollow => true;
    protected virtual bool SupportsTeleport => true;

    /// <summary>
    /// Caches controlled renderers and colliders under the visibility root.
    /// </summary>
    protected virtual void Awake()
    {
        Transform root = visibilityRoot != null ? visibilityRoot : transform;
        controlledColliders = root.GetComponentsInChildren<Collider2D>(true);
        controlledRenderers = root.GetComponentsInChildren<Renderer>(true);
        initialColliderEnabledStates = CaptureColliderStates();
        initialRendererEnabledStates = CaptureRendererStates();
        startTransformSnapshot = CaptureTransform(GetMoveTarget());
    }

    /// <summary>
    /// Applies the starting visibility and starts active behavior for visible obstacles.
    /// </summary>
    protected virtual void Start()
    {
        revealed = !startsHidden;
        ApplyVisibility(revealed);

        if (revealed)
        {
            OnRevealed(null);
        }
    }

    /// <summary>
    /// Updates follow movement while a follow target is active.
    /// </summary>
    protected virtual void Update()
    {
        UpdateFollowMovement();
    }

    /// <summary>
    /// Stops active movement tweens before Unity disables this obstacle.
    /// </summary>
    protected virtual void OnDisable()
    {
        KillMoveTween();
        followTarget = null;
    }

    /// <summary>
    /// Stops active movement tweens before Unity destroys this obstacle.
    /// </summary>
    protected virtual void OnDestroy()
    {
        KillMoveTween();
    }

    /// <summary>
    /// Handles trigger contact from the player.
    /// </summary>
    protected virtual void OnTriggerEnter2D(Collider2D other)
    {
        if (IsPlayer(other.gameObject))
        {
            OnPlayerContact(other.gameObject);
        }
    }

    /// <summary>
    /// Handles collision contact from the player.
    /// </summary>
    protected virtual void OnCollisionEnter2D(Collision2D collision)
    {
        if (IsPlayer(collision.gameObject))
        {
            OnPlayerContact(collision.gameObject);
        }
    }

    /// <summary>
    /// Reveals the obstacle and starts reveal-time behavior.
    /// </summary>
    public void Reveal(GameObject revealingPlayer = null)
    {
        if (revealed)
        {
            return;
        }

        revealed = true;
        ApplyVisibility(true);
        OnRevealed(revealingPlayer);
    }

    /// <summary>
    /// Hides the obstacle and stops follow behavior.
    /// </summary>
    public void Hide()
    {
        revealed = false;
        followTarget = null;
        ApplyVisibility(false);
        OnHidden();
    }

    /// <summary>
    /// Hides the obstacle after a reveal-zone exit when it is allowed to hide again.
    /// </summary>
    public void HideFromRevealZone()
    {
        if (!stayRevealed)
        {
            Hide();
        }
    }

    /// <summary>
    /// Starts configured zone movement when move-on-zone is enabled.
    /// </summary>
    public void TriggerMove()
    {
        if (!moveOnZone)
        {
            return;
        }

        MoveToDestination();
    }

    /// <summary>
    /// Teleports the movement target when teleport-on-zone is enabled.
    /// </summary>
    public void TriggerTeleport()
    {
        if (!teleportOnZone || teleportDestination == null)
        {
            return;
        }

        GetMoveTarget().position = teleportDestination.position;
    }

    /// <summary>
    /// Starts following the given player transform when follow movement is enabled.
    /// </summary>
    public void StartFollowing(Transform playerTransform)
    {
        if (!followPlayer || playerTransform == null)
        {
            return;
        }

        followTarget = playerTransform;
        followTimer = alwaysFollow ? 0f : followDuration;
    }

    /// <summary>
    /// Runs obstacle-specific shoot behavior when a trigger zone requests it.
    /// </summary>
    public virtual void TriggerShoot(GameObject targetPlayer)
    {
    }

    /// <summary>
    /// Runs obstacle-specific direction change behavior when a trigger zone requests it.
    /// </summary>
    public virtual void TriggerDirectionChange()
    {
    }

    /// <summary>
    /// Runs obstacle-specific move-away behavior when a trigger zone requests it.
    /// </summary>
    public virtual void TriggerMoveAway()
    {
    }

    /// <summary>
    /// Runs obstacle-specific return behavior when a trigger zone requests it.
    /// </summary>
    public virtual void TriggerReturn()
    {
    }

    /// <summary>
    /// Restores visibility, movement target, tweens, and follow state to their level-start values.
    /// </summary>
    public virtual void ResetLevelState()
    {
        KillMoveTween();
        followTarget = null;
        RestoreTransform(GetMoveTarget(), startTransformSnapshot);
        revealed = !startsHidden;
        ApplyVisibility(revealed);

        if (revealed)
        {
            OnRevealed(null);
        }
    }

    /// <summary>
    /// Runs obstacle-specific player contact behavior.
    /// </summary>
    protected abstract void OnPlayerContact(GameObject player);

    /// <summary>
    /// Runs behavior that should start when the obstacle becomes revealed.
    /// </summary>
    protected virtual void OnRevealed(GameObject revealingPlayer)
    {
        if (startsHidden && moveOnStart)
        {
            MoveToDestination();
        }

        if (revealingPlayer != null)
        {
            StartFollowing(revealingPlayer.transform);
        }
    }

    /// <summary>
    /// Runs behavior that should stop when the obstacle becomes hidden.
    /// </summary>
    protected virtual void OnHidden()
    {
    }

    /// <summary>
    /// Enables or disables controlled renderers and colliders.
    /// </summary>
    protected void ApplyVisibility(bool visible)
    {
        if (controlRenderers)
        {
            ApplyControlledRendererVisibility(visible);
        }

        if (controlColliders)
        {
            ApplyControlledColliderVisibility(visible);
        }
    }

    /// <summary>
    /// Enables or disables controlled renderers while respecting their level-start enabled state.
    /// </summary>
    protected void ApplyControlledRendererVisibility(bool visible)
    {
        for (int i = 0; i < controlledRenderers.Length; i++)
        {
            controlledRenderers[i].enabled = visible && initialRendererEnabledStates[i];
        }
    }

    /// <summary>
    /// Enables or disables controlled colliders while respecting their level-start enabled state.
    /// </summary>
    protected void ApplyControlledColliderVisibility(bool visible)
    {
        for (int i = 0; i < controlledColliders.Length; i++)
        {
            controlledColliders[i].enabled = visible && initialColliderEnabledStates[i];
        }
    }

    /// <summary>
    /// Returns the configured transform to move, falling back to this obstacle transform.
    /// </summary>
    protected Transform GetMoveTarget()
    {
        return transformToMove != null ? transformToMove : transform;
    }

    /// <summary>
    /// Checks whether a game object belongs to the configured player layer mask.
    /// </summary>
    protected bool IsPlayer(GameObject target)
    {
        return (playerLayer.value & (1 << target.layer)) != 0;
    }

    /// <summary>
    /// Moves the configured movement target to its destination.
    /// </summary>
    private void MoveToDestination()
    {
        if (moveDestination == null)
        {
            return;
        }

        KillMoveTween();
        moveTween = GetMoveTarget().DOMove(moveDestination.position, moveDuration).SetEase(moveEase);
    }

    /// <summary>
    /// Moves the obstacle toward the followed player while follow movement is active.
    /// </summary>
    private void UpdateFollowMovement()
    {
        if (followTarget == null)
        {
            return;
        }

        Transform moveTarget = GetMoveTarget();
        moveTarget.position = Vector3.MoveTowards(moveTarget.position, followTarget.position, followSpeed * Time.deltaTime);

        if (alwaysFollow)
        {
            return;
        }

        followTimer -= Time.deltaTime;
        if (followTimer <= 0f)
        {
            followTarget = null;
        }
    }

    /// <summary>
    /// Kills the active movement tween when one exists.
    /// </summary>
    protected void KillMoveTween()
    {
        if (moveTween == null || !moveTween.IsActive())
        {
            return;
        }

        moveTween.Kill();
        moveTween = null;
    }

    /// <summary>
    /// Captures the enabled state of every controlled collider.
    /// </summary>
    private bool[] CaptureColliderStates()
    {
        bool[] states = new bool[controlledColliders.Length];

        for (int i = 0; i < controlledColliders.Length; i++)
        {
            states[i] = controlledColliders[i].enabled;
        }

        return states;
    }

    /// <summary>
    /// Captures the enabled state of every controlled renderer.
    /// </summary>
    private bool[] CaptureRendererStates()
    {
        bool[] states = new bool[controlledRenderers.Length];

        for (int i = 0; i < controlledRenderers.Length; i++)
        {
            states[i] = controlledRenderers[i].enabled;
        }

        return states;
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
