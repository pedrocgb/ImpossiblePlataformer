using UnityEngine;

public enum MachineGunBulletEffect
{
    Kill,
    Push
}

public static class MachineGunBulletEffectUtility
{
    /// <summary>
    /// Applies the selected bullet effect to the player object hit by a machinegun bullet.
    /// </summary>
    public static void ApplyToPlayer(GameObject player, MachineGunBulletEffect effect, Vector2 direction, float pushForce)
    {
        if (player == null)
        {
            return;
        }

        if (effect == MachineGunBulletEffect.Kill)
        {
            LevelGameManager.Current?.RegisterDeath(player);
            return;
        }

        PlayerMovement movement = player.GetComponent<PlayerMovement>();

        if (movement != null)
        {
            movement.AddExternalImpulse(direction.normalized * pushForce);
        }
    }

    /// <summary>
    /// Instantiates a bullet hit effect aligned to the hit surface normal.
    /// </summary>
    public static void SpawnHitEffect(GameObject hitPrefab, Vector3 position, Vector2 normal)
    {
        if (hitPrefab == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal);
        Object.Instantiate(hitPrefab, position, rotation);
    }
}
