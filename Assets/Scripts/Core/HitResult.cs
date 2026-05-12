namespace FlickFest.Core
{
    /// <summary>
    /// Returned by <see cref="ScoreKeeper.RegisterHit"/>. Carries everything the
    /// presentation layer needs for feedback without re-deriving scoring logic.
    /// </summary>
    public sealed class HitResult
    {
        public int PointsDelta { get; }
        public int ComboCount { get; }
        public bool WasNegative { get; }
        public float ReactionMs { get; }

        public HitResult(int pointsDelta, int comboCount, bool wasNegative, float reactionMs)
        {
            PointsDelta = pointsDelta;
            ComboCount = comboCount;
            WasNegative = wasNegative;
            ReactionMs = reactionMs;
        }
    }
}
