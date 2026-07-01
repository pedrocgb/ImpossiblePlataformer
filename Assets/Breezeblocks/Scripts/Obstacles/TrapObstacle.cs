using UnityEngine;

public sealed class TrapObstacle : RevealableObstacle
{
    /// <summary>
    /// Kills the player when the player touches the trap.
    /// </summary>
    protected override void OnPlayerContact(GameObject player)
    {
        LevelGameManager.Current?.RegisterDeath(player);
    }
}
