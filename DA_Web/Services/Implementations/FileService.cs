using DA_Web.Services.Interfaces;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DA_Web.Services.Implementations
{
    public class FileService : IFileService
    {
        private readonly IWebHostEnvironment _webHostEnvironment;

        public FileService(IWebHostEnvironment webHostEnvironment)
        {
            _webHostEnvironment = webHostEnvironment;
        }

        public async Task<string> SaveFileAsync(IFormFile file, string subfolder)
        {
            if (file == null || file.Length == 0) return null;

            var uploadDir = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", subfolder);
            if (!Directory.Exists(uploadDir)) Directory.CreateDirectory(uploadDir);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            return Path.Combine("uploads", subfolder, fileName).Replace("\\", "/");
        }

        public async Task<List<string>> SaveFilesAsync(List<IFormFile> files, string subfolder)
        {
            var savedFilePaths = new List<string>();
            if (files == null || files.Count == 0)
            {
                return savedFilePaths;
            }

            foreach (var file in files)
            {
                var savedPath = await SaveFileAsync(file, subfolder);
                if (!string.IsNullOrEmpty(savedPath))
                {
                    savedFilePaths.Add(savedPath);
                }
            }

            return savedFilePaths;
        }


        public void DeleteFile(string relativeFilePath)
        {
            if (string.IsNullOrEmpty(relativeFilePath)) return;

            var webRootPath = _webHostEnvironment.WebRootPath;
            var fullFilePath = Path.Combine(webRootPath, relativeFilePath.TrimStart('/'));

            if (File.Exists(fullFilePath))
            {
                try
                {
                    File.Delete(fullFilePath);
                }
                catch (Exception ex)
                {
                    // Ghi lại lỗi (log error) ở đây nếu cần
                    Console.WriteLine($"Error deleting file: {ex.Message}");
                }
            }
        }
    }
}