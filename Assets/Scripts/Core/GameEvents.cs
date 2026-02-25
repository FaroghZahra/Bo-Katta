using System;

public static class GameEvents
{
    public static event Action OnMinigameWon;
    public static void MinigameWon() => OnMinigameWon?.Invoke();

    /// <summary>Fired when the character reaches the break point — starts collectible animations.</summary>
    public static event Action OnBreakpointReached;
    public static void BreakpointReached() => OnBreakpointReached?.Invoke();

    /// <summary>Set before loading the minigame scene so the sequence knows which level to generate.</summary>
    public static int CurrentLevel = 1;
}
