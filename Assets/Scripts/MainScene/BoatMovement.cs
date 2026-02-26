using DG.Tweening;
using UnityEngine;

public class BoatMovement : MonoBehaviour
{
    [Header("Target")]
    public Transform moveTarget;

    [Header("Passengers")]
    public Transform character;

    [Header("Timing")]
    public float moveDuration = 3f;
    public Ease  moveEase     = Ease.InOutSine;

    public System.Action onSailingComplete;

    public void StartSailing()
    {
        if (moveTarget == null) return;
        if (character != null)
            character.SetParent(transform, worldPositionStays: true);
        transform.DOMove(moveTarget.position, moveDuration)
            .SetEase(moveEase)
            .OnComplete(() => onSailingComplete?.Invoke());
    }
}
