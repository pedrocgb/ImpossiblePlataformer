using Rewired;
using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Drives a tight 2D platformer character using Rigidbody2D physics and Celeste-inspired movement assists.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class PlayerMovement : MonoBehaviour, ILevelResettable
{
    private enum DashDirectionMode
    {
        [InspectorName("No Dash")]
        NoDash,

        [InspectorName("Horizontal Dash")]
        HorizontalDash,

        [InspectorName("Vertical Dash")]
        VerticalDash,

        [InspectorName("All Directions Dash")]
        AllDirectionsDash
    }

    private bool CanDash => dashDirectionMode != DashDirectionMode.NoDash;
    private bool CanDashVertically => dashDirectionMode == DashDirectionMode.VerticalDash || dashDirectionMode == DashDirectionMode.AllDirectionsDash;

    [TitleGroup("Input")]
    [BoxGroup("Input/Rewired")]
    [SerializeField, LabelText("Use Rewired Input")]
    private bool useRewiredInput = true;

    [BoxGroup("Input/Rewired")]
    [SerializeField, MinValue(0), ShowIf(nameof(useRewiredInput))]
    private int rewiredPlayerId;

    [BoxGroup("Input/Actions")]
    [SerializeField, ShowIf(nameof(useRewiredInput))]
    private string horizontalAction = "Move Horizontal";

    [BoxGroup("Input/Actions")]
    [SerializeField, ShowIf(nameof(useRewiredInput))]
    private string verticalAction = "Move Vertical";

    [BoxGroup("Input/Actions")]
    [SerializeField, ShowIf(nameof(useRewiredInput))]
    private string jumpAction = "Jump";

    [BoxGroup("Input/Actions")]
    [SerializeField, ShowIf("@useRewiredInput && CanDash")]
    private string dashAction = "Dash";

    [TitleGroup("Collision")]
    [SerializeField, Tooltip("Layers treated as solid ground by the ground probe.")]
    private LayerMask groundLayer = ~0;

    [TitleGroup("Collision")]
    [SerializeField, MinValue(0.001f)]
    private float groundCheckDistance = 0.04f;

    [TitleGroup("Horizontal Movement")]
    [SerializeField, MinValue(0f)]
    private float maxRunSpeed = 5.625f;

    [TitleGroup("Horizontal Movement")]
    [SerializeField, MinValue(0f)]
    private float runAcceleration = 62.5f;

    [TitleGroup("Horizontal Movement")]
    [SerializeField, MinValue(0f)]
    private float runDeceleration = 25f;

    [TitleGroup("Horizontal Movement")]
    [SerializeField, Range(0f, 1f)]
    private float airControlMultiplier = 0.65f;

    [TitleGroup("Horizontal Movement")]
    [SerializeField, Range(0f, 1f)]
    private float inputDeadZone = 0.15f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float jumpSpeed = 6.5625f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float gravity = 56.25f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float halfGravityThreshold = 2.5f;

    [TitleGroup("Jumping")]
    [SerializeField, Range(0f, 1f)]
    private float jumpCutMultiplier = 0.5f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float variableJumpDuration = 0.2f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float maxFallSpeed = 10f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float fastFallSpeed = 15f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float fastFallAcceleration = 18.75f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float coyoteTime = 0.1f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0f)]
    private float jumpBufferTime = 0.1f;

    [TitleGroup("Jumping")]
    [SerializeField, MinValue(0)]
    private int extraAirJumps = 1;

    [TitleGroup("Dash")]
    [SerializeField, EnumToggleButtons]
    private DashDirectionMode dashDirectionMode = DashDirectionMode.AllDirectionsDash;

    [TitleGroup("Dash")]
    [SerializeField, MinValue(0f), ShowIf(nameof(CanDash))]
    private float dashSpeed = 15f;

    [TitleGroup("Dash")]
    [SerializeField, MinValue(0f), ShowIf(nameof(CanDash))]
    private float dashDuration = 0.15f;

    [TitleGroup("Dash")]
    [SerializeField, MinValue(0f), ShowIf(nameof(CanDash))]
    private float dashCooldown = 0.2f;

    [TitleGroup("Dash")]
    [SerializeField, MinValue(0f), ShowIf(nameof(CanDash))]
    private float dashEndSpeed = 10f;

    [TitleGroup("Dash")]
    [SerializeField, Range(0f, 1f), ShowIf(nameof(CanDashVertically))]
    private float upwardDashEndMultiplier = 0.75f;

    [TitleGroup("Dash")]
    [SerializeField, MinValue(0f), ShowIf(nameof(CanDash))]
    private float dashBufferTime = 0.1f;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[4];

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private ContactFilter2D groundFilter;
    private Rewired.Player rewiredPlayer;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private bool controlsEnabled = true;
    private bool bodyColliderStartEnabled;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private Vector2 moveInput;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private Vector2 velocity;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private Vector2 dashDirection;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float coyoteTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float jumpBufferTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float dashBufferTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float dashTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float dashCooldownTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private float variableJumpTimer;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private int airJumpsRemaining;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private int dashesRemaining;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private int facingDirection = 1;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private bool isGrounded;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private bool isDashing;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private bool jumpHeld;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private bool jumpReleasedQueued;

    [FoldoutGroup("Runtime Data")]
    [ShowInInspector, ReadOnly]
    private bool ControlsEnabled => controlsEnabled;

    /// <summary>
    /// Gets whether the controller detected ground during the latest physics step.
    /// </summary>
    public bool IsGrounded => isGrounded;

    /// <summary>
    /// Gets whether the controller is currently inside the fixed-duration dash state.
    /// </summary>
    public bool IsDashing => isDashing;

    /// <summary>
    /// Gets the latest horizontal facing direction, where 1 is right and -1 is left.
    /// </summary>
    public int FacingDirection => facingDirection;

    /// <summary>
    /// Caches required components and prepares Rigidbody2D for script-driven platformer motion.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        bodyCollider = GetComponent<Collider2D>();
        startPosition = transform.position;
        startRotation = transform.rotation;
        bodyColliderStartEnabled = bodyCollider.enabled;
        body.gravityScale = 0f;
        body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        airJumpsRemaining = extraAirJumps;
        dashesRemaining = 1;
        BuildGroundFilter();
    }

    /// <summary>
    /// Resolves the configured Rewired player after Rewired has initialized.
    /// </summary>
    private void Start()
    {
        CacheRewiredPlayer();
    }

    /// <summary>
    /// Keeps collision filters current when inspector values change during play mode.
    /// </summary>
    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            BuildGroundFilter();
        }
    }

    /// <summary>
    /// Advances buffered input and cooldown timers using render-frame time for responsive button windows.
    /// </summary>
    private void Update()
    {
        if (!controlsEnabled)
        {
            return;
        }

        ReadRewiredInput();

        float deltaTime = Time.deltaTime;
        jumpBufferTimer = Mathf.Max(0f, jumpBufferTimer - deltaTime);
        dashBufferTimer = Mathf.Max(0f, dashBufferTimer - deltaTime);
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - deltaTime);
    }

    /// <summary>
    /// Applies platformer movement, jumps, dashes, and gravity during the physics tick.
    /// </summary>
    private void FixedUpdate()
    {
        if (!controlsEnabled)
        {
            body.linearVelocity = Vector2.zero;
            return;
        }

        float fixedDeltaTime = Time.fixedDeltaTime;
        velocity = body.linearVelocity;

        RefreshGroundedState(fixedDeltaTime);
        TryStartDash();

        if (isDashing)
        {
            RunDash(fixedDeltaTime);
            CommitVelocity();
            ClearFrameInput();
            return;
        }

        TryStartJump();
        ApplyHorizontalMovement(fixedDeltaTime);
        ApplyJumpCut();
        ApplyGravity(fixedDeltaTime);
        CommitVelocity();
        TickJumpTimers(fixedDeltaTime);
        ClearFrameInput();
    }

    /// <summary>
    /// Supplies normalized movement input from an external input reader such as a Rewired adapter.
    /// </summary>
    public void SetMoveInput(Vector2 input)
    {
        moveInput = Vector2.ClampMagnitude(input, 1f);

        if (Mathf.Abs(moveInput.x) > inputDeadZone)
        {
            facingDirection = moveInput.x > 0f ? 1 : -1;
        }
    }

    /// <summary>
    /// Updates whether jump is currently held so variable-height jumps can stay responsive.
    /// </summary>
    public void SetJumpHeld(bool isHeld)
    {
        jumpHeld = isHeld;
    }

    /// <summary>
    /// Buffers a jump press for the next valid coyote, grounded, or air-jump opportunity.
    /// </summary>
    public void PressJump()
    {
        jumpHeld = true;
        jumpBufferTimer = jumpBufferTime;
    }

    /// <summary>
    /// Marks jump as released so upward velocity can be cut for variable-height jumps.
    /// </summary>
    public void ReleaseJump()
    {
        jumpHeld = false;
        jumpReleasedQueued = true;
    }

    /// <summary>
    /// Buffers a dash press for the next available dash window.
    /// </summary>
    public void PressDash()
    {
        if (!CanDash)
        {
            return;
        }

        dashBufferTimer = dashBufferTime;
    }

    /// <summary>
    /// Updates all movement inputs in one call for input readers that sample every frame.
    /// </summary>
    public void SetInput(Vector2 input, bool isJumpHeld, bool wasJumpPressed, bool wasJumpReleased, bool wasDashPressed)
    {
        SetMoveInput(input);
        SetJumpHeld(isJumpHeld);

        if (wasJumpPressed)
        {
            PressJump();
        }

        if (wasJumpReleased)
        {
            ReleaseJump();
        }

        if (wasDashPressed)
        {
            PressDash();
        }
    }

    /// <summary>
    /// Enables or disables player control while preserving the movement component for reset.
    /// </summary>
    public void SetControlEnabled(bool isEnabled)
    {
        controlsEnabled = isEnabled;
        bodyCollider.enabled = isEnabled && bodyColliderStartEnabled;

        if (!controlsEnabled)
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            ResetMovementState();
        }
    }

    /// <summary>
    /// Restores the player to the level-start position and clears all movement state.
    /// </summary>
    public void ResetLevelState()
    {
        transform.SetPositionAndRotation(startPosition, startRotation);
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        controlsEnabled = true;
        bodyCollider.enabled = bodyColliderStartEnabled;
        ResetMovementState();
    }

    /// <summary>
    /// Caches the Rewired player requested by the inspector settings when Rewired is available.
    /// </summary>
    private void CacheRewiredPlayer()
    {
        if (!useRewiredInput || !ReInput.isReady)
        {
            rewiredPlayer = null;
            return;
        }

        rewiredPlayer = ReInput.players.GetPlayer(rewiredPlayerId);
    }

    /// <summary>
    /// Samples Rewired actions and forwards them into the movement input buffering layer.
    /// </summary>
    private void ReadRewiredInput()
    {
        if (!useRewiredInput)
        {
            return;
        }

        if (rewiredPlayer == null)
        {
            CacheRewiredPlayer();
        }

        if (rewiredPlayer == null)
        {
            return;
        }

        SetInput(
            new Vector2(rewiredPlayer.GetAxis(horizontalAction), rewiredPlayer.GetAxis(verticalAction)),
            rewiredPlayer.GetButton(jumpAction),
            rewiredPlayer.GetButtonDown(jumpAction),
            rewiredPlayer.GetButtonUp(jumpAction),
            CanDash && rewiredPlayer.GetButtonDown(dashAction));
    }

    /// <summary>
    /// Rebuilds the reusable contact filter used by ground checks.
    /// </summary>
    private void BuildGroundFilter()
    {
        groundFilter = new ContactFilter2D
        {
            useLayerMask = true,
            useTriggers = false,
            layerMask = groundLayer
        };
    }

    /// <summary>
    /// Checks whether the collider is touching walkable ground and refills grounded movement resources.
    /// </summary>
    private void RefreshGroundedState(float fixedDeltaTime)
    {
        bool wasGrounded = isGrounded;
        isGrounded = bodyCollider.Cast(Vector2.down, groundFilter, groundHits, groundCheckDistance) > 0;

        if (isGrounded)
        {
            coyoteTimer = coyoteTime;
            airJumpsRemaining = extraAirJumps;
            dashesRemaining = 1;

            if (!wasGrounded && velocity.y < 0f)
            {
                velocity.y = 0f;
            }
        }
        else
        {
            coyoteTimer = Mathf.Max(0f, coyoteTimer - fixedDeltaTime);
        }
    }

    /// <summary>
    /// Starts a dash when buffered input and dash resources are both available.
    /// </summary>
    private void TryStartDash()
    {
        if (!CanDash || dashBufferTimer <= 0f || dashCooldownTimer > 0f || dashesRemaining <= 0)
        {
            return;
        }

        dashDirection = GetDashDirection();

        if (dashDirection.sqrMagnitude <= 0f)
        {
            return;
        }

        velocity = dashDirection * dashSpeed;
        dashTimer = dashDuration;
        dashCooldownTimer = dashCooldown;
        dashBufferTimer = 0f;
        variableJumpTimer = 0f;
        isDashing = true;
        dashesRemaining--;
    }

    /// <summary>
    /// Holds dash velocity for the dash duration, then exits into a controlled post-dash speed.
    /// </summary>
    private void RunDash(float fixedDeltaTime)
    {
        velocity = dashDirection * dashSpeed;
        dashTimer -= fixedDeltaTime;

        if (dashTimer > 0f)
        {
            return;
        }

        isDashing = false;
        velocity = dashDirection * dashEndSpeed;

        if (dashDirection.y > 0f)
        {
            velocity.y *= upwardDashEndMultiplier;
        }
    }

    /// <summary>
    /// Chooses a dash direction that matches the configured dash mode.
    /// </summary>
    private Vector2 GetDashDirection()
    {
        if (dashDirectionMode == DashDirectionMode.NoDash)
        {
            return Vector2.zero;
        }

        Vector2 rawDirection = moveInput;

        if (Mathf.Abs(rawDirection.x) <= inputDeadZone)
        {
            rawDirection.x = 0f;
        }

        if (Mathf.Abs(rawDirection.y) <= inputDeadZone)
        {
            rawDirection.y = 0f;
        }

        if (dashDirectionMode == DashDirectionMode.HorizontalDash)
        {
            return new Vector2(GetHorizontalDashDirection(rawDirection.x), 0f);
        }

        if (dashDirectionMode == DashDirectionMode.VerticalDash)
        {
            return GetVerticalDashDirection(rawDirection.y);
        }

        if (rawDirection.sqrMagnitude <= 0f)
        {
            return new Vector2(facingDirection, 0f);
        }

        return rawDirection.normalized;
    }

    /// <summary>
    /// Chooses a horizontal dash direction, using facing direction when no horizontal input is held.
    /// </summary>
    private float GetHorizontalDashDirection(float horizontalInput)
    {
        if (horizontalInput > 0f)
        {
            return 1f;
        }

        if (horizontalInput < 0f)
        {
            return -1f;
        }

        return facingDirection;
    }

    /// <summary>
    /// Chooses an up or down dash direction and cancels vertical dash when no vertical input is held.
    /// </summary>
    private Vector2 GetVerticalDashDirection(float verticalInput)
    {
        if (verticalInput > 0f)
        {
            return Vector2.up;
        }

        if (verticalInput < 0f)
        {
            return Vector2.down;
        }

        return Vector2.zero;
    }

    /// <summary>
    /// Starts a grounded, coyote, or air jump when a buffered jump is available.
    /// </summary>
    private void TryStartJump()
    {
        if (jumpBufferTimer <= 0f)
        {
            return;
        }

        if (isGrounded || coyoteTimer > 0f)
        {
            StartJump();
            coyoteTimer = 0f;
            return;
        }

        if (airJumpsRemaining <= 0)
        {
            return;
        }

        airJumpsRemaining--;
        StartJump();
    }

    /// <summary>
    /// Applies the shared jump launch velocity and variable-jump timing.
    /// </summary>
    private void StartJump()
    {
        jumpBufferTimer = 0f;
        variableJumpTimer = variableJumpDuration;
        velocity.y = jumpSpeed;
    }

    /// <summary>
    /// Moves horizontal velocity toward the desired run speed using stronger ground control than air control.
    /// </summary>
    private void ApplyHorizontalMovement(float fixedDeltaTime)
    {
        float horizontalInput = Mathf.Abs(moveInput.x) > inputDeadZone ? moveInput.x : 0f;
        float targetSpeed = horizontalInput * maxRunSpeed;
        float acceleration = Mathf.Abs(targetSpeed) > 0f ? runAcceleration : runDeceleration;

        if (!isGrounded)
        {
            acceleration *= airControlMultiplier;
        }

        velocity.x = Mathf.MoveTowards(velocity.x, targetSpeed, acceleration * fixedDeltaTime);
    }

    /// <summary>
    /// Cuts upward velocity after jump release to support short hops.
    /// </summary>
    private void ApplyJumpCut()
    {
        if (!jumpReleasedQueued || velocity.y <= 0f)
        {
            return;
        }

        velocity.y *= jumpCutMultiplier;
        variableJumpTimer = 0f;
    }

    /// <summary>
    /// Applies Celeste-style gravity, including jump-hold float and fast-fall terminal speed.
    /// </summary>
    private void ApplyGravity(float fixedDeltaTime)
    {
        float targetFallSpeed = moveInput.y < -inputDeadZone ? fastFallSpeed : maxFallSpeed;
        float gravityStep = gravity;

        if (moveInput.y < -inputDeadZone && velocity.y < -maxFallSpeed)
        {
            gravityStep = fastFallAcceleration;
        }

        if (jumpHeld && variableJumpTimer > 0f && velocity.y > 0f && velocity.y < halfGravityThreshold)
        {
            gravityStep *= 0.5f;
        }

        velocity.y = Mathf.MoveTowards(velocity.y, -targetFallSpeed, gravityStep * fixedDeltaTime);
    }

    /// <summary>
    /// Decrements fixed-step timers that control jump grace windows.
    /// </summary>
    private void TickJumpTimers(float fixedDeltaTime)
    {
        variableJumpTimer = Mathf.Max(0f, variableJumpTimer - fixedDeltaTime);
    }

    /// <summary>
    /// Writes the calculated velocity back to the Rigidbody2D.
    /// </summary>
    private void CommitVelocity()
    {
        body.linearVelocity = velocity;
    }

    /// <summary>
    /// Clears one-frame input flags after physics has consumed them.
    /// </summary>
    private void ClearFrameInput()
    {
        jumpReleasedQueued = false;
    }

    /// <summary>
    /// Clears runtime movement, input buffers, cooldowns, and resource counters.
    /// </summary>
    private void ResetMovementState()
    {
        moveInput = Vector2.zero;
        velocity = Vector2.zero;
        dashDirection = Vector2.zero;
        coyoteTimer = 0f;
        jumpBufferTimer = 0f;
        dashBufferTimer = 0f;
        dashTimer = 0f;
        dashCooldownTimer = 0f;
        variableJumpTimer = 0f;
        airJumpsRemaining = extraAirJumps;
        dashesRemaining = 1;
        facingDirection = 1;
        isGrounded = false;
        isDashing = false;
        jumpHeld = false;
        jumpReleasedQueued = false;
    }
}
