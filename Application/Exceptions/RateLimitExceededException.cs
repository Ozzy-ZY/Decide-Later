namespace Application.Exceptions;

public sealed class RateLimitExceededException(string message) : Exception(message);

