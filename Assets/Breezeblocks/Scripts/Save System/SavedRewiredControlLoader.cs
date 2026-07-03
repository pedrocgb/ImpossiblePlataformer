using System;
using System.Collections.Generic;
using Rewired;
using UnityEngine;

public static class SavedRewiredControlLoader
{
    /// <summary>
    /// Hooks Rewired initialization after scene managers have loaded so saved keyboard bindings can apply safely.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterRewiredLoadCallback()
    {
        TryRegisterRewiredCallbacks();

        if (IsRewiredReady())
        {
            LoadSavedControls();
        }
    }

    /// <summary>
    /// Loads saved keyboard bindings into the saved or first available Rewired player.
    /// </summary>
    private static void LoadSavedControls()
    {
        if (!IsRewiredReady())
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

        TryAddKeyboardMap(player, keyboardMap);
    }

    /// <summary>
    /// Resolves a saved Rewired player id, falling back to the first game player.
    /// </summary>
    private static Player ResolvePlayer(int playerId)
    {
        if (!IsRewiredReady())
        {
            return null;
        }

        try
        {
            Player player = ReInput.players.GetPlayer(playerId);

            if (player != null)
            {
                return player;
            }

            IList<Player> players = ReInput.players.Players;
            return players.Count > 0 ? players[0] : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Safely hooks Rewired initialization callbacks when the Rewired manager is available.
    /// </summary>
    private static void TryRegisterRewiredCallbacks()
    {
        try
        {
            ReInput.InitializedEvent -= LoadSavedControls;
            ReInput.InitializedEvent += LoadSavedControls;
        }
        catch (Exception)
        {
            // Rewired can be unavailable during editor play-mode teardown before the manager exists.
        }
    }

    /// <summary>
    /// Checks whether Rewired can currently be used without throwing during startup or teardown.
    /// </summary>
    private static bool IsRewiredReady()
    {
        try
        {
            return ReInput.isReady;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Safely adds the saved keyboard map when Rewired remains available.
    /// </summary>
    private static void TryAddKeyboardMap(Player player, ControllerMap keyboardMap)
    {
        if (player == null || keyboardMap == null || !IsRewiredReady())
        {
            return;
        }

        try
        {
            player.controllers.maps.AddMap(ReInput.controllers.Keyboard, keyboardMap);
        }
        catch (Exception)
        {
            // Rewired may deinitialize between readiness checks while leaving play mode.
        }
    }
}
