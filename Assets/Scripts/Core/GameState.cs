namespace FlickFest.Core
{
  /// <summary>
  /// Lifecycle phase emitted by <see cref="GameSession.OnStateChanged"/>.
  /// UI panels bind their visibility to these transitions.
  /// </summary>
  public enum GameState
  {
    MainMenu,
    Countdown,
    Playing,
    GameOver
  }
}
