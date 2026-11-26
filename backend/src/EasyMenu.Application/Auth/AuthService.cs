using EasyMenu.Application.Abstractions;
using EasyMenu.Application.Common;
using EasyMenu.Domain.Common;
using EasyMenu.Domain.Establishments;
using EasyMenu.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace EasyMenu.Application.Auth;

/// <summary>Owner registration, login and current user (FR-001..FR-004).</summary>
public sealed class AuthService(
    IAppDbContext db, IPasswordHasher passwordHasher, ITokenService tokenService, TimeProvider timeProvider)
{
    // Hash verified when the e-mail does not exist, so the response time does not reveal registered e-mails.
    private static string? _dummyHash;

    public async Task<AuthResponse> RegisterOwnerAsync(RegisterOwnerRequest request, CancellationToken cancellationToken)
    {
        var slug = Slug.Create(request.Slug);
        var normalizedEmail = User.NormalizeEmail(request.Email);

        if (await db.Users.AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken))
        {
            throw new ConflictException(ErrorCodes.EmailTaken);
        }

        if (await db.Establishments.AnyAsync(e => e.Slug == slug.Value, cancellationToken))
        {
            throw new ConflictException(ErrorCodes.SlugTaken);
        }

        var now = timeProvider.GetUtcNow();
        var establishment = Establishment.Create(request.EstablishmentName, slug, now);
        var owner = User.CreateOwner(establishment.Id, request.Name, request.Email, passwordHasher.Hash(request.Password), now);

        db.Establishments.Add(establishment);
        db.Users.Add(owner);
        await db.SaveChangesAsync(cancellationToken);

        return CreateAuthResponse(owner, establishment);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = User.NormalizeEmail(request.Email);
        var user = await db.Users.AsNoTracking()
            .SingleOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        // Always verify a hash (a dummy one for unknown e-mails) and give the same error for every failure.
        var passwordMatches = passwordHasher.Verify(user?.PasswordHash ?? DummyHash(), request.Password);
        if (user is null || !user.IsActive || !passwordMatches)
        {
            throw new DomainException(ErrorCodes.InvalidCredentials);
        }

        var establishment = await db.Establishments.AsNoTracking()
            .SingleAsync(e => e.Id == user.EstablishmentId, cancellationToken);

        return CreateAuthResponse(user, establishment);
    }

    public async Task<CurrentUserResponse> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var result = await (
                from user in db.Users.AsNoTracking()
                join establishment in db.Establishments.AsNoTracking() on user.EstablishmentId equals establishment.Id
                where user.Id == userId
                select new { user, establishment })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException();

        return ToCurrentUser(result.user, result.establishment);
    }

    private AuthResponse CreateAuthResponse(User user, Establishment establishment)
    {
        var token = tokenService.Issue(user);
        return new AuthResponse
        {
            AccessToken = token.Token,
            ExpiresAt = token.ExpiresAt,
            User = ToCurrentUser(user, establishment),
        };
    }

    private static CurrentUserResponse ToCurrentUser(User user, Establishment establishment) => new()
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Role = user.Role,
        Establishment = new EstablishmentResponse
        {
            Id = establishment.Id,
            Name = establishment.Name,
            Slug = establishment.Slug,
        },
    };

    private string DummyHash() => _dummyHash ??= passwordHasher.Hash(Guid.NewGuid().ToString("N"));
}
