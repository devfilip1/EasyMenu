using EasyMenu.Domain.Common;

namespace EasyMenu.Application.Common;

/// <summary>The request conflicts with the current state (e.g. <c>slug_taken</c>); mapped to 409.</summary>
public class ConflictException(string code) : DomainException(code);
