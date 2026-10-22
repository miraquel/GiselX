using System.Security.Claims;
using GiselX.Web;

namespace GiselX.Tests;

/// <summary>
/// Cross-company access hangs on the Administrator role alone, so whoever can grant it controls every
/// company's data. A user holding only the Users.UserRoles permission could grant it - even to themselves.
/// </summary>
public sealed class RoleAssignmentPolicyTests
{
    [Fact]
    public void A_non_administrator_cannot_grant_the_administrator_role()
    {
        var (toAdd, _) = RoleAssignmentPolicy.Plan(current: ["Distributor"], selected: ["Distributor", "Administrator"],
            callerIsAdministrator: false);

        Assert.Empty(toAdd);
    }

    [Fact]
    public void A_non_administrator_cannot_take_the_administrator_role_away()
    {
        var (_, toRemove) = RoleAssignmentPolicy.Plan(current: ["Administrator", "Distributor"], selected: ["Distributor"],
            callerIsAdministrator: false);

        Assert.Empty(toRemove);
    }

    [Fact]
    public void A_non_administrator_can_still_change_the_other_roles()
    {
        var (toAdd, toRemove) = RoleAssignmentPolicy.Plan(current: ["Viewer"], selected: ["Distributor"],
            callerIsAdministrator: false);

        Assert.Equal(["Distributor"], toAdd);
        Assert.Equal(["Viewer"], toRemove);
    }

    [Fact]
    public void An_administrator_can_grant_and_remove_the_administrator_role()
    {
        var (toAdd, toRemove) = RoleAssignmentPolicy.Plan(current: ["Distributor"], selected: ["Administrator"],
            callerIsAdministrator: true);

        Assert.Equal(["Administrator"], toAdd);
        Assert.Equal(["Distributor"], toRemove);
    }

    [Fact]
    public void Roles_offered_to_a_non_administrator_leave_out_the_administrator_role()
    {
        Assert.Equal(["Distributor"], RoleAssignmentPolicy.Assignable(["Administrator", "Distributor"], callerIsAdministrator: false));
        Assert.Equal(["Administrator", "Distributor"], RoleAssignmentPolicy.Assignable(["Administrator", "Distributor"], callerIsAdministrator: true));
    }

    [Theory]
    [InlineData("Administrator", false)]
    [InlineData("GMK Administrator", false)]
    [InlineData("Distributor", true)]
    public void Distributor_item_identity_is_shown_only_to_the_distributor_role(string role, bool expected)
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, role)], "test"));

        Assert.Equal(expected, user.SeesDistributorItemIdentity());
    }
}
