using DG.Tweening;
using TMPro;
using UnityEngine;
using Sirenix.OdinInspector;

[RequireComponent(typeof(CanvasGroup))]
public sealed class LevelResultPanel : MonoBehaviour
{
    [Title("Text")]
    [SerializeField]
    private TMP_Text titleText;

    [SerializeField]
    private TMP_Text timerText;

    [SerializeField]
    private TMP_Text deathsText;

    [Title("Labels")]
    [SerializeField]
    private string timePrefix = "Time: ";

    [SerializeField]
    private string deathsPrefix = "Deaths: ";

    [Title("Animation")]
    [SerializeField]
    [MinValue(0f)]
    private float fadeDuration = 0.35f;

    [SerializeField]
    private Ease fadeEase = Ease.OutQuad;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;

    /// <summary>
    /// Caches the same-object canvas group used for panel fading.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    /// <summary>
    /// Stops active panel tweens before Unity disables the object.
    /// </summary>
    private void OnDisable()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Stops active panel tweens before Unity destroys the object.
    /// </summary>
    private void OnDestroy()
    {
        KillFadeTween();
    }

    /// <summary>
    /// Shows the win panel and fills its final level stats.
    /// </summary>
    public void ShowWin(string levelTitle, string timeValue, int deathCount)
    {
        SetText(titleText, levelTitle);
        SetText(timerText, $"{timePrefix}{timeValue}");
        SetText(deathsText, $"{deathsPrefix}{deathCount}");
        FadeIn();
    }

    /// <summary>
    /// Hides the panel instantly and disables interaction.
    /// </summary>
    public void HideImmediate()
    {
        EnsureCanvasGroup();
        KillFadeTween();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>
    /// Fades the panel in and enables UI interaction.
    /// </summary>
    private void FadeIn()
    {
        EnsureCanvasGroup();
        KillFadeTween();
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        fadeTween = canvasGroup.DOFade(1f, fadeDuration).SetEase(fadeEase).SetUpdate(true);
    }

    /// <summary>
    /// Kills the current fade tween when it exists.
    /// </summary>
    private void KillFadeTween()
    {
        if (fadeTween == null || !fadeTween.IsActive())
        {
            return;
        }

        fadeTween.Kill();
        fadeTween = null;
    }

    /// <summary>
    /// Caches the same-object canvas group if another script calls this panel before Awake.
    /// </summary>
    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Assigns text when the target text reference exists.
    /// </summary>
    private static void SetText(TMP_Text targetText, string value)
    {
        if (targetText != null)
        {
            targetText.text = value;
        }
    }
}
