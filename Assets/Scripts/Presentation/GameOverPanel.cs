using FlickFest.Core;
using TMPro;
using UBear.Leaderboard;
using UnityEngine;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Shown on game over. Three phases: immediate local score, server response
  /// with rank/percentile, optional full-leaderboard fetch on demand.
  /// </summary>
  public sealed class GameOverPanel : MonoBehaviour
  {
    [SerializeField] private GameSession _session;

    [Header("Score Display")]
    [SerializeField] private TextMeshProUGUI _finalScoreLabel;
    [SerializeField] private TextMeshProUGUI _scoreContextLabel;
    [SerializeField] private TextMeshProUGUI _rankLabel;
    [SerializeField] private TextMeshProUGUI _percentileLabel;
    [SerializeField] private TextMeshProUGUI _personalBestLabel;
    [SerializeField] private TextMeshProUGUI _statusLabel;

    [Header("Leaderboard")]
    [SerializeField] private Transform _leaderboardContainer;
    [SerializeField] private LeaderboardRow _rowPrefab;
    [SerializeField] private TextMeshProUGUI _leaderboardError;

    [Header("Buttons")]
    [SerializeField] private GameObject _playAgainButton;
    [SerializeField] private GameObject _leaderboardButton;

    [Header("Panel Root")]
    [Tooltip("Root GameObject toggled on/off when the panel shows or hides. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;

    private int _submittedScore;
    private string _submittedGameMode;
    private GameModeDefinition _mode;

    private void OnEnable()
    {
      if (_session != null)
      {
        _session.OnGameOver += HandleGameOver;
        _session.OnStateChanged += HandleStateChanged;
      }
    }

    private void OnDisable()
    {
      if (_session != null)
      {
        _session.OnGameOver -= HandleGameOver;
        _session.OnStateChanged -= HandleStateChanged;
      }
    }

    private void HandleStateChanged(GameState state)
    {
      if (state != GameState.GameOver)
      {
        SetPanelActive(false);
      }
    }

    private void HandleGameOver(int finalScore, string gameMode)
    {
      _submittedScore = finalScore;
      _submittedGameMode = gameMode;
      _mode = _session.ActiveMode;

      SetPanelActive(true);
      ClearLeaderboardRows();
      SetText(_leaderboardError, string.Empty);
      SetText(_rankLabel, string.Empty);
      SetText(_percentileLabel, string.Empty);
      SetText(_personalBestLabel, string.Empty);

      DisplayLocalScore(finalScore, _mode);

      SetText(_statusLabel, "Submitting...");
      SetActive(_playAgainButton, false);
      SetActive(_leaderboardButton, false);

      _session.SubmitScore(finalScore, gameMode, OnScoreSubmitted);
    }

    private void DisplayLocalScore(int finalScore, GameModeDefinition mode)
    {
      if (mode != null && mode.EndCondition == EndCondition.TargetCount)
      {
        SetText(_finalScoreLabel, FormatBlitz(finalScore));
        SetText(_scoreContextLabel, "Time to clear all targets");
      }
      else
      {
        SetText(_finalScoreLabel, finalScore.ToString("N0"));
        SetText(_scoreContextLabel, mode != null ? $"{mode.DisplayLabel} mode" : string.Empty);
      }
    }

    private void OnScoreSubmitted(ApiResult<ScoreResponse> result)
    {
      if (!result.Success || result.Data == null)
      {
        SetText(_statusLabel, "Offline — score not submitted");
        Debug.LogWarning($"[GameOverPanel] Score submission failed: {result.Error}");
        SetActive(_playAgainButton, true);
        SetActive(_leaderboardButton, true);
        return;
      }

      ScoreResponse stored = result.Data;
      bool isNewBest = stored.Score == _submittedScore;

      if (stored.Rank.HasValue)
      {
        SetText(_rankLabel, $"#{stored.Rank.Value}");
      }

      if (stored.Percentile.HasValue)
      {
        double topPercent = 100.0 - stored.Percentile.Value;
        SetText(_percentileLabel, $"Top {topPercent:F0}%");
      }

      if (isNewBest)
      {
        SetText(_personalBestLabel, "New personal best!");
      }
      else
      {
        string formatted = FormatScoreForMode(stored.Score, _mode);
        SetText(_personalBestLabel, $"Best: {formatted}");
      }

      SetText(_statusLabel, string.Empty);
      SetActive(_playAgainButton, true);
      SetActive(_leaderboardButton, true);
    }

    public void OnLeaderboardButtonPressed()
    {
      SetActive(_leaderboardButton, false);
      SetText(_statusLabel, "Loading leaderboard...");
      SetText(_leaderboardError, string.Empty);
      ClearLeaderboardRows();
      _session.FetchLeaderboard(_submittedGameMode, OnLeaderboardReceived);
    }

    public void OnPlayAgainPressed()
    {
      ClearLeaderboardRows();
      SetPanelActive(false);
      if (_session != null && _session.ActiveMode != null)
      {
        _session.StartGame(_session.ActiveMode);
      }
    }

    private void OnLeaderboardReceived(ApiResult<LeaderboardResponse> result)
    {
      SetText(_statusLabel, string.Empty);

      if (!result.Success || result.Data == null)
      {
        SetText(_leaderboardError, result.Error ?? "Failed to load leaderboard.");
        SetActive(_leaderboardButton, true);
        return;
      }

      LeaderboardResponse data = result.Data;
      if (data.Scores == null || data.Scores.Count == 0)
      {
        SetText(_leaderboardError, "No scores yet — you're the first!");
        return;
      }

      if (_rowPrefab == null || _leaderboardContainer == null)
      {
        Debug.LogWarning("[GameOverPanel] LeaderboardRow prefab or container not assigned.");
        return;
      }

      foreach (ScoreResponse score in data.Scores)
      {
        LeaderboardRow row = Instantiate(_rowPrefab, _leaderboardContainer);
        int rank = score.Rank ?? 0;
        string formatted = FormatScoreForMode(score.Score, _mode);
        row.Populate(rank, score.Player ?? "—", formatted);
      }
    }

    private void ClearLeaderboardRows()
    {
      if (_leaderboardContainer == null)
      {
        return;
      }
      for (int i = _leaderboardContainer.childCount - 1; i >= 0; i--)
      {
        Destroy(_leaderboardContainer.GetChild(i).gameObject);
      }
    }

    private static string FormatScoreForMode(long score, GameModeDefinition mode)
    {
      if (mode != null && mode.EndCondition == EndCondition.TargetCount)
      {
        return FormatBlitz((int)score);
      }
      return score.ToString("N0");
    }

    private static string FormatBlitz(int milliseconds)
    {
      float seconds = milliseconds / 1000f;
      return $"{seconds:F2}s";
    }

    private void SetPanelActive(bool active)
    {
      if (_panelRoot != null)
      {
        _panelRoot.SetActive(active);
      }
    }

    private static void SetText(TextMeshProUGUI label, string text)
    {
      if (label != null)
      {
        label.text = text;
      }
    }

    private static void SetActive(GameObject go, bool active)
    {
      if (go != null)
      {
        go.SetActive(active);
      }
    }
  }
}
