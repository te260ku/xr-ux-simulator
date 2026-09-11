public sealed class UiController
{
    private readonly UiImageRegistry _uiImageRegistry;
    private readonly IImageRepository _imageRepository;

    public UiController(
        UiImageRegistry uiImageRegistry,
        IImageRepository imageRepository)
    {
        _uiImageRegistry = uiImageRegistry;
        _imageRepository = imageRepository;
    }

    public void SetImage(
        UiImageTarget target,
        ImageId imageId)
    {
        var binding = _uiImageRegistry.Get(target);
        var sprite = _imageRepository.Get(imageId);

        binding.SetSprite(sprite);
    }

    public void ClearImage(UiImageTarget target)
    {
        _uiImageRegistry.Get(target).Clear();
    }
}

// [ScenarioCommand]
// public void SetUiImage(
//     UiImageTarget target,
//     ImageId imageId)
// {
//     _uiController.SetImage(target, imageId);
// }