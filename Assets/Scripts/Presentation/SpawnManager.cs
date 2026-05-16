using FlickFest.Core;
using UnityEngine;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Polls <see cref="GameSession.TrySpawnTarget"/> each frame, instantiates
  /// <see cref="TargetView"/> prefabs, and maps normalized positions to world
  /// coordinates derived from the active camera's viewport.
  ///
  /// Viewport-driven bounds keep the play area filling the screen at every
  /// aspect ratio. The attached <see cref="BoxCollider2D"/> is retained for
  /// editor visualization and as a fallback when no camera is available; it
  /// no longer drives spawn placement at runtime.
  /// </summary>
  [RequireComponent(typeof(BoxCollider2D))]
  public sealed class SpawnManager : MonoBehaviour
  {
    [SerializeField] private GameSession _session;
    [SerializeField] private GameObject _targetPrefab;

    [Tooltip("Optional parent for spawned targets. If null, targets spawn at the scene root.")]
    [SerializeField] private Transform _targetParent;

    [Tooltip("Camera used to derive the spawn area. Falls back to Camera.main when null.")]
    [SerializeField] private Camera _camera;

    private BoxCollider2D _fallbackBounds;

    private void Awake()
    {
      _fallbackBounds = GetComponent<BoxCollider2D>();
      if (_camera == null)
      {
        _camera = Camera.main;
      }
    }

    private void Update()
    {
      if (_session == null || _targetPrefab == null)
      {
        return;
      }

      TargetData data = _session.TrySpawnTarget();
      if (data == null)
      {
        return;
      }

      Bounds b = ResolveSpawnBounds();
      var worldPos = new Vector3(
          Mathf.Lerp(b.min.x, b.max.x, data.NormalizedPosition.x),
          Mathf.Lerp(b.min.y, b.max.y, data.NormalizedPosition.y),
          0f);

      // Radius normalizes to the shorter edge: this keeps the worst-case
      // D/W (Fitts difficulty) roughly invariant across aspect ratios, at
      // the cost of smaller absolute targets on wider screens. Trade-off
      // chosen for fairness across portrait/landscape.
      float worldRadius = data.Radius * Mathf.Min(b.size.x, b.size.y);
      
      var instance = Instantiate(_targetPrefab, worldPos, Quaternion.identity, _targetParent);
      if (instance.TryGetComponent(out TargetView view))
      {
        view.Initialize(data, _session, worldPos, worldRadius);
      }
      else
      {
        Debug.LogError($"[SpawnManager] Target prefab is missing a {nameof(TargetView)} component.");
      }
    }

    /// <summary>
    /// Build a world-space rectangle matching the camera's current viewport.
    /// Orthographic cameras only — the project is 2D. Falls back to the
    /// component's collider if no camera is wired up, preserving prior
    /// behavior for any edit-time or headless contexts.
    /// </summary>
    private Bounds ResolveSpawnBounds()
    {
      if (_camera == null)
      {
        _camera = Camera.main;
      }

      if (_camera == null || !_camera.orthographic)
      {
        return _fallbackBounds.bounds;
      }

      float halfHeight = _camera.orthographicSize;
      float halfWidth = halfHeight * _camera.aspect;
      Vector3 center = _camera.transform.position;
      // Bounds is built in world space; the camera's z is irrelevant for 2D
      // spawning so we project to the spawn plane (z=0).
      return new Bounds(
          new Vector3(center.x, center.y, 0f),
          new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));
    }
  }
}