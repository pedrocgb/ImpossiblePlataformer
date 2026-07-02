using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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

    [SerializeField]
    private TMP_Text totalDeathsText;

    [Title("Animated Items")]
    [SerializeField]
    private CanvasGroup titleGroup;

    [SerializeField]
    private CanvasGroup timerGroup;

    [SerializeField]
    private CanvasGroup currentDeathsGroup;

    [SerializeField]
    private CanvasGroup totalDeathsGroup;

    [SerializeField]
    private CanvasGroup nextLevelButtonGroup;

    [SerializeField]
    private CanvasGroup quitButtonGroup;

    [SerializeField]
    private Button nextLevelButton;

    [SerializeField]
    private Button quitButton;

    [Title("Labels")]
    [SerializeField]
    private string timePrefix = "Time: ";

    [SerializeField]
    private string deathsPrefix = "Deaths: ";

    [SerializeField]
    private string totalDeathsPrefix = "Total Deaths: ";

    [Title("Animation")]
    [SerializeField]
    [MinValue(0f)]
    private float fadeDuration = 0.35f;

    [SerializeField]
    private Ease fadeEase = Ease.OutQuad;

    [SerializeField, MinValue(0.01f)]
    private float itemShowDuration = 0.35f;

    [SerializeField, MinValue(0f)]
    private float nextItemStartDelay = 0.28f;

    [SerializeField]
    private Ease itemEase = Ease.OutBack;

    [SerializeField, Range(0f, 1f)]
    private float hiddenItemScale = 0.92f;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private Sequence contentSequence;

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
        KillContentSequence();
    }

    /// <summary>
    /// Stops active panel tweens before Unity destroys the object.
    /// </summary>
    private void OnDestroy()
    {
        KillFadeTween();
        KillContentSequence();
    }

    /// <summary>
    /// Shows the win panel and fills its final level stats.
    /// </summary>
    public void ShowWin(string levelTitle, string timeValue, int deathCount, int totalDeathCount)
    {
        SetText(titleText, levelTitle);
        SetText(timerText, $"{timePrefix}{timeValue}");
        SetText(deathsText, $"{deathsPrefix}{deathCount}");
        SetText(totalDeathsText, $"{totalDeathsPrefix}{totalDeathCount}");
        PrepareAnimatedItems();
        FadeIn();
    }

    /// <summary>
    /// Hides the panel instantly and disables interaction.
    /// </summary>
    public void HideImmediate()
    {
        EnsureCanvasGroup();
        KillFadeTween();
        KillContentSequence();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        PrepareAnimatedItems();
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
        fadeTween = canvasGroup
            .DOFade(1f, fadeDuration)
            .SetEase(fadeEase)
            .SetUpdate(true)
            .OnComplete(PlayContentSequence);
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
    /// Hides animated panel items and disables result buttons before their reveal sequence.
    /// </summary>
    private void PrepareAnimatedItems()
    {
        SetItemHidden(titleGroup);
        SetItemHidden(timerGroup);
        SetItemHidden(currentDeathsGroup);
        SetItemHidden(totalDeathsGroup);
        SetItemHidden(nextLevelButtonGroup);
        SetItemHidden(quitButtonGroup);
        SetButtonInteractable(false);
    }

    /// <summary>
    /// Reveals result panel items in the configured order with overlap.
    /// </summary>
    private void PlayContentSequence()
    {
        KillContentSequence();
        contentSequence = DOTween.Sequence().SetUpdate(true);
        AppendItemAnimation(titleGroup, 0);
        AppendItemAnimation(timerGroup, 1);
        AppendItemAnimation(currentDeathsGroup, 2);
        AppendItemAnimation(totalDeathsGroup, 3);
        AppendItemAnimation(nextLevelButtonGroup, 4);
        AppendItemAnimation(quitButtonGroup, 5);
        contentSequence.OnComplete(() => SetButtonInteractable(true));
    }

    /// <summary>
    /// Adds one item reveal animation to the active content sequence.
    /// </summary>
    private void AppendItemAnimation(CanvasGroup itemGroup, int index)
    {
        if (contentSequence == null || itemGroup == null)
        {
            return;
        }

        float startTime = nextItemStartDelay * index;
        contentSequence.Insert(startTime, itemGroup.DOFade(1f, itemShowDuration).SetEase(Ease.OutQuad));
        contentSequence.Insert(startTime, itemGroup.transform.DOScale(Vector3.one, itemShowDuration).SetEase(itemEase));
    }

    /// <summary>
    /// Hides one animated item and resets its scale.
    /// </summary>
    private void SetItemHidden(CanvasGroup itemGroup)
    {
        if (itemGroup == null)
        {
            return;
        }

        itemGroup.alpha = 0f;
        itemGroup.transform.localScale = Vector3.one * hiddenItemScale;
    }

    /// <summary>
    /// Enables or disables the result buttons.
    /// </summary>
    private void SetButtonInteractable(bool isInteractable)
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.interactable = isInteractable;
        }

        if (quitButton != null)
        {
            quitButton.interactable = isInteractable;
        }
    }

    /// <summary>
    /// Stops the active content reveal sequence when one exists.
    /// </summary>
    private void KillContentSequence()
    {
        if (contentSequence == null || !contentSequence.IsActive())
        {
            return;
        }

        contentSequence.Kill();
        contentSequence = null;
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
