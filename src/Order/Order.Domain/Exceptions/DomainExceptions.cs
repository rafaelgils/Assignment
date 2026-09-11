namespace Order.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message);

public sealed class DomainValidationException(string message) : DomainException(message);

public sealed class InvalidOrderStateException(string message) : DomainException(message);
