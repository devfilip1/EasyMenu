using EasyMenu.Domain.Common;

namespace EasyMenu.Application.Common;

/// <summary>Resource not found or owned by another establishment (mapped to 404 with <see cref="DomainException.Code"/>).</summary>
public class NotFoundException(string code = ErrorCodes.NotFound) : DomainException(code);
