using FlickFest.Core;
using UnityEngine;

namespace FlickFest.Presentation
{
    /// <summary>
    /// Visual representation of a single target. Instantiated by
    /// <see cref="SpawnManager"/>; resolves itself on click or expiry and
    /// reports back through the owning <see cref="GameSession"/>.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class TargetView : MonoBehaviour
    {
        [SerializeField] private Color _positiveColor = new(0.2f, 0.8f, 0.3f);
        [SerializeField] private Color _negativeColor = new(0.9f, 0.2f, 0.2f);

        [Tooltip("Seconds for the spawn pop-in animation.")]
        [SerializeField, Min(0f)] private float _spawnAnimDuration = 0.1f;

        [Tooltip("Minimum alpha applied as the target nears its expiry.")]
        [SerializeField, Range(0f, 1f)] private float _minAlpha = 0.3f;

        private TargetData _data;
        private GameSession _session;
        private SpriteRenderer _spriteRenderer;
        private Collider2D _collider;
        private float _expiryTime;
        private float _fullScale;
        private bool _resolved;

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _collider = GetComponent<Collider2D>();
        }

        public void Initialize(TargetData data, GameSession session, Vector3 worldPosition, float worldRadius)
        {
            _data = data;
            _session = session;
            _expiryTime = data.SpawnTime + data.Lifetime;
            _fullScale = worldRadius * 2f;

            transform.position = worldPosition;
            transform.localScale = Vector3.zero;

            var color = data.Type == TargetType.Positive ? _positiveColor : _negativeColor;
            _spriteRenderer.color = color;
        }

        private void Update()
        {
            if (_resolved || _data == null)
            {
                return;
            }

            float now = Time.time;

            if (now >= _expiryTime)
            {
                _resolved = true;
                _session.ReportExpired(_data);
                _session.NotifyTargetResolved();
                Destroy(gameObject);
                return;
            }

            ApplySpawnAnimation(now);
            ApplyLifetimeFade(now);
        }

        /// <summary>
        /// Invoked by <see cref="ClickRouter"/> when a left-click world-space point
        /// is found to be inside this target's collider. Project settings select
        /// the new Input System, which suppresses <c>OnMouseDown</c>, so click
        /// dispatch is routed through the manager rather than this callback.
        /// </summary>
        public void HandleClick(Vector3 worldPoint)
        {
            if (_resolved || _data == null)
            {
                return;
            }

            _resolved = true;
            float offset = ComputeClickOffset(worldPoint);
            _session.ReportHit(_data, offset);
            _session.NotifyTargetResolved();
            Destroy(gameObject);
        }

        private float ComputeClickOffset(Vector3 worldPoint)
        {
            float dist = Vector2.Distance(new Vector2(worldPoint.x, worldPoint.y), transform.position);
            float radius = _collider.bounds.extents.x;
            return radius > 0f ? Mathf.Clamp01(dist / radius) : 0f;
        }

        private void ApplySpawnAnimation(float now)
        {
            if (_spawnAnimDuration <= 0f)
            {
                transform.localScale = Vector3.one * _fullScale;
                return;
            }

            float elapsed = now - _data.SpawnTime;
            float t = Mathf.Clamp01(elapsed / _spawnAnimDuration);
            transform.localScale = Vector3.one * (_fullScale * t);
        }

        private void ApplyLifetimeFade(float now)
        {
            float remaining = _expiryTime - now;
            float lifetimeFraction = Mathf.Clamp01(remaining / _data.Lifetime);
            var color = _spriteRenderer.color;
            color.a = Mathf.Lerp(_minAlpha, 1f, lifetimeFraction);
            _spriteRenderer.color = color;
        }
    }
}
