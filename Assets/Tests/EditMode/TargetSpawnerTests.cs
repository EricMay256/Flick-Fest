using FlickFest.Core;
using NUnit.Framework;
using UnityEngine;

namespace FlickFest.Tests.EditMode
{
  public class TargetSpawnerTests
  {
    private static GameModeDefinition MakeMode(
        int maxActive = 1,
        float spawnInterval = 0.5f,
        float negativeChance = 0f)
    {
      var mode = ScriptableObject.CreateInstance<GameModeDefinition>();
      ModeHelpers.SetField(mode, "_gameModeName", "test");
      ModeHelpers.SetField(mode, "_displayLabel", "Test");
      ModeHelpers.SetField(mode, "_endCondition", EndCondition.TimeLimit);
      ModeHelpers.SetField(mode, "_duration", 30f);
      ModeHelpers.SetField(mode, "_targetGoal", 20);
      ModeHelpers.SetField(mode, "_maxActiveTargets", maxActive);
      ModeHelpers.SetField(mode, "_spawnInterval", spawnInterval);
      ModeHelpers.SetField(mode, "_targetLifetime", 2f);
      ModeHelpers.SetField(mode, "_targetRadius", 0.05f);
      ModeHelpers.SetField(mode, "_hitPoints", 100);
      ModeHelpers.SetField(mode, "_missPenalty", 50);
      ModeHelpers.SetField(mode, "_negativePenalty", 200);
      ModeHelpers.SetField(mode, "_comboEnabled", true);
      ModeHelpers.SetField(mode, "_negativeTargetChance", negativeChance);
      return mode;
    }

    [Test]
    public void Sequential_SpawnsOnce_ThenBlocksUntilResolved()
    {
      var mode = MakeMode(maxActive: 1);
      var spawner = new TargetSpawner(mode);

      TargetData first = spawner.TrySpawn(currentTime: 0f);
      Assert.That(first, Is.Not.Null);
      Assert.That(spawner.ActiveCount, Is.EqualTo(1));

      // Subsequent attempts are blocked.
      Assert.That(spawner.TrySpawn(0.1f), Is.Null);
      Assert.That(spawner.TrySpawn(1f), Is.Null);
      Assert.That(spawner.TrySpawn(5f), Is.Null);

      spawner.NotifyResolved();
      Assert.That(spawner.ActiveCount, Is.EqualTo(0));

      TargetData second = spawner.TrySpawn(currentTime: 5f);
      Assert.That(second, Is.Not.Null);
      Assert.That(second.Id, Is.EqualTo(1), "IDs increment monotonically");
    }

    [Test]
    public void Concurrent_RespectsSpawnInterval()
    {
      var mode = MakeMode(maxActive: 5, spawnInterval: 0.5f);
      var spawner = new TargetSpawner(mode);

      TargetData first = spawner.TrySpawn(currentTime: 0f);
      Assert.That(first, Is.Not.Null);

      // Within the interval: no spawn.
      Assert.That(spawner.TrySpawn(0.1f), Is.Null);
      Assert.That(spawner.TrySpawn(0.4f), Is.Null);

      // Past the interval: spawn.
      TargetData second = spawner.TrySpawn(0.5f);
      Assert.That(second, Is.Not.Null);
      Assert.That(spawner.ActiveCount, Is.EqualTo(2));
    }

    [Test]
    public void Concurrent_RespectsActiveCap()
    {
      var mode = MakeMode(maxActive: 2, spawnInterval: 0.5f);
      var spawner = new TargetSpawner(mode);

      Assert.That(spawner.TrySpawn(0f), Is.Not.Null);
      Assert.That(spawner.TrySpawn(0.5f), Is.Not.Null);
      // Cap reached.
      Assert.That(spawner.TrySpawn(1f), Is.Null);

      spawner.NotifyResolved();
      Assert.That(spawner.TrySpawn(1f), Is.Not.Null);
    }

    [Test]
    public void NormalizedPosition_StaysWithinEdgeMargin()
    {
      var mode = MakeMode(maxActive: 1);
      var spawner = new TargetSpawner(mode);

      for (int i = 0; i < 50; i++)
      {
        TargetData data = spawner.TrySpawn(currentTime: i);
        Assert.That(data, Is.Not.Null);
        Assert.That(data.NormalizedPosition.x, Is.InRange(0.08f, 1f - 0.08f));
        Assert.That(data.NormalizedPosition.y, Is.InRange(0.08f, 1f - 0.08f));
        spawner.NotifyResolved();
      }
    }

    [Test]
    public void NegativeChanceZero_NeverProducesNegativeTargets()
    {
      var mode = MakeMode(maxActive: 1, negativeChance: 0f);
      var spawner = new TargetSpawner(mode);

      for (int i = 0; i < 30; i++)
      {
        TargetData data = spawner.TrySpawn(currentTime: i);
        Assert.That(data.Type, Is.EqualTo(TargetType.Positive));
        spawner.NotifyResolved();
      }
    }

    [Test]
    public void NotifyResolved_FloorsActiveCountAtZero()
    {
      var mode = MakeMode(maxActive: 1);
      var spawner = new TargetSpawner(mode);

      spawner.NotifyResolved();
      spawner.NotifyResolved();
      Assert.That(spawner.ActiveCount, Is.EqualTo(0));
    }
  }
}
