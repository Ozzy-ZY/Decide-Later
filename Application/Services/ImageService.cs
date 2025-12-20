using Application.DTOs.Image;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Models;
using Infrastructure.Repositories.Interfaces;

namespace Application.Services;

public class ImageService(ICloudinaryService cloudinaryService, IImageRepository imageRepository)
    : IImageService
{
    private const string ProfilePictureFolder = "profile-pictures";
    private const string TempTag = "temp";

    public ProfilePictureUploadSignatureResponseDto GetProfilePictureUploadSignature(string userId)
    {
        var uploadRequest = new CloudinaryUploadRequestDto(
            Folder: ProfilePictureFolder,
            Tags: [TempTag, $"user:{userId}"]
        );
        
        var signature = cloudinaryService.GetUploadSignature(uploadRequest);
        
        return new ProfilePictureUploadSignatureResponseDto(
            Signature: signature.Signature,
            Timestamp: signature.Timestamp,
            ApiKey: signature.ApiKey,
            CloudName: signature.CloudName,
            Folder: signature.Folder,
            Tags: signature.Tags,
            PublicId: signature.PublicId);
    }

    public async Task<ImageDto> SaveProfilePictureAsync(
        string userId, 
        SaveProfilePictureRequestDto request, 
        CancellationToken cancellationToken = default)
    {
        var existingImage = await imageRepository.GetProfilePictureByUserIdAsync(userId, cancellationToken);
        if (existingImage != null)
        {
            await imageRepository.SoftDeleteAsync(existingImage.Id, cancellationToken);
            // we delete from cloudinary later XD
            // TODO: Create a cron job for that
        }
        var image = new Image
        {
            Id = Guid.NewGuid(),
            PublicId = request.PublicId,
            Url = request.Url,
            Type = ImageType.ProfilePicture,
            UserId = userId,
            CreatedAtUtc = DateTime.UtcNow,
            IsDeleted = false
        };

        await imageRepository.AddAsync(image, cancellationToken);
        await imageRepository.SaveChangesAsync(cancellationToken);

        return new ImageDto(
            Id: image.Id,
            PublicId: image.PublicId,
            Url: image.Url,
            CreatedAtUtc: image.CreatedAtUtc);
    }

    public async Task<ImageDto> GetProfilePictureAsync(
        string userIdOrUsername,
        bool isId,
        CancellationToken cancellationToken = default)
    {
        Image image;
        if (isId)
        {
            image = await imageRepository.GetProfilePictureByUserIdAsync(userIdOrUsername, cancellationToken)
                        ?? throw new ImageNotFoundException("No profile picture found for this user");
        }
        else
        {
            image = await imageRepository.GetProfilePictureByUsernameAsync(userIdOrUsername, cancellationToken)
                ?? throw new ImageNotFoundException("No profile picture found for this user");
        }
        
        return new ImageDto(
            Id: image.Id,
            PublicId: image.PublicId,
            Url: image.Url,
            CreatedAtUtc: image.CreatedAtUtc);
    }

    public async Task DeleteProfilePictureAsync(
        string userId, 
        CancellationToken cancellationToken = default)
    {
        var image = await imageRepository.GetProfilePictureByUserIdAsync(userId, cancellationToken)
            ?? throw new ImageNotFoundException("No profile picture found to delete");

        await imageRepository.SoftDeleteAsync(image.Id, cancellationToken);
        await imageRepository.SaveChangesAsync(cancellationToken);

        // Delete from Cloudinary later as we know
    }
}

