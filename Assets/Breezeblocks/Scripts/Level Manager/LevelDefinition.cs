using Sirenix.OdinInspector;
using UnityEngine;

public enum LevelDifficulty
{
    [InspectorName("Difícil")]
    Dificil,
    Insano,
    Pesadelo,
    [InspectorName("Impossível")]
    Impossivel
}

[CreateAssetMenu(fileName = "Level Definition", menuName = "Breezeblocks/Levels/Level Definition")]
public sealed class LevelDefinition : ScriptableObject
{
    [Title("Difficulty")]
    [SerializeField]
    private LevelDifficulty difficulty = LevelDifficulty.Dificil;

    /// <summary>
    /// Gets the configured difficulty enum value.
    /// </summary>
    public LevelDifficulty Difficulty => difficulty;

    /// <summary>
    /// Gets the localized difficulty label used by menu UI.
    /// </summary>
    public string GetDifficultyLabel()
    {
        return GameLocalization.Get(GetDifficultyLocalizationKey(), difficulty.ToString());
    }

    /// <summary>
    /// Gets the localization key that matches the configured difficulty.
    /// </summary>
    private string GetDifficultyLocalizationKey()
    {
        switch (difficulty)
        {
            case LevelDifficulty.Dificil:
                return "difficulty.dificil";
            case LevelDifficulty.Insano:
                return "difficulty.insano";
            case LevelDifficulty.Pesadelo:
                return "difficulty.pesadelo";
            case LevelDifficulty.Impossivel:
                return "difficulty.impossivel";
            default:
                return difficulty.ToString();
        }
    }
}
