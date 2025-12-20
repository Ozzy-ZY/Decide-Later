namespace Application.DTOs;

public record CloudinarySignatureDto(
    string Signature,
    long Timestamp,
    string ApiKey,
    string CloudName,
    string Folder);