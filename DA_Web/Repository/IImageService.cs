namespace DA_Web.Repository
{
    public interface IImageService
    {
        Task<string> SaveImageAsync(IFormFile imageFile, string subfolder = null, string customFileName = null);
        Task<bool> DeleteImageAsync(string imageUrl);
        bool IsImageValid(IFormFile file);
    }
}
 