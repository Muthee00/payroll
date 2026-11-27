namespace PayMaster.Domain.Common;

/// <summary>Raised when a business rule or workflow invariant is violated.</summary>
public class DomainException(string message) : Exception(message);
