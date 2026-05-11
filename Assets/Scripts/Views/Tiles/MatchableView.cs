using DG.Tweening;
using UnityEngine;

public class MatchableTileView : TileView, IAnimateDestroy
{
    protected override string GetCategoryByType() => "Matchable";

    public void PlayDestroy()
    {
        transform.DOKill();
        m_SpriteRenderer.DOKill();

        Sequence seq = DOTween.Sequence();
        seq.Append(transform.DOScale(Vector3.one * 0.4f, 0.08f).SetEase(Ease.InQuad));
        seq.Join(transform.DOMove(transform.position + Vector3.up * 0.1f, 0.08f).SetEase(Ease.OutQuad));
        seq.AppendCallback(() =>
        {
            m_SpriteRenderer.enabled = false;
            transform.localScale = Vector3.one;
            ReturnToPool(); // kendini döndürür
        });
    }

    public void ResetView()
    {
        transform.DOKill();
        m_SpriteRenderer.DOKill();
        transform.localScale = Vector3.one;
        m_SpriteRenderer.enabled = true;
        m_SpriteRenderer.color = Color.white;
    }
}