using System.Collections;
using FlickFest.Core;
using TMPro;
using UnityEngine;

namespace FlickFest.Presentation
{
    /// <summary>
    /// In-game heads-up display. Subscribes to <see cref="GameSession"/> events
    /// and updates TMP labels. Holds no game logic; every TMP field is null-safe
    /// so the UI can be wired up incrementally.
    /// </summary>
    public sealed class HUD : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private TextMeshProUGUI _scoreLabel;
        [SerializeField] private TextMeshProUGUI _comboLabel;
        [SerializeField] private TextMeshProUGUI _timerLabel;
        [SerializeField] private TextMeshProUGUI _countdownLabel;
        [SerializeField] private TextMeshProUGUI _modeLabel;

        [Tooltip("Combo count must reach this number before the combo label appears.")]
        [SerializeField, Min(2)] private int _comboDisplayThreshold = 2;

        private Coroutine _countdownRoutine;

        private void OnEnable()
        {
            if (_session == null)
            {
                return;
            }

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
                    break;
                case GameState.Countdown:
                    SetVisible(_scoreLabel, true);
                    SetVisible(_comboLabel, false);
                    SetVisible(_timerLabel, false);
                    SetVisible(_countdownLabel, true);
                    SetVisible(_modeLabel, true);
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
                    break;
            }
        }

        private void HandleScoreChanged(int score)
        {
            SetText(_scoreLabel, score.ToString("N0"));
        }

        private void HandleComboChanged(int combo)
        {
            if (combo >= _comboDisplayThreshold)
            {
                SetVisible(_comboLabel, true);
                SetText(_comboLabel, $"x{combo}");
            }
            else
            {
                SetVisible(_comboLabel, false);
            }
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

            SetVisible(_scoreLabel, false);
            SetVisible(_comboLabel, false);
            SetVisible(_timerLabel, false);
            SetVisible(_countdownLabel, false);
            SetVisible(_modeLabel, false);
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
