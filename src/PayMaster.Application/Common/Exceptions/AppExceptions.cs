namespace PayMaster.Application.Common.Exceptions;

public class NotFoundException(string entity, object key)
    : Exception($"{entity} '{key}' was not found.");

public class ForbiddenAccessException(string message = "You do not have permission to perform this action.")
    : Exception(message);

/// <summary>Request is well-formed but conflicts with current state (duplicates, stale edits).</summary>
public class ConflictException(string message) : Exception(message);
