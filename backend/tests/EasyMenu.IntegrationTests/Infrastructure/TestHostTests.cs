using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EasyMenu.IntegrationTests.Infrastructure;

/// <summary>Sanity checks of the test host itself (clock, configuration overrides).</summary>
public sealed class TestHostTests(EasyMenuApiFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public void Api_time_provider_is_the_controllable_fake_clock()
    {
        var apiClock = Factory.Services.GetRequiredService<TimeProvider>();
        var before = apiClock.GetUtcNow();

        Factory.Clock.Advance(TimeSpan.FromHours(9));

        apiClock.GetUtcNow().Should().Be(before + TimeSpan.FromHours(9));
    }

    [Fact]
    public void Each_test_starts_with_a_fresh_clock_close_to_real_time()
    {
        Factory.Clock.GetUtcNow().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void Test_settings_override_jwt_and_storage()
    {
        var configuration = Factory.Services.GetRequiredService<IConfiguration>();

        configuration["Jwt:Issuer"].Should().Be(EasyMenuApiFactory.JwtIssuer);
        configuration["Jwt:SigningKey"].Should().Be(EasyMenuApiFactory.JwtSigningKey);
        configuration["Storage:ImagesPath"].Should().Be(Factory.ImagesPath);
        Directory.Exists(Factory.ImagesPath).Should().BeTrue();
    }
}
