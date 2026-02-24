using UnityEngine;

/// <summary>
/// Data container placed on each Collectible in MainScene.
/// CharacterMovement reads levelIndex to pass the correct level to MinigameMenuController.
/// </summary>
public class CollectibleLevel : MonoBehaviour
{
    public int levelIndex = 1;
}
