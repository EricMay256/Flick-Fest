using System;
using UnityEngine;

namespace FlickFest.Core
{
    /// <summary>
    /// Decides when and where targets appear. Pure logic — positions are
    /// normalized 0-1, time is taken as a parameter. Owns no scene state.
    /// </summary>
    public sealed class TargetSpawner
    {
        private const float EdgeMargin = 0.08f;

        private readonly GameModeDefinition _mode;

        private int _activeCount;
        private int _nextId;
        private float _lastSpawnTime = float.NegativeInfinity;
        private bool _awaitingResolution;

        public TargetSpawner(GameModeDefinition mode)
        {
            _mode = mode ?? throw new ArgumentNullException(nameof(mode));
        }

        public int ActiveCount => _activeCount;

        /// <summary>
        /// Returns a freshly built <see cref="TargetData"/> if a spawn should occur
        /// at <paramref name="currentTime"/>, otherwise <c>null</c>.
        /// </summary>
        public TargetData TrySpawn(float currentTime)
        {
            if (_activeCount >= _mode.MaxActiveTargets)
            {
                return null;
            }

            bool sequential = _mode.MaxActiveTargets == 1;
            if (sequential && _awaitingResolution)
            {
                return null;
            }

            if (!sequential && currentTime - _lastSpawnTime < _mode.SpawnInterval)
            {
                return null;
            }

            var data = new TargetData(
                id: _nextId++,
                normalizedPosition: RandomPosition(),
                spawnTime: currentTime,
                lifetime: _mode.TargetLifetime,
                type: RollType(),
                radius: _mode.TargetRadius);

            _activeCount++;
            _lastSpawnTime = currentTime;
            if (sequential)
            {
                _awaitingResolution = true;
            }

            return data;
        }

        public void NotifyResolved()
        {
            _activeCount = Math.Max(0, _activeCount - 1);
            _awaitingResolution = false;
        }

        private static Vector2 RandomPosition()
        {
            float x = UnityEngine.Random.Range(EdgeMargin, 1f - EdgeMargin);
            float y = UnityEngine.Random.Range(EdgeMargin, 1f - EdgeMargin);
            return new Vector2(x, y);
        }

        private TargetType RollType()
        {
            if (_mode.NegativeTargetChance <= 0f)
            {
                return TargetType.Positive;
            }

            return UnityEngine.Random.value < _mode.NegativeTargetChance
                ? TargetType.Negative
                : TargetType.Positive;
        }
    }
}
