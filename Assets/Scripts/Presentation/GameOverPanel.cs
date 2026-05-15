using FlickFest.Core;
using TMPro;
using UBear.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Shown on game over. Three phases: immediate local score, server response
  /// with rank/percentile, optional full-leaderboard fetch on demand.
  ///
  /// The layout exposes both the legacy standalone labels (_rankLabel,
  /// _percentileLabel, _personalBestLabel) and a new combined "Best Score"
  /// presentation (_bestScoreValueLabel + _bestScoreSubRowLabel). Wire whichever
  /// pair the prefab uses — the unwired side simply stays empty.
  /// </summary>
  public sealed class GameOverPanel : MonoBehaviour
  {
    [SerializeField] private GameSession _session;

    [Header("Score Display")]
    [SerializeField] private TextMeshProUGUI _finalScoreLabel;
    [SerializeField] private TextMeshProUGUI _scoreContextLabel;
    [SerializeField] private TextMeshProUGUI _statusLabel;

    [Header("Legacy Rank / Percentile / Best (optional)")]
    [Tooltip("Standalone rank label. Leave unwired if you use the combined sub-row label instead.")]
    [SerializeField] private TextMeshProUGUI _rankLabel;
    [Tooltip("Standalone percentile label. Leave unwired if you use the combined sub-row label instead.")]
    [SerializeField] private TextMeshProUGUI _percentileLabel;
    [Tooltip("Standalone personal-best label. Leave unwired if you use the combined best-score value label instead.")]
    [SerializeField] private TextMeshProUGUI _personalBestLabel;

    [Header("Best Score Block")]
    [Tooltip("Caption like \"Best Score:\". Shown at reduced opacity by design (set in scene).")]
    [SerializeField] private TextMeshProUGUI _bestScoreCaptionLabel;
    [Tooltip("The best-score value (e.g. \"13.45s\") rendered in semibold weight (set in scene).")]
    [SerializeField] private TextMeshProUGUI _bestScoreValueLabel;
    [Tooltip("Sub-row like \"guest_a3f2 · #1 · Top 0%\" rendered at reduced opacity (set in scene).")]
    [SerializeField] private TextMeshProUGUI _bestScoreSubRowLabel;

    [Header("Leaderboard")]
    [SerializeField] private Transform _leaderboardContainer;
    [SerializeField] private LeaderboardRow _rowPrefab;
    [SerializeField] private TextMeshProUGUI _leaderboardError;

    [Header("Buttons")]
    [SerializeField] private GameObject _playAgainButton;
    [SerializeField] private GameObject _leaderboardButton;
    [SerializeField] private Button _changeNameButton;

    [Header("Change Name")]
    [SerializeField] private ChangeNameDialog _changeNameDialog;

    [Header("Panel Root")]
    [Tooltip("Root GameObject toggled on/off when the panel shows or hides. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;

    private int _submittedScore;
    private string _submittedGameMode;
    private GameModeDefinition _mode;
    private string _currentPlayerName;
    private string _cachedRankText;
    private string _cachedPercentileText;
    private LeaderboardResponse _lastLeaderboardData;

    private void Awake()
    {
      if (_changeNameButton != null)
      {
        _changeNameButton.onClick.AddListener(OnChangeNamePressed);
      }
      if (_changeNameDialog != null)
      {
        _changeNameDialog.OnRenamed += HandlePlayerRenamed;
      }
      SetActive(_changeNameButton != null ? _changeNameButton.gameObject : null, false);
    }

    private void OnDestroy()
    {
      if (_changeNameButton != null)
      {
        _changeNameButton.onClick.RemoveListener(OnChangeNamePressed);
      }
      if (_changeNameDialog != null)
      {
        _changeNameDialog.OnRenamed -= HandlePlayerRenamed;
      }
    }

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
      _lastLeaderboardData = null;

      SetPanelActive(true);
      ClearLeaderboardRows();
      SetText(_leaderboardError, string.Empty);
      SetText(_rankLabel, string.Empty);
      SetText(_percentileLabel, string.Empty);
      SetText(_personalBestLabel, string.Empty);
      SetText(_bestScoreCaptionLabel, string.Empty);
      SetText(_bestScoreValueLabel, string.Empty);
      SetText(_bestScoreSubRowLabel, string.Empty);

      DisplayLocalScore(finalScore, _mode);

      SetText(_statusLabel, "Submitting...");
      SetActive(_playAgainButton, false);
      SetActive(_leaderboardButton, false);
      SetActive(_changeNameButton != null ? _changeNameButton.gameObject : null, false);

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
        // Without a confirmed identity from the server, hide the rename button.
        SetActive(_changeNameButton != null ? _changeNameButton.gameObject : null, false);
        return;
      }

      ScoreResponse stored = result.Data;
      bool isNewBest = stored.Score == _submittedScore;
      _currentPlayerName = stored.Player;

      RenderRankPercentileBlock(stored, isNewBest);

      SetText(_statusLabel, string.Empty);
      SetActive(_playAgainButton, true);
      SetActive(_leaderboardButton, true);
      SetActive(_changeNameButton != null ? _changeNameButton.gameObject : null, _changeNameDialog != null);
    }

    private void RenderRankPercentileBlock(ScoreResponse stored, bool isNewBest)
    {
      string rankText = stored.Rank.HasValue ? $"#{stored.Rank.Value}" : string.Empty;
      string percentileText = stored.Percentile.HasValue
          ? $"Top {(100.0 - stored.Percentile.Value):F0}%"
          : string.Empty;
      _cachedRankText = rankText;
      _cachedPercentileText = percentileText;
      string bestFormatted = FormatScoreForMode(stored.Score, _mode);
      string bestLine = isNewBest ? "New personal best!" : bestFormatted;

      // Legacy standalone labels — only populated if wired.
      SetText(_rankLabel, rankText);
      SetText(_percentileLabel, percentileText);
      SetText(_personalBestLabel, isNewBest ? "New personal best!" : $"Best: {bestFormatted}");

      // New combined "Best Score" presentation.
      if (_bestScoreValueLabel != null)
      {
        _bestScoreValueLabel.text = bestLine;
      }
      if (_bestScoreCaptionLabel != null)
      {
        // Hide the "Best Score:" caption when the row shows the celebratory
        // "New personal best!" message instead of a value.
        _bestScoreCaptionLabel.text = isNewBest ? string.Empty : "Best Score:";
      }
      if (_bestScoreSubRowLabel != null)
      {
        _bestScoreSubRowLabel.text = ComposeSubRow(_currentPlayerName, rankText, percentileText);
      }
    }

    private static string ComposeSubRow(string player, string rank, string percentile)
    {
      const string Separator = "  ·  ";
      var parts = new System.Collections.Generic.List<string>(3);
      if (!string.IsNullOrEmpty(player)) parts.Add(player);
      if (!string.IsNullOrEmpty(rank)) parts.Add(rank);
      if (!string.IsNullOrEmpty(percentile)) parts.Add(percentile);
      return string.Join(Separator, parts);
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

    private void OnChangeNamePressed()
    {
      if (_changeNameDialog == null)
      {
        return;
      }
      _changeNameDialog.Open(_currentPlayerName);
    }

    private void HandlePlayerRenamed(string newName)
    {
      string previousName = _currentPlayerName;
      _currentPlayerName = newName;

      // Update the visible sub-row immediately so the user sees their new name.
      if (_bestScoreSubRowLabel != null)
      {
        _bestScoreSubRowLabel.text = ComposeSubRow(newName, _cachedRankText, _cachedPercentileText);
      }

      // The cached leaderboard payload still references the previous username,
      // so patch the matching entry in-place before re-rendering. That keeps
      // the self-highlight on the right row without forcing a network round trip.
      if (_lastLeaderboardData != null && _lastLeaderboardData.Scores != null)
      {
        if (!string.IsNullOrEmpty(previousName))
        {
          foreach (ScoreResponse score in _lastLeaderboardData.Scores)
          {
            if (string.Equals(score.Player, previousName, System.StringComparison.Ordinal))
            {
              score.Player = newName;
              break;
            }
          }
        }
        PopulateLeaderboard(_lastLeaderboardData);
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

      _lastLeaderboardData = data;
      PopulateLeaderboard(data);
    }

    private void PopulateLeaderboard(LeaderboardResponse data)
    {
      ClearLeaderboardRows();
      foreach (ScoreResponse score in data.Scores)
      {
        LeaderboardRow row = Instantiate(_rowPrefab, _leaderboardContainer);
        int rank = score.Rank ?? 0;
        string formatted = FormatScoreForMode(score.Score, _mode);
        bool isSelf = !string.IsNullOrEmpty(_currentPlayerName)
                   && string.Equals(score.Player, _currentPlayerName, System.StringComparison.Ordinal);
        row.Populate(rank, score.Player ?? "—", formatted, isSelf);
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
