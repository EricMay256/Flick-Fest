using FlickFest.Core;
using UnityEngine;

namespace FlickFest.Presentation
{
    /// <summary>
    /// Receives clicks on empty play-area space. <see cref="ClickRouter"/> calls
    /// <see cref="HandleMisclick"/> after determining no target was hit.
    /// </summary>
    public sealed class MisclickCatcher : MonoBehaviour
    {
        [SerializeField] private GameSession _session;

        public void HandleMisclick()
        {
            if (_session != null)
            {
                _session.ReportMisclick();
            }
        }
    }
}
