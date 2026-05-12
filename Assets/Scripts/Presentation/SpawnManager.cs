using FlickFest.Core;
using UnityEngine;

namespace FlickFest.Presentation
{
    /// <summary>
    /// Polls <see cref="GameSession.TrySpawnTarget"/> each frame, instantiates
    /// <see cref="TargetView"/> prefabs, and maps normalized positions to world
    /// coordinates using the attached collider's bounds.
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    public sealed class SpawnManager : MonoBehaviour
    {
        [SerializeField] private GameSession _session;
        [SerializeField] private GameObject _targetPrefab;

        [Tooltip("Optional parent for spawned targets. If null, targets spawn at the scene root.")]
        [SerializeField] private Transform _targetParent;

        private BoxCollider2D _bounds;

        private void Awake()
        {
            _bounds = GetComponent<BoxCollider2D>();
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

            Bounds b = _bounds.bounds;
            var worldPos = new Vector3(
                Mathf.Lerp(b.min.x, b.max.x, data.NormalizedPosition.x),
                Mathf.Lerp(b.min.y, b.max.y, data.NormalizedPosition.y),
                0f);

            float worldRadius = data.Radius * b.size.x;

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
    }
}
