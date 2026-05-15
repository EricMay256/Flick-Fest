using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Prefab for a single leaderboard entry. Score formatting is the caller's
  /// responsibility — GameOverPanel knows whether to render seconds or points.
  /// The row can highlight itself when it represents the current player by
  /// tinting the row background; see <see cref="Populate"/>.
  /// </summary>
  public sealed class LeaderboardRow : MonoBehaviour
  {
    [SerializeField] private TextMeshProUGUI _rankLabel;
    [SerializeField] private TextMeshProUGUI _playerLabel;
    [SerializeField] private TextMeshProUGUI _scoreLabel;

    [Header("Self Highlight")]
    [Tooltip("Optional background image whose color flips when the row represents the current player.")]
    [SerializeField] private Image _rowBackground;
    [Tooltip("Background color used for ordinary leaderboard rows.")]
    [SerializeField] private Color _normalBackgroundColor = new Color(0f, 0f, 0f, 0f);
    [Tooltip("Background color used when this row belongs to the current player. Defaults to a muted orange.")]
    [SerializeField] private Color _highlightBackgroundColor = new Color(0.85f, 0.45f, 0.10f, 0.25f);

    public void Populate(int rank, string player, string formattedScore, bool isCurrentUser = false)
    {
      if (_rankLabel != null)
      {
        _rankLabel.text = $"#{rank}";
      }
      if (_playerLabel != null)
      {
        _playerLabel.text = player ?? string.Empty;
      }
      if (_scoreLabel != null)
      {
        _scoreLabel.text = formattedScore ?? string.Empty;
      }
      if (_rowBackground != null)
      {
        _rowBackground.color = isCurrentUser ? _highlightBackgroundColor : _normalBackgroundColor;
      }
    }
  }
}
