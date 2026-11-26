namespace EasyMenu.Application.Common;

/// <summary>A database unique constraint (PostgreSQL 23505) was violated; <see cref="ConstraintName"/> identifies it.</summary>
/// <remarks>Stub created by the test author (T110) so the tests compile; implementation belongs to T111.</remarks>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException(string constraintName, Exception? innerException = null) =>
        throw new NotImplementedException();

    public string ConstraintName => throw new NotImplementedException();
}
