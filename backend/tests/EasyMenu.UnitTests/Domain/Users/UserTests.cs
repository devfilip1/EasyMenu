using EasyMenu.Domain.Common;
using EasyMenu.Domain.Users;
using FluentAssertions;

namespace EasyMenu.UnitTests.Domain.Users;

public sealed class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid EstablishmentId = Guid.CreateVersion7();

    private static User NewKitchen() =>
        User.CreateKitchen(EstablishmentId, "Ana", "ana@bar.com", "hash", Now);

    [Fact]
    public void Create_owner_sets_fields_and_starts_active()
    {
        var user = User.CreateOwner(EstablishmentId, "Zé", "ze@bar.com", "hash", Now);

        user.Id.Should().NotBe(Guid.Empty);
        user.EstablishmentId.Should().Be(EstablishmentId);
        user.Name.Should().Be("Zé");
        user.Role.Should().Be(Role.Owner);
        user.PasswordHash.Should().Be("hash");
        user.IsActive.Should().BeTrue();
        user.SecurityStamp.Should().NotBeNullOrWhiteSpace();
        user.SecurityStamp.Length.Should().BeLessThanOrEqualTo(64);
        user.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public void Owner_name_is_optional()
    {
        User.CreateOwner(EstablishmentId, null, "ze@bar.com", "hash", Now).Name.Should().BeNull();
    }

    [Fact]
    public void Create_kitchen_sets_kitchen_role()
    {
        NewKitchen().Role.Should().Be(Role.Kitchen);
    }

    [Theory]
    [InlineData("Ze@Bar.com", "ZE@BAR.COM")]
    [InlineData("  ze@bar.com  ", "ZE@BAR.COM")]
    [InlineData("ZE@BAR.COM", "ZE@BAR.COM")]
    public void Email_is_normalized_with_trim_and_upper_case(string email, string normalized)
    {
        var user = User.CreateOwner(EstablishmentId, null, email, "hash", Now);

        user.NormalizedEmail.Should().Be(normalized);
        user.Email.Should().Be(email.Trim(), "the e-mail is kept as typed (only trimmed)");
    }

    [Fact]
    public void Each_user_gets_its_own_security_stamp()
    {
        NewKitchen().SecurityStamp.Should().NotBe(NewKitchen().SecurityStamp);
    }

    [Fact]
    public void Deactivate_marks_inactive_and_rotates_the_security_stamp()
    {
        var user = NewKitchen();
        var stamp = user.SecurityStamp;

        user.Deactivate();

        user.IsActive.Should().BeFalse();
        user.SecurityStamp.Should().NotBe(stamp).And.NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Deactivate_is_idempotent()
    {
        var user = NewKitchen();
        user.Deactivate();
        var stampAfterFirst = user.SecurityStamp;

        var act = () => user.Deactivate();

        act.Should().NotThrow();
        user.IsActive.Should().BeFalse();
        user.SecurityStamp.Should().Be(stampAfterFirst, "deactivating an inactive user changes nothing");
    }

    [Fact]
    public void Owner_cannot_be_deactivated()
    {
        var owner = User.CreateOwner(EstablishmentId, null, "ze@bar.com", "hash", Now);
        var stamp = owner.SecurityStamp;

        var act = () => owner.Deactivate();

        act.Should().Throw<DomainException>().Which.Code.Should().Be("cannot_deactivate_owner");
        owner.IsActive.Should().BeTrue();
        owner.SecurityStamp.Should().Be(stamp);
    }
}
