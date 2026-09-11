using GiselX.Domain;
using GiselX.Web.Controllers.Api;

namespace GiselX.Tests;

/// <summary>
/// The Users grid searched nothing: the page sent a "Search" query parameter but the bound DTO exposes
/// "SearchTerm", so the filter never ran. The predicate itself also gated every match on LastName being
/// non-null, which would hide any user without one.
/// </summary>
public sealed class UserSearchTests
{
    // Case-insensitivity is supplied by the database collation (SQL_Latin1_General_CP1_CI_AS), not by the
    // predicate, so these in-memory cases match ordinally and only cover the field coverage.
    [Theory]
    [InlineData("ahmad", "ahmad.fauzi")]
    [InlineData("gandummas", "ahmad.fauzi")]
    [InlineData("Ahm", "ahmad.fauzi")]
    public void Search_matches_on_username_email_or_either_name(string term, string expectedUserName)
    {
        var match = Assert.Single(UsersController.ApplySearch(Users(), term));

        Assert.Equal(expectedUserName, match.UserName);
    }

    [Fact]
    public void Search_finds_a_user_who_has_no_last_name()
    {
        var match = Assert.Single(UsersController.ApplySearch(Users(), "Cher"));

        Assert.Equal("cher", match.UserName);
    }

    [Fact]
    public void Search_finds_a_user_who_has_no_email()
    {
        var match = Assert.Single(UsersController.ApplySearch(Users(), "noemail"));

        Assert.Equal("noemail", match.UserName);
    }

    [Fact]
    public void Search_surrounding_whitespace_is_ignored()
    {
        Assert.Single(UsersController.ApplySearch(Users(), "  fauzi  "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_term_returns_every_user(string? term)
    {
        Assert.Equal(Users().Count(), UsersController.ApplySearch(Users(), term).Count());
    }

    [Fact]
    public void A_term_matching_nobody_returns_nobody()
    {
        Assert.Empty(UsersController.ApplySearch(Users(), "zzzz"));
    }

    private static IQueryable<AppIdentityUser> Users() => new[]
    {
        new AppIdentityUser
        {
            UserName = "ahmad.fauzi", Email = "ahmad.fauzi@gandummas.co.id", FirstName = "Ahmad", LastName = "Fauzi"
        },
        new AppIdentityUser { UserName = "cher", Email = "cher@example.com", FirstName = "Cher", LastName = null },
        new AppIdentityUser { UserName = "noemail", Email = null, FirstName = "No", LastName = "Email" }
    }.AsQueryable();
}
