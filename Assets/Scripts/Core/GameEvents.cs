using System;

public static class GameEvents
{
    public static event Action OnMinigameWon;
    public static void MinigameWon() => OnMinigameWon?.Invoke();

    /// <summary>Fired when the character reaches the break point — starts collectible animations.</summary>
    public static event Action OnBreakpointReached;
    public static void BreakpointReached() => OnBreakpointReached?.Invoke();

    /// <summary>Fired after the victory display when a level is fully completed.</summary>
    public static event Action<int> OnLevelProgression;
    public static void LevelProgression(int completedLevel) => OnLevelProgression?.Invoke(completedLevel);

    /// <summary>Fired whenever the player picks something up: (wasGold, regularCollected, regularTotal, goldCollected).</summary>
    public static event Action<bool, int, int, bool> OnItemCollected;
    public static void ItemCollected(bool wasGold, int collected, int total, bool goldCollected) =>
        OnItemCollected?.Invoke(wasGold, collected, total, goldCollected);

    /// <summary>Set before loading the minigame scene so the sequence knows which level to generate.</summary>
    public static int CurrentLevel = 2;

    /// <summary>Set to true when the player completes the full game (boat sails off). Persisted via PlayerPrefs.</summary>
    public static bool GameCompleted = false;
}
