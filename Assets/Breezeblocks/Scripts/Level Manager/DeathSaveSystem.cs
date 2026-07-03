public static class DeathSaveSystem
{
    /// <summary>
    /// Loads the saved all-time death count from the project save file.
    /// </summary>
    public static int LoadTotalDeaths()
    {
        return GameSaveSystem.LoadTotalDeaths();
    }

    /// <summary>
    /// Saves the all-time death count to the project save file.
    /// </summary>
    public static void SaveTotalDeaths(int totalDeaths)
    {
        GameSaveSystem.SaveTotalDeaths(totalDeaths);
    }

    /// <summary>
    /// Loads the all-time death count saved for one level.
    /// </summary>
    public static int LoadLevelDeaths(int buildIndex)
    {
        return GameSaveSystem.LoadLevelDeaths(buildIndex);
    }

    /// <summary>
    /// Adds one death to one level's all-time death count.
    /// </summary>
    public static int RegisterLevelDeath(int buildIndex)
    {
        return GameSaveSystem.AddLevelDeath(buildIndex);
    }

    /// <summary>
    /// Clears the saved all-time death count while keeping settings intact.
    /// </summary>
    public static void ClearTotalDeaths()
    {
        GameSaveSystem.ClearTotalDeaths();
    }
}
