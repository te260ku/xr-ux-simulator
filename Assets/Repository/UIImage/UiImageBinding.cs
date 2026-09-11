using UnityEngine;
using UnityEngine.UI;
using System;

public sealed class UiImageBinding : MonoBehaviour
{
    [SerializeField]
    private UiImageTarget target;

    [SerializeField]
    private Image image;

    public UiImageTarget Target => target;

    public void SetSprite(Sprite sprite)
    {
        if (sprite == null)
            throw new ArgumentNullException(nameof(sprite));

        image.sprite = sprite;
    }

    public void Clear()
    {
        image.sprite = null;
    }

    private void OnValidate()
    {
        if (image == null)
            image = GetComponent<Image>();
    }
}