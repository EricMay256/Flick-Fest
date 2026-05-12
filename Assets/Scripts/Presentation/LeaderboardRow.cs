using TMPro;
using UnityEngine;

namespace FlickFest.Presentation
{
    /// <summary>
    /// Prefab for a single leaderboard entry. Score formatting is the caller's
    /// responsibility — GameOverPanel knows whether to render seconds or points.
    /// </summary>
    public sealed class LeaderboardRow : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _rankLabel;
        [SerializeField] private TextMeshProUGUI _playerLabel;
        [SerializeField] private TextMeshProUGUI _scoreLabel;

        public void Populate(int rank, string player, string formattedScore)
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
        }
    }
}
