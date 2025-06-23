namespace DA_Web.Repository
{
    public class ImageService : IImageService
    {
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _allowedExtensions = { ".jpg", ".jpeg", ".png", ".gif" };
        private const int MaxFileSize = 5 * 1024 * 1024; // 5MB

        public ImageService(IWebHostEnvironment environment)
        {
            _environment = environment;
        }

        public async Task<string> SaveImageAsync(IFormFile imageFile, string subfolder = null, string customFileName = null)
        {
            if (imageFile == null || imageFile.Length == 0)
                throw new ArgumentException("Không có file được chọn");

            if (!IsImageValid(imageFile))
                throw new ArgumentException("File không hợp lệ");

            // Sử dụng tên file tùy chỉnh nếu được cung cấp, nếu không thì tạo GUID
            string fileName = customFileName ?? Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);

            // Tạo đường dẫn tới thư mục lưu trữ
            string uploadsFolder;

            if (string.IsNullOrEmpty(subfolder))
            {
                uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            }
            else
            {
                // Sử dụng subfolder được chỉ định (ví dụ: "locations/5")
                uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", subfolder);
            }

            // Tạo thư mục nếu chưa tồn tại
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            // Đường dẫn đầy đủ tới file
            var filePath = Path.Combine(uploadsFolder, fileName);

            // Lưu file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await imageFile.CopyToAsync(stream);
            }

            // Trả về đường dẫn tương đối để lưu vào database
            return  $"/uploads/{subfolder}/{fileName}";
        }

        public async Task<bool> DeleteImageAsync(string imageUrl)
        {
            if (string.IsNullOrEmpty(imageUrl))
                return false;

            try
            {
                var filePath = Path.Combine(_environment.WebRootPath,
                    imageUrl.TrimStart('/'));

                if (File.Exists(filePath))
                {
                    await Task.Run(() => File.Delete(filePath));
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public bool IsImageValid(IFormFile file)
        {
            if (file == null || file.Length == 0)
                return false;

            if (file.Length > MaxFileSize)
                return false;

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !_allowedExtensions.Contains(extension))
                return false;

            return true;
        }
    }
}
