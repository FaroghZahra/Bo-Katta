using System;

public static class GameEvents
{
    public static event Action OnMinigameWon;
    public static void MinigameWon() => OnMinigameWon?.Invoke();
}
