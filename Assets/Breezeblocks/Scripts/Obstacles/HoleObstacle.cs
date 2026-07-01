using UnityEngine;

public sealed class HoleObstacle : RevealableObstacle
{
    /// <summary>
    /// Kills the player when the player reaches the hole zone.
    /// </summary>
    protected override void OnPlayerContact(GameObject player)
    {
        LevelGameManager.Current?.RegisterDeath(player);
    }
}
