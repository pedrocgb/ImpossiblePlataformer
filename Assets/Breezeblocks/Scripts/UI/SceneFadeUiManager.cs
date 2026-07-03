using System.Collections;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(CanvasGroup))]
public sealed class SceneFadeUiManager : MonoBehaviour
{
    public static SceneFadeUiManager Current { get; private set; }

    [Title("Fade")]
    [SerializeField, MinValue(0f)]
    private float fadeDuration = 0.5f;

    [SerializeField]
    private Ease fadeEase = Ease.InOutQuad;

    [SerializeField]
    private bool dontDestroyOnLoad = true;

    private CanvasGroup canvasGroup;
    private Tween fadeTween;
    private Coroutine loadRoutine;

    /// <summary>
    /// Caches the CanvasGroup and registers this fade manager as the active persistent scene fade service.
    /// </summary>
    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (Current != null && Current != this)
        {
            Destroy(gameObject);
            return;
        }

        Current = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    /// <summary>
    /// Starts the opening fade for scenes that begin with this manager already present.
    /// </summary>
    private void Start()
    {
        BeginSceneFadeIn();
    }

    /// <summary>
    /// Subscribes to scene load events so every new scene fades in.
    /// </summary>
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Unsubscribes from scene load events and stops active tweens.
    /// </summary>
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        KillFadeTween();
    }

    /// <summary>
    /// Cleans up active tweens and clears the active manager reference.
    /// </summary>
    private void OnDestroy()
    {
        KillFadeTween();

        if (Current == this)
        {
            Current = null;
        }
    }

    /// <summary>
    /// Loads a scene by name after fading to black.
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName) || loadRoutine != null)
        {
            return;
        }

        loadRoutine = StartCoroutine(LoadSceneRoutine(sceneName));
    }

    /// <summary>
    /// Loads a scene by build index after fading to black.
    /// </summary>
    public void LoadScene(int sceneBuildIndex)
    {
        if (sceneBuildIndex < 0 || loadRoutine != null)
        {
            return;
        }

        loadRoutine = StartCoroutine(LoadSceneRoutine(sceneBuildIndex));
    }

    /// <summary>
    /// Fades from black to transparent.
    /// </summary>
    public void FadeIn()
    {
        FadeTo(ClampAlpha(0f));
    }

    /// <summary>
    /// Fades from transparent to black.
    /// </summary>
    public void FadeOut()
    {
        FadeTo(ClampAlpha(1f));
    }

    /// <summary>
    /// Fades out before starting the async scene load.
    /// </summary>
    private IEnumerator LoadSceneRoutine(string sceneName)
    {
        yield return FadeToRoutine(ClampAlpha(1f));
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(sceneName);
        loadRoutine = null;
    }

    /// <summary>
    /// Fades out before starting the async scene load by build index.
    /// </summary>
    private IEnumerator LoadSceneRoutine(int sceneBuildIndex)
    {
        yield return FadeToRoutine(ClampAlpha(1f));
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(sceneBuildIndex);
        loadRoutine = null;
    }

    /// <summary>
    /// Starts a fade in after Unity finishes loading a scene.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BeginSceneFadeIn();
    }

    /// <summary>
    /// Runs a fade tween and waits for it to finish.
    /// </summary>
    private IEnumerator FadeToRoutine(float alpha)
    {
        Tween tween = FadeTo(alpha);
        if (tween != null)
        {
            yield return tween.WaitForCompletion();
        }
    }

    /// <summary>
    /// Starts a fade tween toward the requested alpha.
    /// </summary>
    private Tween FadeTo(float alpha)
    {
        EnsureCanvasGroup();

        if (canvasGroup == null)
        {
            return null;
        }

        float targetAlpha = ClampAlpha(alpha);
        KillFadeTween();
        canvasGroup.blocksRaycasts = targetAlpha > 0f;
        canvasGroup.interactable = targetAlpha > 0f;
        fadeTween = canvasGroup
            .DOFade(targetAlpha, fadeDuration)
            .SetEase(fadeEase)
            .SetUpdate(true)
            .OnComplete(() =>
            {
                canvasGroup.alpha = ClampAlpha(canvasGroup.alpha);
                canvasGroup.blocksRaycasts = canvasGroup.alpha > 0f;
                canvasGroup.interactable = canvasGroup.alpha > 0f;
            });
        return fadeTween;
    }

    /// <summary>
    /// Starts each scene covered in black before fading to transparent.
    /// </summary>
    private void BeginSceneFadeIn()
    {
        EnsureCanvasGroup();

        if (canvasGroup == null)
        {
            return;
        }

        KillFadeTween();
        canvasGroup.alpha = ClampAlpha(1f);
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;
        FadeIn();
    }

    /// <summary>
    /// Caches the CanvasGroup again when Unity calls a method before Awake has finished.
    /// </summary>
    private void EnsureCanvasGroup()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }
    }

    /// <summary>
    /// Clamps fade alpha values so tweens never target invalid transparency.
    /// </summary>
    private static float ClampAlpha(float alpha)
    {
        return Mathf.Clamp01(alpha);
    }

    /// <summary>
    /// Stops the active fade tween when one exists.
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
}
