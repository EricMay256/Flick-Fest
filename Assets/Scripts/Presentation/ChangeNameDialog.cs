using System;
using System.Collections;
using TMPro;
using UBear.Leaderboard;
using UnityEngine;
using UnityEngine.UI;

namespace FlickFest.Presentation
{
  /// <summary>
  /// Modal dialog for renaming the authenticated user. Wraps
  /// <see cref="LeaderboardService.Rename"/> with simple submit/cancel
  /// affordances; submit fades the panel out only when the server confirms
  /// the rename, otherwise the error is shown inline with its status code.
  /// </summary>
  public sealed class ChangeNameDialog : MonoBehaviour
  {
    [SerializeField] private LeaderboardService _leaderboardService;

    [Header("Panel")]
    [Tooltip("Root GameObject toggled on/off when the dialog opens or closes. Defaults to this GameObject.")]
    [SerializeField] private GameObject _panelRoot;
    [Tooltip("CanvasGroup faded out on successful rename. Optional — if null, the dialog hides instantly.")]
    [SerializeField] private CanvasGroup _canvasGroup;

    [Header("Controls")]
    [SerializeField] private TMP_InputField _input;
    [SerializeField] private TextMeshProUGUI _errorLabel;
    [SerializeField] private Button _submitButton;
    [SerializeField] private Button _cancelButton;

    [Header("Animation")]
    [SerializeField, Min(0.05f)] private float _fadeSeconds = 0.25f;

    /// <summary>
    /// Fires when the rename succeeds. The argument is the new username
    /// that was accepted by the server.
    /// </summary>
    public event Action<string> OnRenamed;

    private bool _submitting;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
      if (_panelRoot == null)
      {
        _panelRoot = gameObject;
      }
      if (_submitButton != null)
      {
        _submitButton.onClick.AddListener(HandleSubmit);
      }
      if (_cancelButton != null)
      {
        _cancelButton.onClick.AddListener(Close);
      }
      SetPanelActive(false);
    }

    private void OnDestroy()
    {
      if (_submitButton != null)
      {
        _submitButton.onClick.RemoveListener(HandleSubmit);
      }
      if (_cancelButton != null)
      {
        _cancelButton.onClick.RemoveListener(Close);
      }
    }

    /// <summary>
    /// Opens the dialog and pre-fills the input with <paramref name="currentName"/>
    /// (if provided) so the player can edit rather than retype.
    /// </summary>
    public void Open(string currentName = null)
    {
      if (_fadeRoutine != null)
      {
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
      }
      if (_canvasGroup != null)
      {
        _canvasGroup.alpha = 1f;
        _canvasGroup.interactable = true;
        _canvasGroup.blocksRaycasts = true;
      }
      SetPanelActive(true);
      _submitting = false;
      SetButtonsInteractable(true);
      SetError(string.Empty);
      if (_input != null)
      {
        _input.text = currentName ?? string.Empty;
        _input.Select();
        _input.ActivateInputField();
      }
    }

    /// <summary>Closes the dialog without notifying listeners.</summary>
    public void Close()
    {
      if (_fadeRoutine != null)
      {
        StopCoroutine(_fadeRoutine);
        _fadeRoutine = null;
      }
      SetPanelActive(false);
      _submitting = false;
    }

    private void HandleSubmit()
    {
      if (_submitting)
      {
        return;
      }
      if (_leaderboardService == null)
      {
        SetError("Leaderboard service not configured.");
        return;
      }

      string desired = _input != null ? (_input.text ?? string.Empty).Trim() : string.Empty;
      if (string.IsNullOrEmpty(desired))
      {
        SetError("Name cannot be empty.");
        return;
      }

      _submitting = true;
      SetError(string.Empty);
      SetButtonsInteractable(false);
      StartCoroutine(_leaderboardService.Rename(desired, result => HandleRenameResult(result, desired)));
    }

    private void HandleRenameResult(ApiResult<bool> result, string desiredName)
    {
      _submitting = false;
      if (result.Success)
      {
        OnRenamed?.Invoke(desiredName);
        _fadeRoutine = StartCoroutine(FadeOutAndClose());
        return;
      }

      int code = result.StatusCode ?? 0;
      string detail = result.Error ?? "Rename failed.";
      SetError(code > 0 ? $"[{code}] {detail}" : detail);
      SetButtonsInteractable(true);
    }

    private IEnumerator FadeOutAndClose()
    {
      if (_canvasGroup == null || _fadeSeconds <= 0f)
      {
        Close();
        yield break;
      }

      if (_cancelButton != null) _cancelButton.interactable = false;
      if (_submitButton != null) _submitButton.interactable = false;
      _canvasGroup.interactable = false;
      _canvasGroup.blocksRaycasts = false;

      float elapsed = 0f;
      float startAlpha = _canvasGroup.alpha;
      while (elapsed < _fadeSeconds)
      {
        elapsed += Time.unscaledDeltaTime;
        _canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / _fadeSeconds);
        yield return null;
      }
      _canvasGroup.alpha = 0f;
      SetPanelActive(false);
      _fadeRoutine = null;
    }

    private void SetError(string text)
    {
      if (_errorLabel != null)
      {
        _errorLabel.text = text ?? string.Empty;
        _errorLabel.gameObject.SetActive(!string.IsNullOrEmpty(text));
      }
    }

    private void SetButtonsInteractable(bool interactable)
    {
      if (_submitButton != null) _submitButton.interactable = interactable;
      if (_cancelButton != null) _cancelButton.interactable = interactable;
    }

    private void SetPanelActive(bool active)
    {
      if (_panelRoot != null && _panelRoot.activeSelf != active)
      {
        _panelRoot.SetActive(active);
      }
    }
  }
}
