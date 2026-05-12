using System.Reflection;
using FlickFest.Core;
using NUnit.Framework;
using UnityEngine;

namespace FlickFest.Tests.EditMode
{
    public class ScoreKeeperTests
    {
        private static GameModeDefinition MakeMode(
            int hitPoints = 100,
            int missPenalty = 50,
            int negativePenalty = 200,
            bool comboEnabled = true,
            EndCondition endCondition = EndCondition.TimeLimit,
            int maxActiveTargets = 1,
            float spawnInterval = 0.5f,
            float targetLifetime = 2f,
            float negativeChance = 0f)
        {
            var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
            ModeHelpers.SetField(mode, "_gameModeName", "test");
            ModeHelpers.SetField(mode, "_displayLabel", "Test");
            ModeHelpers.SetField(mode, "_endCondition", endCondition);
            ModeHelpers.SetField(mode, "_duration", 30f);
            ModeHelpers.SetField(mode, "_targetGoal", 20);
            ModeHelpers.SetField(mode, "_maxActiveTargets", maxActiveTargets);
            ModeHelpers.SetField(mode, "_spawnInterval", spawnInterval);
            ModeHelpers.SetField(mode, "_targetLifetime", targetLifetime);
            ModeHelpers.SetField(mode, "_targetRadius", 0.05f);
            ModeHelpers.SetField(mode, "_hitPoints", hitPoints);
            ModeHelpers.SetField(mode, "_missPenalty", missPenalty);
            ModeHelpers.SetField(mode, "_negativePenalty", negativePenalty);
            ModeHelpers.SetField(mode, "_comboEnabled", comboEnabled);
            ModeHelpers.SetField(mode, "_negativeTargetChance", negativeChance);
            return mode;
        }

        private static TargetData MakePositive(float spawnTime = 0f) =>
            new TargetData(id: 1, normalizedPosition: Vector2.one * 0.5f, spawnTime: spawnTime,
                lifetime: 2f, type: TargetType.Positive, radius: 0.05f);

        private static TargetData MakeNegative(float spawnTime = 0f) =>
            new TargetData(id: 2, normalizedPosition: Vector2.one * 0.5f, spawnTime: spawnTime,
                lifetime: 2f, type: TargetType.Negative, radius: 0.05f);

        [Test]
        public void PositiveHit_AddsBasePoints_WhenSlowEnoughForNoSpeedBonus()
        {
            var mode = MakeMode(hitPoints: 100, comboEnabled: false);
            var keeper = new ScoreKeeper(mode);

            HitResult result = keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0.5f);

            Assert.That(result.PointsDelta, Is.EqualTo(100));
            Assert.That(keeper.CurrentScore, Is.EqualTo(100));
            Assert.That(keeper.ComboCount, Is.EqualTo(1));
            Assert.That(result.WasNegative, Is.False);
        }

        [Test]
        public void PositiveHit_AppliesSpeedBonus_AtFullSpeed()
        {
            var mode = MakeMode(hitPoints: 100, comboEnabled: false);
            var keeper = new ScoreKeeper(mode);

            HitResult result = keeper.RegisterHit(MakePositive(), reactionMs: 0f, clickOffsetNormalized: 0f);

            Assert.That(result.PointsDelta, Is.EqualTo(150));
            Assert.That(keeper.CurrentScore, Is.EqualTo(150));
        }

        [Test]
        public void PositiveHit_AppliesNoSpeedBonus_AtBoundary()
        {
            var mode = MakeMode(hitPoints: 100, comboEnabled: false);
            var keeper = new ScoreKeeper(mode);

            HitResult result = keeper.RegisterHit(MakePositive(), reactionMs: 1000f, clickOffsetNormalized: 0f);

            Assert.That(result.PointsDelta, Is.EqualTo(100));
        }

        [Test]
        public void NegativeHit_DeductsPenalty_ResetsCombo_FloorAtZero()
        {
            var mode = MakeMode(hitPoints: 100, negativePenalty: 200, comboEnabled: false);
            var keeper = new ScoreKeeper(mode);
            // Bring score to 100 first.
            keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f);
            Assert.That(keeper.CurrentScore, Is.EqualTo(100));

            HitResult result = keeper.RegisterHit(MakeNegative(), reactionMs: 500f, clickOffsetNormalized: 0f);

            Assert.That(keeper.CurrentScore, Is.EqualTo(0));
            Assert.That(keeper.ComboCount, Is.EqualTo(0));
            Assert.That(result.WasNegative, Is.True);
            Assert.That(result.PointsDelta, Is.EqualTo(-100));
        }

        [Test]
        public void Miss_ResetsCombo_DeductsPenalty_FloorAtZero()
        {
            var mode = MakeMode(hitPoints: 100, missPenalty: 50, comboEnabled: false);
            var keeper = new ScoreKeeper(mode);
            keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f);
            Assert.That(keeper.CurrentScore, Is.EqualTo(100));

            keeper.RegisterMiss();
            Assert.That(keeper.CurrentScore, Is.EqualTo(50));
            Assert.That(keeper.ComboCount, Is.EqualTo(0));

            // Two more misses should floor at 0, not go negative.
            keeper.RegisterMiss();
            keeper.RegisterMiss();
            Assert.That(keeper.CurrentScore, Is.EqualTo(0));
        }

        [Test]
        public void ComboMultiplier_AppliesAtDocumentedBrackets()
        {
            var mode = MakeMode(hitPoints: 100, comboEnabled: true);
            var keeper = new ScoreKeeper(mode);

            int Hit() => keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f).PointsDelta;

            Assert.That(Hit(), Is.EqualTo(100), "combo 1 → 1x");
            Assert.That(Hit(), Is.EqualTo(100), "combo 2 → 1x");
            Assert.That(Hit(), Is.EqualTo(100), "combo 3 → 1x");
            Assert.That(Hit(), Is.EqualTo(100), "combo 4 → 1x");
            Assert.That(Hit(), Is.EqualTo(100), "combo 5 → 1x");
            Assert.That(Hit(), Is.EqualTo(200), "combo 6 → 2x");
            for (int i = 7; i <= 10; i++)
            {
                Assert.That(Hit(), Is.EqualTo(200), $"combo {i} → 2x");
            }
            for (int i = 11; i <= 15; i++)
            {
                Assert.That(Hit(), Is.EqualTo(300), $"combo {i} → 3x");
            }
        }

        [Test]
        public void ComboMultiplier_CapsAt5x()
        {
            var mode = MakeMode(hitPoints: 100, comboEnabled: true);
            var keeper = new ScoreKeeper(mode);

            // Drive combo to 26.
            for (int i = 0; i < 25; i++)
            {
                keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f);
            }

            HitResult result = keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f);
            Assert.That(keeper.ComboCount, Is.EqualTo(26));
            Assert.That(result.PointsDelta, Is.EqualTo(500), "combo 26 caps at 5x");
        }

        [Test]
        public void Events_FireOnScoreAndComboChanges()
        {
            var mode = MakeMode(comboEnabled: false);
            var keeper = new ScoreKeeper(mode);

            int scoreEvents = 0;
            int comboEvents = 0;
            int lastScore = -1;
            int lastCombo = -1;
            keeper.OnScoreChanged += s => { scoreEvents++; lastScore = s; };
            keeper.OnComboChanged += c => { comboEvents++; lastCombo = c; };

            keeper.RegisterHit(MakePositive(), reactionMs: 1500f, clickOffsetNormalized: 0f);
            Assert.That(scoreEvents, Is.EqualTo(1));
            Assert.That(comboEvents, Is.EqualTo(1));
            Assert.That(lastScore, Is.EqualTo(100));
            Assert.That(lastCombo, Is.EqualTo(1));

            keeper.RegisterMiss();
            Assert.That(comboEvents, Is.EqualTo(2));
            Assert.That(lastCombo, Is.EqualTo(0));
        }
    }
}
