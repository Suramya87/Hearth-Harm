using UnityEngine;

[RequireComponent(typeof(Collider))]
public class EnemyHoverTarget : MonoBehaviour
{
    private EnemyUnit enemyUnit;

    private void Awake()
    {
        enemyUnit = GetComponent<EnemyUnit>();
    }

    private void OnMouseEnter()
    {
        if (enemyUnit == null || enemyUnit.IsDead)
            return;

        // Hover ONLY controls the tile preview.
        TilemapHighlighter.Instance?.ShowEnemyMoveRange(enemyUnit);
    }

    private void OnMouseExit()
    {
        // Leaving an enemy does NOT affect the enemy UI.
        TilemapHighlighter.Instance?.ClearEnemyPreview();
    }

    private void OnMouseDown()
    {
        if (enemyUnit == null || enemyUnit.IsDead)
            return;

        EnemyHealthUI.Instance?.SelectEnemy(enemyUnit);
    }
}