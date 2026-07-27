using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneTransitionOnInteract : MonoBehaviour
{
    [Header("Interaction Source")]
    [Tooltip("The interactable whose completed interaction should trigger this transition.")]
    [SerializeField] private Interactable interactionSource;

    [Tooltip("When enabled, locked, rejected, or otherwise state-neutral interactions do not transition.")]
    [SerializeField] private bool requireStateChange;

    [Header("Destination")]
    [SerializeField] private string targetSceneName;
    [SerializeField] private LoadSceneMode loadMode = LoadSceneMode.Single;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float transitionDelay;
    [SerializeField] private bool useUnscaledTime;

    private bool transitionStarted;

    private void Reset()
    {
        ResolveInteractionSource();
    }

    private void OnEnable()
    {
        ResolveInteractionSource();
        if (interactionSource != null)
        {
            interactionSource.InteractionCompleted += HandleInteractionCompleted;
        }
    }

    private void OnDisable()
    {
        if (interactionSource != null)
        {
            interactionSource.InteractionCompleted -= HandleInteractionCompleted;
        }
    }

    private void OnValidate()
    {
        transitionDelay = Mathf.Max(0f, transitionDelay);
        ResolveInteractionSource();
    }

    private void ResolveInteractionSource()
    {
        if (interactionSource == null)
        {
            interactionSource = GetComponent<Interactable>();
        }
    }

    private void HandleInteractionCompleted(InteractionResult result)
    {
        if (requireStateChange && !result.StateChanged)
        {
            return;
        }

        BeginTransition();
    }

    public void BeginTransition()
    {
        if (transitionStarted)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogError("SceneTransitionOnInteract needs a target scene name.", this);
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(targetSceneName))
        {
            Debug.LogError(
                $"SceneTransitionOnInteract cannot load scene '{targetSceneName}'. Add it to Build Settings and verify the name.",
                this);
            return;
        }

        transitionStarted = true;
        StartCoroutine(LoadTargetScene());
    }

    private IEnumerator LoadTargetScene()
    {
        if (transitionDelay > 0f)
        {
            if (useUnscaledTime)
            {
                yield return new WaitForSecondsRealtime(transitionDelay);
            }
            else
            {
                yield return new WaitForSeconds(transitionDelay);
            }
        }

        SceneTransitionService.LoadScene(targetSceneName, loadMode);
    }
}
