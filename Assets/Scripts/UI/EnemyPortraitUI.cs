using UnityEngine;
using UnityEngine.UI;

public class EnemyPortraitUI : MonoBehaviour
{
    [SerializeField] private Image portraitImage;

    private void Awake()
    {
        if (portraitImage == null)
            portraitImage = GetComponent<Image>();

        ClearPortrait();
    }

    public void SetEnemy(EnemyUnit enemy)
    {
        if (portraitImage == null || enemy == null)
        {
            ClearPortrait();
            return;
        }

        Sprite portrait = null;

        // Bosses use their BossUnit portrait.
        BossUnit boss = enemy.GetComponent<BossUnit>();

        if (boss != null)
            portrait = boss.Portrait;
        else
            portrait = enemy.Portrait;

        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;

        Debug.Log(
            $"[EnemyPortraitUI] Selected {enemy.name}, portrait = " +
            $"{(portrait != null ? portrait.name : "NULL")}"
        );
    }

    public void ClearPortrait()
    {
        if (portraitImage == null)
            return;

        portraitImage.sprite = null;
        portraitImage.enabled = false;
    }
}