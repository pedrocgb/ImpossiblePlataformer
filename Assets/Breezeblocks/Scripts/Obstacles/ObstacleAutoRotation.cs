using System;
using DG.Tweening;
using Sirenix.OdinInspector;
using UnityEngine;

[Serializable]
public sealed class ObstacleAutoRotation
{
    private enum AutoRotationType
    {
        FixedRotation,
        FluidRotation
    }

    private enum RotationDirection
    {
        Clockwise,
        Counterclockwise
    }

    [SerializeField]
    private bool autoRotate;

    [SerializeField]
    [ShowIf(nameof(autoRotate))]
    private AutoRotationType rotationType = AutoRotationType.FluidRotation;

    [SerializeField]
    [ShowIf(nameof(ShowFixedRotationSettings))]
    [MinValue(0.01f)]
    private float fixedRotationStepDegrees = 90f;

    [SerializeField]
    [ShowIf(nameof(ShowFixedRotationSettings))]
    [MinValue(0.01f)]
    private float fixedRotationDuration = 0.08f;

    [SerializeField]
    [ShowIf(nameof(ShowFixedRotationSettings))]
    [MinValue(0f)]
    private float fixedRotationDelay = 0.25f;

    [SerializeField]
    [ShowIf(nameof(ShowFixedRotationSettings))]
    private Ease fixedRotationEase = Ease.OutQuad;

    [SerializeField]
    [ShowIf(nameof(ShowFixedRotationSettings))]
    private RotationDirection fixedRotationDirection = RotationDirection.Clockwise;

    [SerializeField]
    [ShowIf(nameof(ShowFluidRotationSettings))]
    [MinValue(0f)]
    private float fluidDegreesPerSecond = 90f;

    [SerializeField]
    [ShowIf(nameof(ShowFluidRotationSettings))]
    private RotationDirection fluidRotationDirection = RotationDirection.Clockwise;

    private Transform rotationTarget;
    private Quaternion startLocalRotation;
    private Tween fixedRotationTween;
    private bool isRunning;

    private bool ShowFixedRotationSettings => autoRotate && rotationType == AutoRotationType.FixedRotation;
    private bool ShowFluidRotationSettings => autoRotate && rotationType == AutoRotationType.FluidRotation;

    /// <summary>
    /// Caches the transform that should rotate and stores its authored local rotation.
    /// </summary>
    public void Initialize(Transform target)
    {
        rotationTarget = target;

        if (rotationTarget != null)
        {
            startLocalRotation = rotationTarget.localRotation;
        }
    }

    /// <summary>
    /// Starts the configured rotation mode when auto-rotation is enabled.
    /// </summary>
    public void Start()
    {
        if (!autoRotate || rotationTarget == null)
        {
            return;
        }

        isRunning = true;

        if (rotationType == AutoRotationType.FixedRotation)
        {
            StartFixedRotationStep();
        }
    }

    /// <summary>
    /// Advances the low-cost fluid rotation mode by the supplied frame delta time.
    /// </summary>
    public void Tick(float deltaTime)
    {
        if (!isRunning || !autoRotate || rotationTarget == null || rotationType != AutoRotationType.FluidRotation)
        {
            return;
        }

        float signedSpeed = fluidRotationDirection == RotationDirection.Clockwise
            ? -fluidDegreesPerSecond
            : fluidDegreesPerSecond;

        rotationTarget.Rotate(0f, 0f, signedSpeed * deltaTime, Space.Self);
    }

    /// <summary>
    /// Stops active rotation tweens without changing the current transform rotation.
    /// </summary>
    public void Stop()
    {
        isRunning = false;
        KillFixedRotationTween();
    }

    /// <summary>
    /// Restores the cached local rotation and stops any active rotation tween.
    /// </summary>
    public void ResetToStart()
    {
        Stop();

        if (rotationTarget != null)
        {
            rotationTarget.localRotation = startLocalRotation;
        }
    }

    /// <summary>
    /// Starts one quick fixed-rotation tween step and chains the next step when complete.
    /// </summary>
    private void StartFixedRotationStep()
    {
        if (!isRunning || rotationTarget == null)
        {
            return;
        }

        KillFixedRotationTween();

        Vector3 currentEuler = rotationTarget.localEulerAngles;
        float signedStep = fixedRotationDirection == RotationDirection.Clockwise
            ? -fixedRotationStepDegrees
            : fixedRotationStepDegrees;

        Vector3 targetEuler = new Vector3(currentEuler.x, currentEuler.y, currentEuler.z + signedStep);
        fixedRotationTween = rotationTarget
            .DOLocalRotate(targetEuler, fixedRotationDuration, RotateMode.FastBeyond360)
            .SetDelay(fixedRotationDelay)
            .SetEase(fixedRotationEase)
            .OnComplete(OnFixedRotationStepComplete);
    }

    /// <summary>
    /// Continues the fixed-rotation loop while the rotation remains active.
    /// </summary>
    private void OnFixedRotationStepComplete()
    {
        fixedRotationTween = null;

        if (isRunning)
        {
            StartFixedRotationStep();
        }
    }

    /// <summary>
    /// Kills the active fixed-rotation tween when one exists.
    /// </summary>
    private void KillFixedRotationTween()
    {
        if (fixedRotationTween == null || !fixedRotationTween.IsActive())
        {
            return;
        }

        fixedRotationTween.Kill();
        fixedRotationTween = null;
    }
}
