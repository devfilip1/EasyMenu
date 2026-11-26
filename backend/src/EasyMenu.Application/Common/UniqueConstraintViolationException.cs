namespace EasyMenu.Application.Common;

/// <summary>A database unique constraint (PostgreSQL 23505) was violated; <see cref="ConstraintName"/> identifies it.</summary>
public sealed class UniqueConstraintViolationException(string constraintName, Exception? innerException = null)
    : Exception($"Unique constraint '{constraintName}' was violated.", innerException)
{
    public string ConstraintName { get; } = constraintName;
}
