using System.Threading.Tasks;

public interface IImageLoader
{
    Task<LoadedImage> LoadAsync(string filePath);
}