using Sirenix.OdinInspector;
using UnityEngine;

public sealed class PersistentDeathCounter : MonoBehaviour
{
    public static PersistentDeathCounter Current { get; private set; }

    [ShowInInspector]
    [ReadOnly]
    private int totalDeaths;

    /// <summary>
    /// Gets the saved all-time player death count.
    /// </summary>
    public int TotalDeaths => totalDeaths;

    /// <summary>
    /// Registers this object as the persistent death counter and loads saved data.
    /// </summary>
    private void Awake()
    {
        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;
        DontDestroyOnLoad(gameObject);
        totalDeaths = DeathSaveSystem.LoadTotalDeaths();
    }

    /// <summary>
    /// Clears the static instance reference when this counter is destroyed.
    /// </summary>
    private void OnDestroy()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Adds one death to the all-time death counter and saves it.
    /// </summary>
    public int RegisterDeath()
    {
        totalDeaths++;
        DeathSaveSystem.SaveTotalDeaths(totalDeaths);
        return totalDeaths;
    }

    /// <summary>
    /// Reloads the all-time death counter from saved data.
    /// </summary>
    public void Reload()
    {
        totalDeaths = DeathSaveSystem.LoadTotalDeaths();
    }

    /// <summary>
    /// Clears all saved deaths and resets runtime total deaths to zero.
    /// </summary>
    [Button]
    public void ClearDeaths()
    {
        totalDeaths = 0;
        DeathSaveSystem.ClearTotalDeaths();
    }
}
