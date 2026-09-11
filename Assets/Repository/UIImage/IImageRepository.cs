using UnityEngine;

public interface IImageRepository
{
    Sprite Get(ImageId id);

    bool TryGet(
        ImageId id,
        out Sprite sprite);
}