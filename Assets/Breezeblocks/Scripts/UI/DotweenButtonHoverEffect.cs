using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class DotweenButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Title("Hover")]
    [SerializeField, MinValue(1f)]
    private float hoverScale = 1.08f;

    [SerializeField, MinValue(0f)]
    private float hoverDuration = 0.12f;

    [SerializeField]
    private Ease hoverEase = Ease.OutBack;

    [Title("Exit Bounce")]
    [SerializeField, MinValue(0f)]
    private float exitBounceDuration = 0.5f;

    [SerializeField, Range(0.1f, 1f)]
    private float exitShrinkScale = 0.94f;

    [SerializeField, MinValue(1f)]
    private float exitExpandScale = 1.03f;

    [SerializeField]
    private Ease exitEase = Ease.OutQuad;

    private RectTransform rectTransform;
    private Button button;
    private Vector3 originalScale;
    private Sequence scaleSequence;

    /// <summary>
    /// Caches same-object UI references and stores the authored button scale.
    /// </summary>
    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        button = GetComponent<Button>();
        originalScale = rectTransform.localScale;
    }

    /// <summary>
    /// Stops active hover tweens before Unity disables this button.
    /// </summary>
    private void OnDisable()
    {
        KillScaleSequence();
        RestoreOriginalScale();
    }

    /// <summary>
    /// Stops active hover tweens before Unity destroys this button.
    /// </summary>
    private void OnDestroy()
    {
        KillScaleSequence();
    }

    /// <summary>
    /// Expands the button when the pointer enters and the button can be clicked.
    /// </summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (button == null || !button.interactable)
        {
            return;
        }

        EnsureRectTransform();
        KillScaleSequence();
        scaleSequence = DOTween.Sequence().SetUpdate(true);
        scaleSequence.Append(rectTransform.DOScale(originalScale * hoverScale, hoverDuration).SetEase(hoverEase));
    }

    /// <summary>
    /// Plays a fast shrink and expand bounce before returning to the authored scale.
    /// </summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        EnsureRectTransform();
        KillScaleSequence();

        float segmentDuration = exitBounceDuration / 3f;
        scaleSequence = DOTween.Sequence().SetUpdate(true);
        scaleSequence.Append(rectTransform.DOScale(originalScale * exitShrinkScale, segmentDuration).SetEase(exitEase));
        scaleSequence.Append(rectTransform.DOScale(originalScale * exitExpandScale, segmentDuration).SetEase(exitEase));
        scaleSequence.Append(rectTransform.DOScale(originalScale, segmentDuration).SetEase(exitEase));
    }

    /// <summary>
    /// Caches the RectTransform if an event is received before Awake finishes.
    /// </summary>
    private void EnsureRectTransform()
    {
        if (rectTransform == null)
        {
            rectTransform = GetComponent<RectTransform>();
            originalScale = rectTransform.localScale;
        }
    }

    /// <summary>
    /// Restores the authored button scale after a tween is interrupted.
    /// </summary>
    private void RestoreOriginalScale()
    {
        if (rectTransform != null)
        {
            rectTransform.localScale = originalScale;
        }
    }

    /// <summary>
    /// Kills the active scale sequence when it exists.
    /// </summary>
    private void KillScaleSequence()
    {
        if (scaleSequence == null || !scaleSequence.IsActive())
        {
            return;
        }

        scaleSequence.Kill();
        scaleSequence = null;
    }
}
