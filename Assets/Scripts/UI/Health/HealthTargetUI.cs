using UnityEngine;
using UnityEngine.UI;

public class HealthTargetUI : MonoBehaviour
{
    [SerializeField] private GameObject healthBarRoot;
    [SerializeField] private Slider slider;
    [SerializeField] private float animSpeed = 5f;

    private HealthComponent currentTarget;
    private float targetFill;

    private void Awake()
    {
        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);
    }

    private void Update()
    {
        if (slider == null || currentTarget == null)
            return;

        slider.value = Mathf.MoveTowards(
            slider.value,
            targetFill,
            animSpeed * Time.deltaTime
        );
    }

    public void SetTarget(HealthComponent hc)
    {
        if (currentTarget != null)
            currentTarget.OnHealthChanged -= OnChanged;

        currentTarget = hc;

        if (currentTarget == null)
        {
            if (healthBarRoot != null)
                healthBarRoot.SetActive(false);

            return;
        }

        currentTarget.OnHealthChanged += OnChanged;

        if (healthBarRoot != null)
            healthBarRoot.SetActive(true);

        OnChanged(
            currentTarget.CurrentHealth,
            currentTarget.MaxHealth
        );
    }

    public void ClearTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.OnHealthChanged -= OnChanged;
            currentTarget = null;
        }

        if (healthBarRoot != null)
            healthBarRoot.SetActive(false);
    }

    private void OnChanged(int current, int max)
    {
        targetFill = max > 0
            ? (float)current / max
            : 0f;

        if (slider != null && !Application.isPlaying)
            slider.value = targetFill;
    }

    private void OnDestroy()
    {
        if (currentTarget != null)
            currentTarget.OnHealthChanged -= OnChanged;
    }
}