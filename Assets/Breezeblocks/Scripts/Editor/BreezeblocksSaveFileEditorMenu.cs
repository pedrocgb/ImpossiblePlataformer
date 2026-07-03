#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class BreezeblocksSaveFileEditorMenu
{
    /// <summary>
    /// Shows a destructive action prompt before deleting all project save files.
    /// </summary>
    [MenuItem("Breezeblocks/Erase Save File", priority = 10)]
    private static void EraseSaveFile()
    {
        string savePath = GameSaveSystem.GetSavePath();
        bool shouldDelete = EditorUtility.DisplayDialog(
            "Erase Save File",
            $"This will delete all save files for this game.\n\nPath:\n{savePath}\n\nThis cannot be undone.",
            "Delete Saves",
            "Cancel");

        if (!shouldDelete)
        {
            return;
        }

        bool deletedAnyFile = GameSaveSystem.DeleteAllSaveFiles();
        string message = deletedAnyFile
            ? "Save files deleted."
            : "No save files were found.";

        Debug.Log($"[Breezeblocks] {message}");
        EditorUtility.DisplayDialog("Erase Save File", message, "OK");
    }
}
#endif
