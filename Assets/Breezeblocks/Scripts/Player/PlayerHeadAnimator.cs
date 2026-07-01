using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Animates the player's head with a vertical spring so it feels physical without using extra physics bodies.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public sealed class PlayerHeadAnimator : MonoBehaviour, ILevelResettable
{
    [TitleGroup("References")]
    [SerializeField, Required]
    private Transform headTransform;

    [SerializeField]
    [Tooltip("Optional SpriteRenderer on the head. Flip X false means right, true means left.")]
    private SpriteRenderer headSpriteRenderer;

    [TitleGroup("References")]
    [SerializeField]
    [Tooltip("Optional point on the body where the head should rest. Leave empty to use the head's starting height.")]
    private Transform bodyRestAnchor;

    [TitleGroup("State Detection")]
    [SerializeField, MinValue(0f)]
    private float moveSpeedThreshold = 0.15f;

    [SerializeField, MinValue(0f)]
    private float jumpVelocityThreshold = 0.1f;

    [SerializeField, MinValue(0f)]
    private float fallVelocityThreshold = 0.1f;

    [TitleGroup("Walk Bob")]
    [SerializeField, MinValue(0f)]
    private float walkBobAmplitude = 0.08f;

    [SerializeField, MinValue(0f)]
    private float walkBobFrequency = 8f;

    [TitleGroup("Fall Detach")]
    [SerializeField, MinValue(0f)]
    private float fallDetachMultiplier = 0.035f;

    [SerializeField, MinValue(0f)]
    private float maxFallDetachOffset = 0.22f;

    [TitleGroup("Spring")]
    [SerializeField, MinValue(0.01f)]
    private float springStrength = 85f;

    [SerializeField, MinValue(0f)]
    private float damping = 15f;

    [SerializeField, MinValue(0.01f)]
    private float glueSpringStrength = 150f;

    [SerializeField, MinValue(0f)]
    private float glueDamping = 24f;

    [SerializeField, MinValue(0.01f)]
    private float fallSpringStrength = 45f;

    [SerializeField, MinValue(0f)]
    private float fallDamping = 9f;

    [TitleGroup("Limits")]
    [SerializeField]
    private float minVerticalOffset = -0.04f;

    [SerializeField, MinValue(0f)]
    private float maxVerticalOffset = 0.28f;

    private Rigidbody2D playerBody;
    private PlayerMovement playerMovement;
    private Transform headParent;
    private Vector3 startRootLocalPosition;
    private Quaternion startLocalRotation;
    private Vector3 startLocalScale;
    private float springOffset;
    private float springVelocity;
    private float walkPhase;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private HeadMotionState currentState;

    private enum HeadMotionState
    {
        Idle,
        Moving,
        Jumping,
        Falling
    }

    /// <summary>
    /// Caches same-object components and captures the head's starting local state.
    /// </summary>
    private void Awake()
    {
        playerBody = GetComponent<Rigidbody2D>();
        playerMovement = GetComponent<PlayerMovement>();

        if (headTransform == null)
        {
            enabled = false;
            Debug.LogWarning("PlayerHeadAnimator disabled because Head Transform is not assigned.", this);
            return;
        }

        headParent = headTransform.parent;
        startRootLocalPosition = transform.InverseTransformPoint(headTransform.position);
        startLocalRotation = headTransform.localRotation;
        startLocalScale = headTransform.localScale;
        ApplyHeadPosition(0f);
    }

    /// <summary>
    /// Updates the cosmetic head spring after movement has updated the player body.
    /// </summary>
    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        Vector2 bodyVelocity = playerBody.linearVelocity;
        currentState = ResolveMotionState(bodyVelocity);
        UpdateHeadFacing(bodyVelocity);
        float targetOffset = GetTargetOffset(currentState, bodyVelocity);
        GetSpringSettings(currentState, out float activeSpring, out float activeDamping);
        UpdateSpring(targetOffset, activeSpring, activeDamping, deltaTime);
        ApplyHeadPosition(springOffset);
    }

    /// <summary>
    /// Restores the head spring and transform to the level-start state.
    /// </summary>
    public void ResetLevelState()
    {
        springOffset = 0f;
        springVelocity = 0f;
        walkPhase = 0f;
        currentState = HeadMotionState.Idle;
        headTransform.localRotation = startLocalRotation;
        headTransform.localScale = startLocalScale;
        ApplyHeadPosition(0f);
    }

    /// <summary>
    /// Flips the head sprite to face the latest movement direction.
    /// </summary>
    private void UpdateHeadFacing(Vector2 bodyVelocity)
    {
        if (headSpriteRenderer == null)
        {
            return;
        }

        int facingDirection = playerMovement != null ? playerMovement.FacingDirection : GetVelocityFacingDirection(bodyVelocity);
        headSpriteRenderer.flipX = facingDirection < 0;
    }

    /// <summary>
    /// Derives facing direction from velocity when no PlayerMovement component is available.
    /// </summary>
    private static int GetVelocityFacingDirection(Vector2 bodyVelocity)
    {
        if (bodyVelocity.x < 0f)
        {
            return -1;
        }

        return 1;
    }

    /// <summary>
    /// Chooses the visual head state from grounded state and Rigidbody2D velocity.
    /// </summary>
    private HeadMotionState ResolveMotionState(Vector2 bodyVelocity)
    {
        bool isGrounded = playerMovement == null || playerMovement.IsGrounded;

        if (!isGrounded && bodyVelocity.y > jumpVelocityThreshold)
        {
            return HeadMotionState.Jumping;
        }

        if (!isGrounded && bodyVelocity.y < -fallVelocityThreshold)
        {
            return HeadMotionState.Falling;
        }

        if (isGrounded && Mathf.Abs(bodyVelocity.x) > moveSpeedThreshold)
        {
            return HeadMotionState.Moving;
        }

        return HeadMotionState.Idle;
    }

    /// <summary>
    /// Calculates the desired vertical head offset for the current motion state.
    /// </summary>
    private float GetTargetOffset(HeadMotionState state, Vector2 bodyVelocity)
    {
        if (state == HeadMotionState.Moving)
        {
            walkPhase += Time.deltaTime * walkBobFrequency * Mathf.Max(1f, Mathf.Abs(bodyVelocity.x));
            return Mathf.Sin(walkPhase) * walkBobAmplitude;
        }

        if (state == HeadMotionState.Falling)
        {
            return Mathf.Clamp(-bodyVelocity.y * fallDetachMultiplier, 0f, maxFallDetachOffset);
        }

        return 0f;
    }

    /// <summary>
    /// Selects spring settings that fit the current head motion state.
    /// </summary>
    private void GetSpringSettings(HeadMotionState state, out float activeSpring, out float activeDamping)
    {
        if (state == HeadMotionState.Jumping || state == HeadMotionState.Idle)
        {
            activeSpring = glueSpringStrength;
            activeDamping = glueDamping;
            return;
        }

        if (state == HeadMotionState.Falling)
        {
            activeSpring = fallSpringStrength;
            activeDamping = fallDamping;
            return;
        }

        activeSpring = springStrength;
        activeDamping = damping;
    }

    /// <summary>
    /// Advances the damped spring toward the requested offset.
    /// </summary>
    private void UpdateSpring(float targetOffset, float activeSpring, float activeDamping, float deltaTime)
    {
        float displacement = targetOffset - springOffset;
        float acceleration = displacement * activeSpring - springVelocity * activeDamping;
        springVelocity += acceleration * deltaTime;
        springOffset += springVelocity * deltaTime;
        springOffset = Mathf.Clamp(springOffset, minVerticalOffset, maxVerticalOffset);
    }

    /// <summary>
    /// Applies the spring offset while preserving the head's starting horizontal position.
    /// </summary>
    private void ApplyHeadPosition(float verticalOffset)
    {
        Vector3 rootLocalPosition = startRootLocalPosition;
        rootLocalPosition.y = GetRestRootLocalY() + verticalOffset;

        Vector3 worldPosition = transform.TransformPoint(rootLocalPosition);

        if (headParent != null)
        {
            headTransform.localPosition = headParent.InverseTransformPoint(worldPosition);
            return;
        }

        headTransform.position = worldPosition;
    }

    /// <summary>
    /// Gets the body rest anchor height in player-root local space.
    /// </summary>
    private float GetRestRootLocalY()
    {
        if (bodyRestAnchor == null)
        {
            return startRootLocalPosition.y;
        }

        return transform.InverseTransformPoint(bodyRestAnchor.position).y;
    }
}
