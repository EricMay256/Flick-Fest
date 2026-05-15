using System;

namespace FlickFest.Core
{
  /// <summary>
  /// Accumulates score for a single session. Pure logic — no Unity dependencies.
  /// One instance per session; do not reuse across sessions.
  /// </summary>
  public sealed class ScoreKeeper
  {
    private readonly GameModeDefinition _mode;

    public event Action<int> OnScoreChanged;
    public event Action<int> OnComboChanged;

    public int CurrentScore { get; private set; }
    public int ComboCount { get; private set; }

    public ScoreKeeper(GameModeDefinition mode)
    {
      _mode = mode ?? throw new ArgumentNullException(nameof(mode));
      CurrentScore = 0;
      ComboCount = 0;
    }

    public HitResult RegisterHit(TargetData target, float reactionMs, float clickOffsetNormalized)
    {
      // clickOffsetNormalized is reserved for a future center-accuracy bonus.
      // The parameter is threaded through the call chain so the signature stays stable.
      _ = clickOffsetNormalized;

      if (target.Type == TargetType.Negative)
      {
        ComboCount = 0;
        OnComboChanged?.Invoke(ComboCount);

        var oldScore = CurrentScore;
        CurrentScore = Math.Max(0, CurrentScore - _mode.NegativePenalty);
        OnScoreChanged?.Invoke(CurrentScore);

        var delta = CurrentScore - oldScore;
        return new HitResult(delta, ComboCount, wasNegative: true, reactionMs);
      }

      ComboCount++;

      int basePoints = _mode.HitPoints;
      int multiplier = _mode.ComboEnabled ? ComboMultiplier(ComboCount) : 1;
      int speedBonus = reactionMs < 1000f
          ? (int)(50f * (1f - reactionMs / 1000f))
          : 0;

      int delta2 = basePoints * multiplier + speedBonus;
      CurrentScore += delta2;
      OnScoreChanged?.Invoke(CurrentScore);
      OnComboChanged?.Invoke(ComboCount);

      return new HitResult(delta2, ComboCount, wasNegative: false, reactionMs);
    }

    public void RegisterMiss()
    {
      ComboCount = 0;
      OnComboChanged?.Invoke(ComboCount);

      if (_mode.MissPenalty > 0)
      {
        CurrentScore = Math.Max(0, CurrentScore - _mode.MissPenalty);
        OnScoreChanged?.Invoke(CurrentScore);
      }
    }

    /// <summary>Hits per multiplier tier. 1-5 = 1x, 6-10 = 2x, etc.</summary>
    public const int HitsPerTier = 5;

    /// <summary>Highest multiplier tier the combo can reach.</summary>
    public const int MaxMultiplier = 5;

    // Spec: "Hits 1–5 = 1x, 6–10 = 2x, 11–15 = 3x, etc. Capped at 5x."
    // Ceiling division of comboCount/5 produces the documented brackets;
    // the literal "(ComboCount / 5)" in the spec uses integer division, which
    // gives 1x at combo 6. The example values are the source of truth — see
    // README "Decisions made" for the rationale.
    public static int ComboMultiplier(int comboCount)
    {
      if (comboCount <= 0)
      {
        return 1;
      }

      int ceil = (comboCount + HitsPerTier - 1) / HitsPerTier;
      return Math.Min(ceil, MaxMultiplier);
    }

    /// <summary>
    /// How many hits the player has accumulated within the current multiplier tier.
    /// Returns 0 when no combo is active, and clamps at <see cref="HitsPerTier"/>
    /// once the cap is reached so the progress bar can stay pinned full.
    /// </summary>
    public static int HitsInCurrentTier(int comboCount)
    {
      if (comboCount <= 0)
      {
        return 0;
      }
      int multiplier = ComboMultiplier(comboCount);
      if (multiplier >= MaxMultiplier)
      {
        return HitsPerTier;
      }
      int withinTier = ((comboCount - 1) % HitsPerTier) + 1;
      return withinTier;
    }
  }
}
