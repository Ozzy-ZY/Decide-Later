using Application.DTOs;
using Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Infrastructure.Configurations;
using Microsoft.Extensions.Options;

namespace Application.Services;

public class CloudinaryService(IOptions<CloudinarySettings> cloudinaryConfig) :ICloudinaryService
{
    private readonly Cloudinary _cloudinary = new Cloudinary(new Account(
        cloudinaryConfig.Value.CloudName,
        cloudinaryConfig.Value.ApiKey,
        cloudinaryConfig.Value.ApiSecret));
    private readonly CloudinarySettings _cloudinarySettings = cloudinaryConfig.Value;
    
    public CloudinarySignatureDto GetUploadSignature(string folderName)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        // TODO: Create a cron job to clean Unlinked assets with "temp" tag
        var parameters = new SortedDictionary<string, object>
        {
            { "folder", folderName },
            { "timestamp", timestamp },
            {"tags", "temp"} // update when linked to DB to "saved"
        };
        var signature = _cloudinary.Api.SignParameters(parameters);
        return new CloudinarySignatureDto(
            Signature: signature,
            Timestamp: timestamp,
            ApiKey: _cloudinarySettings.ApiKey,
            CloudName: _cloudinarySettings.CloudName,
            Folder: folderName);
    }

    public async Task<bool> DeleteFileAsync(string publicId)
    {
        var result = await _cloudinary.DestroyAsync(
            new DeletionParams(publicId));
        return result.Result == "ok";
    }
}