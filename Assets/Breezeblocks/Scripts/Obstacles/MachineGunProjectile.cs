using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public sealed class MachineGunProjectile : MonoBehaviour
{
    private Rigidbody2D projectileBody;
    private LayerMask playerLayer;
    private LayerMask blockingLayer;
    private MachineGunBulletEffect bulletEffect;
    private GameObject hitPrefab;
    private Vector2 travelDirection;
    private float pushForce;
    private bool hasHit;

    /// <summary>
    /// Caches same-object physics components used by the projectile.
    /// </summary>
    private void Awake()
    {
        projectileBody = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// Initializes projectile velocity and hit behavior after spawning.
    /// </summary>
    public void Initialize(Vector2 direction, float speed, float lifetime, LayerMask playerMask, LayerMask blockMask, MachineGunBulletEffect effect, float pushAmount, GameObject bulletHitPrefab)
    {
        travelDirection = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        playerLayer = playerMask;
        blockingLayer = blockMask;
        bulletEffect = effect;
        pushForce = pushAmount;
        hitPrefab = bulletHitPrefab;
        projectileBody.linearVelocity = travelDirection * speed;

        if (lifetime > 0f)
        {
            Destroy(gameObject, lifetime);
        }
    }

    /// <summary>
    /// Handles trigger hits against players or blocking surfaces.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleHit(other.gameObject, transform.position, -travelDirection);
    }

    /// <summary>
    /// Handles collision hits against players or blocking surfaces.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        Vector2 hitNormal = collision.contactCount > 0 ? collision.GetContact(0).normal : -travelDirection;
        Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        HandleHit(collision.gameObject, hitPoint, hitNormal);
    }

    /// <summary>
    /// Applies the bullet effect or hit effect when the projectile reaches a valid target.
    /// </summary>
    private void HandleHit(GameObject hitObject, Vector3 hitPoint, Vector2 hitNormal)
    {
        if (hasHit || hitObject == null)
        {
            return;
        }

        if (IsInLayer(hitObject, playerLayer))
        {
            hasHit = true;
            MachineGunBulletEffectUtility.ApplyToPlayer(hitObject, bulletEffect, travelDirection, pushForce);
            MachineGunBulletEffectUtility.SpawnHitEffect(hitPrefab, hitPoint, hitNormal);
            Destroy(gameObject);
            return;
        }

        if (IsInLayer(hitObject, blockingLayer))
        {
            hasHit = true;
            MachineGunBulletEffectUtility.SpawnHitEffect(hitPrefab, hitPoint, hitNormal);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Checks whether an object belongs to a layer mask.
    /// </summary>
    private static bool IsInLayer(GameObject target, LayerMask layerMask)
    {
        return (layerMask.value & (1 << target.layer)) != 0;
    }
}
