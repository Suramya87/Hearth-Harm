using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class UIHoverFeedback :
    MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    [Header("Scale")]
    [SerializeField] private float hoverScale = 1.035f;
    [SerializeField] private float pressedScale = 0.97f;

    [Header("Timing")]
    [SerializeField] private float animationSpeed = 12f;

    private Vector3 normalScale;
    private Vector3 targetScale;

    private Coroutine pulseRoutine;

    private void Awake()
    {
        normalScale = transform.localScale;
        targetScale = normalScale;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            targetScale,
            Time.unscaledDeltaTime * animationSpeed);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        targetScale = normalScale * hoverScale;

        if (pulseRoutine != null)
            StopCoroutine(pulseRoutine);

        pulseRoutine = StartCoroutine(SubtlePulse());
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        targetScale = normalScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        targetScale = normalScale * pressedScale;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        targetScale = normalScale * hoverScale;
    }

    private IEnumerator SubtlePulse()
    {
        Vector3 pulseScale = normalScale * (hoverScale + 0.015f);

        float elapsed = 0f;
        const float duration = 0.1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            transform.localScale = Vector3.Lerp(
                transform.localScale,
                pulseScale,
                elapsed / duration);

            yield return null;
        }

        targetScale = normalScale * hoverScale;
        pulseRoutine = null;
    }
}