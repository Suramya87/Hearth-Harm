using UnityEngine;

public class EnemyHealthUI : MonoBehaviour
{
    public static EnemyHealthUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject enemyUIRoot;
    [SerializeField] private EnemyHealthContainerUI healthUI;
    [SerializeField] private EnemyPortraitUI portraitUI;

    private EnemyUnit currentEnemy;

    public EnemyUnit CurrentEnemy => currentEnemy;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (enemyUIRoot != null)
            enemyUIRoot.SetActive(false);
    }

    public void SelectEnemy(EnemyUnit enemy)
    {
        if (enemy == null || enemy.IsDead)
            return;

        if (currentEnemy != null)
        {
            currentEnemy.OnEnemyDied -= HandleEnemyDied;
            currentEnemy.SetSelected(false);
        }

        currentEnemy = enemy;

        currentEnemy.SetSelected(true);
        currentEnemy.OnEnemyDied += HandleEnemyDied;

        enemyUIRoot.SetActive(true);

        portraitUI?.SetEnemy(enemy);
        healthUI?.SetTarget(enemy.Health);
    }

    private void HandleEnemyDied(EnemyUnit enemy)
    {
        if (enemy == currentEnemy)
            ClearTarget();
    }

    public void ClearTarget()
    {
        if (currentEnemy != null)
        {
            currentEnemy.OnEnemyDied -= HandleEnemyDied;
            currentEnemy.SetSelected(false);
            currentEnemy = null;
        }

        healthUI?.ClearTarget();
        portraitUI?.ClearPortrait();

        if (enemyUIRoot != null)
            enemyUIRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (currentEnemy != null)
            currentEnemy.OnEnemyDied -= HandleEnemyDied;

        if (Instance == this)
            Instance = null;
    }
}