using FlickFest.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Translates new Input System mouse clicks into the same semantics
  /// Unity's legacy <c>OnMouseDown</c> would provide: raycast against 2D
  /// colliders, dispatch to a <see cref="TargetView"/> if found, otherwise
  /// to the play area's <see cref="MisclickCatcher"/>.
  ///
  /// Required because the project's active input handler is set to
  /// "Input System Package (New)" — legacy mouse callbacks do not fire
  /// under that setting and ProjectSettings/ is out of scope for this work.
  /// </summary>
  public sealed class ClickRouter : MonoBehaviour
  {
    [SerializeField] private GameSession _session;
    [SerializeField] private MisclickCatcher _misclickCatcher;
    [SerializeField] private Camera _camera;

    [Tooltip("Layer mask used when raycasting for targets. Default: everything.")]
    [SerializeField] private LayerMask _targetMask = ~0;

    private void Awake()
    {
      if (_camera == null)
      {
        _camera = Camera.main;
      }
    }

    private void Update()
    {
      if (_session == null || _session.CurrentState != GameState.Playing)
      {
        return;
      }

      Mouse mouse = Mouse.current;
      if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
      {
        return;
      }

      if (_camera == null)
      {
        _camera = Camera.main;
        if (_camera == null)
        {
          return;
        }
      }

      Vector2 screen = mouse.position.ReadValue();
      Vector3 world = _camera.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -_camera.transform.position.z));

      // OverlapPointAll because the play-area collider also sits under the
      // cursor — we need to prefer a TargetView hit over the background.
      Collider2D[] hits = Physics2D.OverlapPointAll(world, _targetMask);
      foreach (Collider2D hit in hits)
      {
        if (hit != null && hit.TryGetComponent(out TargetView target))
        {
          target.HandleClick(world);
          return;
        }
      }

      if (_misclickCatcher != null)
      {
        _misclickCatcher.HandleMisclick();
      }
    }
  }
}
