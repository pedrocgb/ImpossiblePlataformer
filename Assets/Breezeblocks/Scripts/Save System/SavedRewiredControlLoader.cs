using System.Collections.Generic;
using Rewired;
using UnityEngine;

public static class SavedRewiredControlLoader
{
    /// <summary>
    /// Hooks Rewired initialization so saved keyboard bindings apply before gameplay input is read.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterRewiredLoadCallback()
    {
        ReInput.InitializedEvent -= LoadSavedControls;
        ReInput.InitializedEvent += LoadSavedControls;

        if (ReInput.isReady)
        {
            LoadSavedControls();
        }
    }

    /// <summary>
    /// Loads saved keyboard bindings into the saved or first available Rewired player.
    /// </summary>
    private static void LoadSavedControls()
    {
        if (!ReInput.isReady)
        {
            return;
        }

        ControlSettingsSaveData controlSettings = GameSaveSystem.LoadControlSettings();

        if (controlSettings == null || string.IsNullOrWhiteSpace(controlSettings.KeyboardMapXml))
        {
            return;
        }

        Player player = ResolvePlayer(controlSettings.PlayerId);

        if (player == null)
        {
            return;
        }

        ControllerMap keyboardMap = ControllerMap.CreateFromXml(ControllerType.Keyboard, controlSettings.KeyboardMapXml);

        if (keyboardMap != null)
        {
            player.controllers.maps.AddMap(ReInput.controllers.Keyboard, keyboardMap);
        }
    }

    /// <summary>
    /// Resolves a saved Rewired player id, falling back to the first game player.
    /// </summary>
    private static Player ResolvePlayer(int playerId)
    {
        Player player = ReInput.players.GetPlayer(playerId);

        if (player != null)
        {
            return player;
        }

        IList<Player> players = ReInput.players.Players;
        return players.Count > 0 ? players[0] : null;
    }
}
