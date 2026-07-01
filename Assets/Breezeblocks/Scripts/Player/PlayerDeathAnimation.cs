using Sirenix.OdinInspector;
using UnityEngine;

/// <summary>
/// Plays the player's death visual by hiding the body, spawning an effect, and releasing the head into physics.
/// </summary>
public sealed class PlayerDeathAnimation : MonoBehaviour, ILevelResettable
{
    [TitleGroup("Body")]
    [SerializeField, Required]
    [Tooltip("Assign the body child, not the head. All renderers under this root are hidden on death.")]
    private Transform bodyVisualRoot;

    [TitleGroup("Head Physics")]
    [SerializeField, Required]
    private Transform headTransform;

    [SerializeField]
    private Rigidbody2D headRigidbody;

    [SerializeField]
    private Collider2D headCollider;

    [SerializeField]
    private bool disableHeadPhysicsUntilDeath = true;

    [SerializeField, MinValue(0f)]
    private float headGravityScale = 2f;

    [SerializeField]
    private Vector2 headLaunchVelocity = new Vector2(0f, 4f);

    [SerializeField]
    private float headAngularVelocity = 360f;

    [TitleGroup("Spawn")]
    [SerializeField]
    private GameObject deathSpawnPrefab;

    [SerializeField]
    private Transform deathSpawnParent;

    [SerializeField]
    private Vector3 deathSpawnOffset;

    private Renderer[] bodyRenderers = new Renderer[0];
    private bool[] bodyRendererStartStates = new bool[0];
    private PlayerHeadAnimator headAnimator;
    private Rigidbody2D playerRigidbody;
    private Transform headStartParent;
    private int headStartSiblingIndex;
    private Vector3 headStartLocalPosition;
    private Quaternion headStartLocalRotation;
    private Vector3 headStartLocalScale;
    private bool headRigidbodyStartSimulated;
    private float headRigidbodyStartGravityScale;
    private RigidbodyType2D headRigidbodyStartBodyType;
    private bool headColliderStartEnabled;
    private GameObject spawnedDeathObject;
    private bool deathPlayed;

    /// <summary>
    /// Caches local components and stores the visual state needed for level reset.
    /// </summary>
    private void Awake()
    {
        headAnimator = GetComponent<PlayerHeadAnimator>();
        playerRigidbody = GetComponent<Rigidbody2D>();
        CacheBodyRenderers();
        CacheHeadState();
        DisableHeadPhysics();
    }

    /// <summary>
    /// Hides the body, spawns the configured effect, and releases the head as a short-lived physics object.
    /// </summary>
    public void PlayDeath()
    {
        if (deathPlayed)
        {
            return;
        }

        deathPlayed = true;
        SetBodyVisible(false);
        SpawnDeathObject();
        ReleaseHead();
    }

    /// <summary>
    /// Restores the body, head transform, spawned effect, and dormant head physics state.
    /// </summary>
    public void ResetLevelState()
    {
        deathPlayed = false;
        DestroySpawnedDeathObject();
        RestoreBodyRenderers();
        RestoreHeadTransform();
        DisableHeadPhysics();

        if (headAnimator != null)
        {
            headAnimator.enabled = true;
            headAnimator.ResetLevelState();
        }
    }

    /// <summary>
    /// Caches all body renderers under the assigned body visual root.
    /// </summary>
    private void CacheBodyRenderers()
    {
        if (bodyVisualRoot == null)
        {
            return;
        }

        bodyRenderers = bodyVisualRoot.GetComponentsInChildren<Renderer>(true);
        bodyRendererStartStates = new bool[bodyRenderers.Length];

        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            bodyRendererStartStates[i] = bodyRenderers[i].enabled;
        }
    }

    /// <summary>
    /// Stores head hierarchy, transform, and physics settings before death changes them.
    /// </summary>
    private void CacheHeadState()
    {
        if (headTransform == null)
        {
            return;
        }

        headStartParent = headTransform.parent;
        headStartSiblingIndex = headTransform.GetSiblingIndex();
        headStartLocalPosition = headTransform.localPosition;
        headStartLocalRotation = headTransform.localRotation;
        headStartLocalScale = headTransform.localScale;

        if (headRigidbody != null)
        {
            headRigidbodyStartSimulated = headRigidbody.simulated;
            headRigidbodyStartGravityScale = headRigidbody.gravityScale;
            headRigidbodyStartBodyType = headRigidbody.bodyType;
        }

        if (headCollider != null)
        {
            headColliderStartEnabled = headCollider.enabled;
        }
    }

    /// <summary>
    /// Enables or disables every cached body renderer.
    /// </summary>
    private void SetBodyVisible(bool isVisible)
    {
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            bodyRenderers[i].enabled = isVisible && bodyRendererStartStates[i];
        }
    }

    /// <summary>
    /// Restores body renderer enabled states captured at level start.
    /// </summary>
    private void RestoreBodyRenderers()
    {
        for (int i = 0; i < bodyRenderers.Length; i++)
        {
            bodyRenderers[i].enabled = bodyRendererStartStates[i];
        }
    }

    /// <summary>
    /// Creates the optional death object at the player's death position.
    /// </summary>
    private void SpawnDeathObject()
    {
        if (deathSpawnPrefab == null)
        {
            return;
        }

        spawnedDeathObject = Instantiate(deathSpawnPrefab, transform.position + deathSpawnOffset, Quaternion.identity, deathSpawnParent);
    }

    /// <summary>
    /// Destroys the spawned death object before resetting the level visuals.
    /// </summary>
    private void DestroySpawnedDeathObject()
    {
        if (spawnedDeathObject == null)
        {
            return;
        }

        Destroy(spawnedDeathObject);
        spawnedDeathObject = null;
    }

    /// <summary>
    /// Lets the head fall using Rigidbody2D physics during the death reset delay.
    /// </summary>
    private void ReleaseHead()
    {
        if (headTransform == null)
        {
            return;
        }

        if (headAnimator != null)
        {
            headAnimator.enabled = false;
        }

        headTransform.SetParent(null, true);

        if (headCollider != null)
        {
            headCollider.enabled = true;
        }

        if (headRigidbody == null)
        {
            return;
        }

        Vector2 inheritedVelocity = playerRigidbody != null ? playerRigidbody.linearVelocity : Vector2.zero;
        headRigidbody.bodyType = RigidbodyType2D.Dynamic;
        headRigidbody.gravityScale = headGravityScale;
        headRigidbody.simulated = true;
        headRigidbody.linearVelocity = inheritedVelocity + headLaunchVelocity;
        headRigidbody.angularVelocity = headAngularVelocity;
    }

    /// <summary>
    /// Restores the head to the original parent and local transform.
    /// </summary>
    private void RestoreHeadTransform()
    {
        if (headTransform == null)
        {
            return;
        }

        headTransform.SetParent(headStartParent, false);
        headTransform.SetSiblingIndex(headStartSiblingIndex);
        headTransform.localPosition = headStartLocalPosition;
        headTransform.localRotation = headStartLocalRotation;
        headTransform.localScale = headStartLocalScale;
    }

    /// <summary>
    /// Returns head Rigidbody2D and Collider2D to their dormant starting settings.
    /// </summary>
    private void DisableHeadPhysics()
    {
        if (headRigidbody != null)
        {
            headRigidbody.linearVelocity = Vector2.zero;
            headRigidbody.angularVelocity = 0f;
            headRigidbody.bodyType = headRigidbodyStartBodyType;
            headRigidbody.gravityScale = headRigidbodyStartGravityScale;
            headRigidbody.simulated = disableHeadPhysicsUntilDeath ? false : headRigidbodyStartSimulated;
        }

        if (headCollider != null)
        {
            headCollider.enabled = disableHeadPhysicsUntilDeath ? false : headColliderStartEnabled;
        }
    }
}
