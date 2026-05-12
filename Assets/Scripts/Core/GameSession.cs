using System;
using System.Collections;
using UnityEngine;
using UBear.Leaderboard;

namespace FlickFest.Core
{
    /// <summary>
    /// Central state machine for a play session. Owns lifecycle, emits events,
    /// and delegates to <see cref="ScoreKeeper"/> and <see cref="TargetSpawner"/>.
    /// Never touches visuals directly — the presentation layer subscribes to events.
    /// </summary>
    public sealed class GameSession : MonoBehaviour
    {
        [SerializeField] private LeaderboardService _leaderboardService;

        [Tooltip("Seconds of countdown before play begins.")]
        [SerializeField, Min(0f)] private float _countdownSeconds = 3f;

        private ScoreKeeper _scoreKeeper;
        private TargetSpawner _spawner;
        private GameState _state = GameState.MainMenu;
        private bool _sessionActive;
        private float _timeRemaining;
        private int _targetsHit;

        public event Action<GameState> OnStateChanged;
        public event Action<TargetData> OnTargetSpawned;
        public event Action<TargetData, HitResult> OnTargetHit;
        public event Action<TargetData> OnTargetExpired;
        public event Action OnMisclick;
        public event Action<int> OnScoreChanged;
        public event Action<int> OnComboChanged;
        public event Action<float> OnTimerTick;
        public event Action<int, string> OnGameOver;

        public GameState CurrentState => _state;
        public GameModeDefinition ActiveMode { get; private set; }
        public LeaderboardService LeaderboardService => _leaderboardService;

        #region Public API — Presentation Layer

        public void StartGame(GameModeDefinition mode)
        {
            if (mode == null)
            {
                throw new ArgumentNullException(nameof(mode));
            }

            ActiveMode = mode;
            _scoreKeeper = new ScoreKeeper(mode);
            _spawner = new TargetSpawner(mode);
            _targetsHit = 0;

            _scoreKeeper.OnScoreChanged += HandleScoreChanged;
            _scoreKeeper.OnComboChanged += HandleComboChanged;

            StopAllCoroutines();
            StartCoroutine(CountdownSequence());
        }

        public void ReportHit(TargetData target, float clickOffsetNormalized)
        {
            if (!_sessionActive)
            {
                return;
            }

            float reactionMs = (Time.time - target.SpawnTime) * 1000f;
            HitResult result = _scoreKeeper.RegisterHit(target, reactionMs, clickOffsetNormalized);
            OnTargetHit?.Invoke(target, result);

            if (ActiveMode.EndCondition == EndCondition.TargetCount)
            {
                _targetsHit++;
                if (_targetsHit >= ActiveMode.TargetGoal)
                {
                    EndSession();
                }
            }
        }

        public void ReportExpired(TargetData target)
        {
            if (!_sessionActive)
            {
                return;
            }

            _scoreKeeper.RegisterMiss();
            OnTargetExpired?.Invoke(target);
        }

        public void ReportMisclick()
        {
            if (!_sessionActive)
            {
                return;
            }

            _scoreKeeper.RegisterMiss();
            OnMisclick?.Invoke();
        }

        public TargetData TrySpawnTarget()
        {
            if (!_sessionActive)
            {
                return null;
            }

            TargetData data = _spawner.TrySpawn(Time.time);
            if (data != null)
            {
                OnTargetSpawned?.Invoke(data);
            }
            return data;
        }

        public void NotifyTargetResolved()
        {
            _spawner?.NotifyResolved();
        }

        /// <summary>
        /// Wraps <see cref="LeaderboardService.SubmitScore"/> so that
        /// <c>GameOverPanel</c> doesn't need its own service reference.
        /// </summary>
        public void SubmitScore(int score, string gameMode, Action<ApiResult<ScoreResponse>> callback)
        {
            if (_leaderboardService == null)
            {
                callback?.Invoke(ApiResult<ScoreResponse>.Fail("LeaderboardService not assigned."));
                return;
            }

            StartCoroutine(_leaderboardService.SubmitScore(score, gameMode, callback));
        }

        /// <summary>
        /// Wraps <see cref="LeaderboardService.GetScores"/>. <paramref name="period"/>
        /// accepts the wire-format strings ("alltime", "daily", "weekly").
        /// </summary>
        public void FetchLeaderboard(
            string gameMode,
            Action<ApiResult<LeaderboardResponse>> callback,
            string period = "alltime")
        {
            if (_leaderboardService == null)
            {
                callback?.Invoke(ApiResult<LeaderboardResponse>.Fail("LeaderboardService not assigned."));
                return;
            }

            TimePeriod parsed = ParsePeriod(period);
            StartCoroutine(_leaderboardService.GetScores(gameMode, callback, parsed));
        }

        #endregion
        #region Lifecycle

        private void Update()
        {
            if (!_sessionActive)
            {
                return;
            }

            if (ActiveMode.EndCondition == EndCondition.TimeLimit)
            {
                _timeRemaining -= Time.deltaTime;
                OnTimerTick?.Invoke(Mathf.Max(0f, _timeRemaining));
                if (_timeRemaining <= 0f)
                {
                    EndSession();
                }
            }
            else
            {
                _timeRemaining += Time.deltaTime;
                OnTimerTick?.Invoke(_timeRemaining);
            }
        }

        private IEnumerator CountdownSequence()
        {
            SetState(GameState.Countdown);
            yield return new WaitForSeconds(_countdownSeconds);
            StartSession();
        }

        private void StartSession()
        {
            _timeRemaining = ActiveMode.EndCondition == EndCondition.TimeLimit
                ? ActiveMode.Duration
                : 0f;
            _sessionActive = true;
            SetState(GameState.Playing);
        }

        private void EndSession()
        {
            _sessionActive = false;

            int finalScore = ActiveMode.EndCondition == EndCondition.TargetCount
                ? Mathf.RoundToInt(_timeRemaining * 1000f)
                : _scoreKeeper.CurrentScore;

            SetState(GameState.GameOver);
            OnGameOver?.Invoke(finalScore, ActiveMode.GameModeName);
        }

        private void SetState(GameState next)
        {
            _state = next;
            OnStateChanged?.Invoke(_state);
        }

        private void OnDisable()
        {
            if (_scoreKeeper != null)
            {
                _scoreKeeper.OnScoreChanged -= HandleScoreChanged;
                _scoreKeeper.OnComboChanged -= HandleComboChanged;
            }
        }

        #endregion
        #region Helpers

        private void HandleScoreChanged(int score) => OnScoreChanged?.Invoke(score);
        private void HandleComboChanged(int combo) => OnComboChanged?.Invoke(combo);

        private static TimePeriod ParsePeriod(string period) =>
            period?.ToLowerInvariant() switch
            {
                "daily" => TimePeriod.Daily,
                "weekly" => TimePeriod.Weekly,
                _ => TimePeriod.Alltime
            };

        #endregion
    }
}
