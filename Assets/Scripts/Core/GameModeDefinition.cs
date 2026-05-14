using UnityEngine;

namespace FlickFest.Core
{
  /// <summary>
  /// Designer-editable configuration for a single game mode. One asset per mode.
  /// Contains everything the client needs to run a session; the server only knows
  /// <see cref="GameModeName"/>, <see cref="DisplayLabel"/>, and a sort order.
  /// </summary>
  [CreateAssetMenu(fileName = "GameMode", menuName = "FlickFest/Game Mode")]
  public sealed class GameModeDefinition : ScriptableObject
  {
    #region Server Mapping

    [Header("Server Mapping")]
    [Tooltip("Must match the name registered on the leaderboard server. Passed to SubmitScore.")]
    [SerializeField] private string _gameModeName = "precision";

    [Tooltip("Player-facing label shown in the mode selector UI.")]
    [SerializeField] private string _displayLabel = "Precision";

    public string GameModeName => _gameModeName;
    public string DisplayLabel => _displayLabel;

    #endregion
    #region Session Rules

    [Header("Session Rules")]
    [SerializeField] private EndCondition _endCondition = EndCondition.TimeLimit;

    [Tooltip("Session length in seconds. Ignored when EndCondition is TargetCount.")]
    [SerializeField, Min(1f)] private float _duration = 30f;

    [Tooltip("Targets to hit before the session ends. Ignored when EndCondition is TimeLimit.")]
    [SerializeField, Min(1)] private int _targetGoal = 20;

    public EndCondition EndCondition => _endCondition;
    public float Duration => _duration;
    public int TargetGoal => _targetGoal;

    #endregion
    #region Spawning

    [Header("Spawning")]
    [Tooltip("Max targets alive at the same time. 1 = sequential, >1 = concurrent.")]
    [SerializeField, Min(1)] private int _maxActiveTargets = 1;

    [Tooltip("Seconds between spawn attempts when below the cap. Only meaningful when MaxActiveTargets > 1.")]
    [SerializeField, Min(0f)] private float _spawnInterval = 0.5f;

    [Tooltip("How long a target lives before expiring.")]
    [SerializeField, Min(0.1f)] private float _targetLifetime = 2.0f;

    [Tooltip("Normalized radius of spawned targets (0-1 relative to play area width).")]
    [SerializeField, Range(0.01f, 0.25f)] private float _targetRadius = 0.05f;

    public int MaxActiveTargets => _maxActiveTargets;
    public float SpawnInterval => _spawnInterval;
    public float TargetLifetime => _targetLifetime;
    public float TargetRadius => _targetRadius;

    #endregion
    #region Scoring

    [Header("Scoring")]
    [SerializeField] private int _hitPoints = 100;
    [SerializeField] private int _missPenalty = 50;
    [SerializeField] private int _negativePenalty = 200;
    [SerializeField] private bool _comboEnabled = true;

    public int HitPoints => _hitPoints;
    public int MissPenalty => _missPenalty;
    public int NegativePenalty => _negativePenalty;
    public bool ComboEnabled => _comboEnabled;

    #endregion
    #region Target Variety

    [Header("Target Variety")]
    [Tooltip("Probability (0-1) that a spawned target is negative. 0 disables negatives.")]
    [SerializeField, Range(0f, 1f)] private float _negativeTargetChance = 0f;

    public float NegativeTargetChance => _negativeTargetChance;

    #endregion
  }
}
