namespace VK.Core.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message) { }
}

public sealed class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"{entityName} với id '{key}' không tồn tại.") { }
}

public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message) : base(message) { }
}

public sealed class ForbiddenOperationException : DomainException
{
    public ForbiddenOperationException(string message) : base(message) { }
}