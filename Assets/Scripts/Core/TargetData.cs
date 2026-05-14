using UnityEngine;

namespace FlickFest.Core
{
  /// <summary>
  /// Pure-data description of a live target. Created by <see cref="TargetSpawner"/>,
  /// consumed by the presentation layer and <see cref="GameSession"/>.
  /// </summary>
  public sealed class TargetData
  {
    public int Id { get; }
    public Vector2 NormalizedPosition { get; }
    public float SpawnTime { get; }
    public float Lifetime { get; }
    public TargetType Type { get; }
    public float Radius { get; }

    public TargetData(
        int id,
        Vector2 normalizedPosition,
        float spawnTime,
        float lifetime,
        TargetType type,
        float radius)
    {
      Id = id;
      NormalizedPosition = normalizedPosition;
      SpawnTime = spawnTime;
      Lifetime = lifetime;
      Type = type;
      Radius = radius;
    }
  }
}
