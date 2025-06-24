using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DA_Web.Services.Interfaces
{
    public interface IFileService
    {
        Task<string> SaveFileAsync(IFormFile file, string subfolder);
        Task<List<string>> SaveFilesAsync(List<IFormFile> files, string subfolder);
        void DeleteFile(string relativeFilePath);
        Task DeleteFileAsync(string relativeFilePath);
    }
}