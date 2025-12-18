namespace Application.Exceptions;

public class ChatNotFoundException(string message) : Exception(message);

public class ChatOperationException(string message) : Exception(message);

public class ChatUnauthorizedException(string message) : Exception(message);

