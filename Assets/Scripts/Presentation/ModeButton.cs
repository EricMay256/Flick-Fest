using System;
using FlickFest.Core;
using TMPro;
using UnityEngine;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Individual mode selector button. Instantiated by <see cref="MainMenu"/>
  /// from a prefab; the prefab's Button.onClick is wired to <see cref="OnClick"/>.
  /// </summary>
  public sealed class ModeButton : MonoBehaviour
  {
    [SerializeField] private TextMeshProUGUI _label;

    private GameModeDefinition _mode;
    private Action<GameModeDefinition> _onSelected;

    public void Initialize(GameModeDefinition mode, Action<GameModeDefinition> onSelected)
    {
      _mode = mode;
      _onSelected = onSelected;
      if (_label != null && mode != null)
      {
        _label.text = mode.DisplayLabel;
      }
    }

    public void OnClick()
    {
      _onSelected?.Invoke(_mode);
    }
  }
}
