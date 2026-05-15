using System.Collections.Generic;
using FlickFest.Core;
using TMPro;
using UBear.Leaderboard;
using UnityEngine;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Title screen. Two responsibilities: silent guest auth and mode selection.
  /// Calls into the leaderboard service via <see cref="GameSession.LeaderboardService"/>
  /// so that all networking concerns stay confined to one MonoBehaviour.
  /// </summary>
  public sealed class MainMenu : MonoBehaviour
  {
    [SerializeField] private GameSession _session;
    [SerializeField] private LeaderboardService _leaderboardService;
    [SerializeField] private List<GameModeDefinition> _modes = new();
    [SerializeField] private Transform _modeButtonContainer;
    [SerializeField] private ModeButton _modeButtonPrefab;
    [SerializeField] private TextMeshProUGUI _titleLabel;
    [SerializeField] private TextMeshProUGUI _statusLabel;
    [SerializeField] private TextMeshProUGUI _selectedModeLabel;
    [SerializeField] private GameObject _playButton;

    [Tooltip("Root GameObject for the menu panel. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;

    private GameModeDefinition _selectedMode;
    private bool _authResolved;
    private readonly List<ModeButton> _spawnedButtons = new();

    private void Awake()
    {
      if (_panelRoot == null)
      {
        _panelRoot = gameObject;
      }
    }

    private void OnEnable()
    {
      SetText(_titleLabel, "Flick Fest");
      SetText(_statusLabel, "Connecting...");
      SetActive(_playButton, false);
      BuildModeButtons();

      _authResolved = false;
      if (_leaderboardService != null)
      {
        StartCoroutine(_leaderboardService.EnsureAuthenticated(OnAuthenticated));
      }
      else
      {
        Debug.LogWarning("[MainMenu] LeaderboardService not assigned — gameplay will proceed in offline mode.");
        OnAuthenticated(ApiResult<bool>.Fail("LeaderboardService not assigned."));
      }
    }

    public void Show()
    {
      if (_panelRoot != null)
      {
        _panelRoot.SetActive(true);
      }
    }

    public void OnPlayPressed()
    {
      if (_selectedMode == null || _session == null)
      {
        return;
      }

      if (_panelRoot != null)
      {
        _panelRoot.SetActive(false);
      }
      _session.StartGame(_selectedMode);
    }

    private void OnAuthenticated(ApiResult<bool> result)
    {
      _authResolved = true;

      if (result.Success)
      {
        SetText(_statusLabel, string.Empty);
      }
      else
      {
        SetText(_statusLabel, "Offline mode — scores won't be saved");
        Debug.LogWarning($"[MainMenu] Auth failed: {result.Error}");
      }

      UpdatePlayButtonInteractable();
    }

    private void BuildModeButtons()
    {
      foreach (ModeButton existing in _spawnedButtons)
      {
        if (existing != null)
        {
          Destroy(existing.gameObject);
        }
      }
      _spawnedButtons.Clear();

      if (_modes.Count == 0 || _modeButtonContainer == null || _modeButtonPrefab == null)
      {
        return;
      }

      foreach (GameModeDefinition mode in _modes)
      {
        ModeButton instance = Instantiate(_modeButtonPrefab, _modeButtonContainer);
        instance.Initialize(mode, SelectMode);
        _spawnedButtons.Add(instance);
      }

      SelectMode(_modes[0]);
    }

    private void SelectMode(GameModeDefinition mode)
    {
      _selectedMode = mode;
      SetText(_selectedModeLabel, $"{mode.DisplayLabel}");
      UpdatePlayButtonInteractable();
    }

    private void UpdatePlayButtonInteractable()
    {
      bool ready = _selectedMode != null && _authResolved;
      SetActive(_playButton, ready);
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
