using System.Collections;
using FlickFest.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlickFest.Presentation
{
  /// <summary>
  /// In-game heads-up display. Subscribes to <see cref="GameSession"/> events
  /// and updates TMP labels. Holds no game logic; every TMP field is null-safe
  /// so the UI can be wired up incrementally.
  ///
  /// The combo display has two layers wired separately so legacy prefabs keep
  /// working: _comboLabel is the original "xN" text; the new multiplier block
  /// (_multiplierLabel + _comboCountLabel + _multiplierProgressBar) presents
  /// the multiplier prominently with the combo count as subtext and a fill bar
  /// showing progress toward the next tier (or the max-tier color when capped).
  /// </summary>
  public sealed class HUD : MonoBehaviour
  {
    [SerializeField] private GameSession _session;
    [SerializeField] private TextMeshProUGUI _scoreLabel;
    [SerializeField] private TextMeshProUGUI _comboLabel;
    [SerializeField] private TextMeshProUGUI _timerLabel;
    [SerializeField] private TextMeshProUGUI _countdownLabel;
    [SerializeField] private TextMeshProUGUI _modeLabel;

    [Header("Multiplier Block (optional)")]
    [Tooltip("Large \"3x\" multiplier label.")]
    [SerializeField] private TextMeshProUGUI _multiplierLabel;
    [Tooltip("Small combo counter shown beneath the multiplier (e.g. \"14 combo\").")]
    [SerializeField] private TextMeshProUGUI _comboCountLabel;
    [Tooltip("Filled-Image progress bar (Image type = Filled, Horizontal). FillAmount drives the visual.")]
    [SerializeField] private Image _multiplierProgressBar;
    [Tooltip("Optional root toggled together with the multiplier block. Defaults to _multiplierLabel's GameObject when null.")]
    [SerializeField] private GameObject _multiplierBlockRoot;

    [Header("Multiplier Colors")]
    [Tooltip("Progress bar color while climbing toward the next multiplier tier.")]
    [SerializeField] private Color _multiplierColorNormal = new Color(0.2f, 0.7f, 0.9f, 1f);
    [Tooltip("Progress bar color once the cap (5x) is reached.")]
    [SerializeField] private Color _multiplierColorMax = new Color(1f, 0.78f, 0.2f, 1f);
    [Tooltip("If set, the multiplier text changes to this color at max tier. Leave alpha at 0 to keep the original color.")]
    [SerializeField] private Color _multiplierTextColorMax = new Color(0f, 0f, 0f, 0f);

    [Tooltip("Combo count must reach this number before the combo label appears.")]
    [SerializeField, Min(2)] private int _comboDisplayThreshold = 2;

    private Coroutine _countdownRoutine;
    private Color _multiplierTextColorOriginal;
    private bool _multiplierTextColorCaptured;

    private void OnEnable()
    {
      if (_session == null)
      {
        return;
      }

      CaptureMultiplierTextColor();

      _session.OnStateChanged += HandleStateChanged;
      _session.OnScoreChanged += HandleScoreChanged;
      _session.OnComboChanged += HandleComboChanged;
      _session.OnTimerTick += HandleTimerTick;

      ResetLabels();
    }

    private void OnDisable()
    {
      if (_session == null)
      {
        return;
      }

      _session.OnStateChanged -= HandleStateChanged;
      _session.OnScoreChanged -= HandleScoreChanged;
      _session.OnComboChanged -= HandleComboChanged;
      _session.OnTimerTick -= HandleTimerTick;
    }

    private void HandleStateChanged(GameState state)
    {
      switch (state)
      {
        case GameState.MainMenu:
          SetVisible(_scoreLabel, false);
          SetVisible(_comboLabel, false);
          SetVisible(_timerLabel, false);
          SetVisible(_countdownLabel, false);
          SetVisible(_modeLabel, false);
          SetMultiplierBlockVisible(false);
          break;
        case GameState.Countdown:
          SetVisible(_scoreLabel, true);
          SetVisible(_comboLabel, false);
          SetVisible(_timerLabel, false);
          SetVisible(_countdownLabel, true);
          SetVisible(_modeLabel, true);
          SetMultiplierBlockVisible(false);
          SetText(_modeLabel, _session.ActiveMode != null ? _session.ActiveMode.DisplayLabel : string.Empty);
          SetText(_scoreLabel, "0");
          StartCountdownRoutine();
          break;
        case GameState.Playing:
          SetVisible(_countdownLabel, false);
          SetVisible(_timerLabel, true);
          break;
        case GameState.GameOver:
          SetVisible(_scoreLabel, false);
          SetVisible(_comboLabel, false);
          SetVisible(_timerLabel, false);
          SetVisible(_countdownLabel, false);
          SetVisible(_modeLabel, false);
          SetMultiplierBlockVisible(false);
          break;
      }
    }

    private void HandleScoreChanged(int score)
    {
      SetText(_scoreLabel, score.ToString("N0"));
    }

    private void HandleComboChanged(int combo)
    {
      // Legacy single-label fallback.
      if (combo >= _comboDisplayThreshold)
      {
        SetVisible(_comboLabel, true);
        SetText(_comboLabel, $"x{combo}");
      }
      else
      {
        SetVisible(_comboLabel, false);
      }

      UpdateMultiplierBlock(combo);
    }

    private void UpdateMultiplierBlock(int combo)
    {
      bool modeRespectsCombo = _session.ActiveMode == null || _session.ActiveMode.ComboEnabled;
      bool show = modeRespectsCombo && combo >= _comboDisplayThreshold;
      SetMultiplierBlockVisible(show);
      if (!show)
      {
        return;
      }

      int multiplier = ScoreKeeper.ComboMultiplier(combo);
      int hitsInTier = ScoreKeeper.HitsInCurrentTier(combo);
      bool atMax = multiplier >= ScoreKeeper.MaxMultiplier;
      float fill = atMax ? 1f : Mathf.Clamp01((float)hitsInTier / ScoreKeeper.HitsPerTier);

      if (_multiplierLabel != null)
      {
        _multiplierLabel.text = $"{multiplier}x";
        if (_multiplierTextColorMax.a > 0f && _multiplierTextColorCaptured)
        {
          _multiplierLabel.color = atMax ? _multiplierTextColorMax : _multiplierTextColorOriginal;
        }
      }
      if (_comboCountLabel != null)
      {
        _comboCountLabel.text = $"{combo} combo";
      }
      if (_multiplierProgressBar != null)
      {
        _multiplierProgressBar.fillAmount = fill;
        _multiplierProgressBar.color = atMax ? _multiplierColorMax : _multiplierColorNormal;
      }
    }

    private void SetMultiplierBlockVisible(bool visible)
    {
      GameObject root = _multiplierBlockRoot != null
          ? _multiplierBlockRoot
          : (_multiplierLabel != null ? _multiplierLabel.gameObject : null);

      if (root != null)
      {
        if (root.activeSelf != visible)
        {
          root.SetActive(visible);
        }
        // When a root is wired, individual labels are children of it. No need
        // to toggle them separately; their parent governs visibility.
        return;
      }

      // No shared root — toggle the individual pieces directly.
      SetVisible(_multiplierLabel, visible);
      SetVisible(_comboCountLabel, visible);
      if (_multiplierProgressBar != null && _multiplierProgressBar.gameObject.activeSelf != visible)
      {
        _multiplierProgressBar.gameObject.SetActive(visible);
      }
    }

    private void CaptureMultiplierTextColor()
    {
      if (_multiplierTextColorCaptured || _multiplierLabel == null)
      {
        return;
      }
      _multiplierTextColorOriginal = _multiplierLabel.color;
      _multiplierTextColorCaptured = true;
    }

    private void HandleTimerTick(float value)
    {
      if (_timerLabel == null || _session.ActiveMode == null)
      {
        return;
      }

      _timerLabel.text = _session.ActiveMode.EndCondition == EndCondition.TimeLimit
          ? value.ToString("F1")
          : $"{value:F1}s";
    }

    private void StartCountdownRoutine()
    {
      if (_countdownLabel == null)
      {
        return;
      }

      if (_countdownRoutine != null)
      {
        StopCoroutine(_countdownRoutine);
      }
      _countdownRoutine = StartCoroutine(CountdownLabels());
    }

    private IEnumerator CountdownLabels()
    {
      var wait = new WaitForSeconds(1f);
      _countdownLabel.text = "3";
      yield return wait;
      _countdownLabel.text = "2";
      yield return wait;
      _countdownLabel.text = "1";
      yield return wait;
      _countdownLabel.text = "GO!";
      _countdownRoutine = null;
    }

    private void ResetLabels()
    {
      SetText(_scoreLabel, "0");
      SetText(_comboLabel, string.Empty);
      SetText(_timerLabel, string.Empty);
      SetText(_countdownLabel, string.Empty);
      SetText(_modeLabel, string.Empty);
      SetText(_multiplierLabel, string.Empty);
      SetText(_comboCountLabel, string.Empty);

      SetVisible(_scoreLabel, false);
      SetVisible(_comboLabel, false);
      SetVisible(_timerLabel, false);
      SetVisible(_countdownLabel, false);
      SetVisible(_modeLabel, false);
      SetMultiplierBlockVisible(false);

      if (_multiplierProgressBar != null)
      {
        _multiplierProgressBar.fillAmount = 0f;
        _multiplierProgressBar.color = _multiplierColorNormal;
      }
    }

    private static void SetText(TextMeshProUGUI label, string text)
    {
      if (label != null)
      {
        label.text = text;
      }
    }

    private static void SetVisible(TextMeshProUGUI label, bool visible)
    {
      if (label != null && label.gameObject.activeSelf != visible)
      {
        label.gameObject.SetActive(visible);
      }
    }
  }
}
