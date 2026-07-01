using UnityEngine;

public sealed class LevelResettableTransform : MonoBehaviour, ILevelResettable
{
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 startScale;

    /// <summary>
    /// Captures this object's transform state at level start.
    /// </summary>
    private void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startScale = transform.localScale;
    }

    /// <summary>
    /// Restores this object's transform state captured at level start.
    /// </summary>
    public void ResetLevelState()
    {
        transform.SetPositionAndRotation(startPosition, startRotation);
        transform.localScale = startScale;
    }
}
