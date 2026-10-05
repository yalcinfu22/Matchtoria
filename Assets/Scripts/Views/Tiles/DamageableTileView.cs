using DG.Tweening;
using UnityEngine;

public abstract class DamageableTileView : TileView, IAnimateDamage, IAnimateDestroy
{
    private int m_displayedHealth = 1;

    protected override void OnSetup(int initialHealth)
    {
        m_displayedHealth = initialHealth > 0 ? initialHealth : 1;
    }

    protected override string GetCategoryByType() => TileType.ToString();
    protected override string GetLabelByType() => $"{TileType}{m_displayedHealth}";

    public void PlayDamage(int currentHealth)
    {
        m_displayedHealth = currentHealth;
        SetSpriteFromLibrary();

        transform.DOKill();
        transform.DOShakePosition(0.15f, strength: 0.05f, vibrato: 20, randomness: 90f, snapping: false, fadeOut: true);
    }

    public void PlayDestroy()
    {
        transform.DOKill();
        m_SpriteRenderer.DOKill();

        Sequence sequence = DOTween.Sequence();
        sequence.Append(transform.DOScale(Vector3.zero, 0.08f).SetEase(Ease.InQuad));
        sequence.AppendCallback(() =>
        {
            transform.localScale = Vector3.one;
            ReturnToPool();
        });
    }
}
