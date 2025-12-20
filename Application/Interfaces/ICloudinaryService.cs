using Application.DTOs;

namespace Application.Interfaces;

public interface ICloudinaryService
{
    CloudinarySignatureDto GetUploadSignature(string folderName);
    Task<bool> DeleteFileAsync(string publicId);
}