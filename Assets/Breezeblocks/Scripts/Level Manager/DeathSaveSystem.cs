using UnityEngine;

public static class DeathSaveSystem
{
    private const string TotalDeathsKey = "Breezeblocks.TotalDeaths";

    /// <summary>
    /// Loads the saved all-time death count from local player preferences.
    /// </summary>
    public static int LoadTotalDeaths()
    {
        return PlayerPrefs.GetInt(TotalDeathsKey, 0);
    }

    /// <summary>
    /// Saves the all-time death count to local player preferences.
    /// </summary>
    public static void SaveTotalDeaths(int totalDeaths)
    {
        PlayerPrefs.SetInt(TotalDeathsKey, Mathf.Max(0, totalDeaths));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Clears the saved all-time death count.
    /// </summary>
    public static void ClearTotalDeaths()
    {
        PlayerPrefs.DeleteKey(TotalDeathsKey);
        PlayerPrefs.Save();
    }
}
