using DG.Tweening;
using UnityEngine;

public class BoxTileView : TileView, IAnimateDamage, IAnimateDestroy
{
    private int m_health = 1;

    protected override void OnSetup(int initialHealth)
    {
        m_health = initialHealth > 0 ? initialHealth : 1;
    }

    protected override string GetCategoryByType() => "Box";
    protected override string GetLabelByType() => $"Box{m_health}";

    public void PlayDamage(int currentHealth)
    {
        m_health = currentHealth;
        SetSpriteFromLibrary();

        transform.DOKill();
        transform.DOShakePosition(0.15f, strength: 0.05f, vibrato: 20, randomness: 90f, snapping: false, fadeOut: true);
    }

    public void PlayDestroy()
    {
        transform.DOKill();
        m_SpriteRenderer.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(Vector3.one * 1.15f, 0.06f).SetEase(Ease.OutQuad));
        seq.Append(transform.DOScale(Vector3.zero, 0.08f).SetEase(Ease.InQuad));
        seq.AppendCallback(() =>
        {
            transform.localScale = Vector3.one;
            ReturnToPool();
        });
    }
}
