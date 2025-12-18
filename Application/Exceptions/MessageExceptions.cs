namespace Application.Exceptions;

public class MessageValidationException(string message, string? parameterName = null) : Exception(message)
{
    public string? ParameterName { get; } = parameterName;
}

public class MessageUnauthorizedException(string message) : Exception(message);

