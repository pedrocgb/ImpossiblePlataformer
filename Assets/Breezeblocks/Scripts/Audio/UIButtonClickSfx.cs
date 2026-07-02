using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class UIButtonClickSfx : MonoBehaviour
{
    private Button button;

    /// <summary>
    /// Caches the same-object UI Button.
    /// </summary>
    private void Awake()
    {
        button = GetComponent<Button>();
    }

    /// <summary>
    /// Starts listening for UI button clicks.
    /// </summary>
    private void OnEnable()
    {
        EnsureButton();
        button.onClick.AddListener(PlayClickSfx);
    }

    /// <summary>
    /// Stops listening for UI button clicks.
    /// </summary>
    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(PlayClickSfx);
        }
    }

    /// <summary>
    /// Plays the configured UI button click SFX.
    /// </summary>
    private void PlayClickSfx()
    {
        GameSfxPlayer.PlayUiButtonClick();
    }

    /// <summary>
    /// Caches the button if OnEnable runs before Awake.
    /// </summary>
    private void EnsureButton()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }
}
