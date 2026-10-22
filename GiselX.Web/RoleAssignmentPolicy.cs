using System.Security.Claims;

namespace GiselX.Web;

/// <summary>
/// Who may hand out which role. Cross-company access depends only on the Administrator role, so only an
/// Administrator may grant or remove it; a user holding just the Users.UserRoles or Users.Create
/// permission could otherwise make anyone - themselves included - an Administrator.
/// </summary>
public static class RoleAssignmentPolicy
{
    public const string AdministratorRole = "Administrator";

    /// <summary>The roles to add and to remove, with the Administrator role left as it was for a non-Administrator caller.</summary>
    public static (List<string> ToAdd, List<string> ToRemove) Plan(IEnumerable<string> current,
        IEnumerable<string> selected, bool callerIsAdministrator)
    {
        var currentRoles = current.ToList();
        var selectedRoles = selected.Distinct(StringComparer.Ordinal).ToList();
        var toAdd = selectedRoles.Except(currentRoles, StringComparer.Ordinal).ToList();
        var toRemove = currentRoles.Except(selectedRoles, StringComparer.Ordinal).ToList();
        if (!callerIsAdministrator)
        {
            toAdd.RemoveAll(IsAdministrator);
            toRemove.RemoveAll(IsAdministrator);
        }

        return (toAdd, toRemove);
    }

    /// <summary>The roles the caller may offer on a role picker.</summary>
    public static List<string> Assignable(IEnumerable<string> roles, bool callerIsAdministrator) =>
        roles.Where(role => callerIsAdministrator || !IsAdministrator(role)).ToList();

    public const string DistributorRole = "Distributor";

    /// <summary>
    /// Whether product ids and names are shown as the distributor's own rather than GMK's: only for the
    /// Distributor role, so internal roles such as "GMK Administrator" keep GMK's identities. The role is
    /// created at startup (Program.cs) so the name the code relies on always exists to be assigned.
    /// </summary>
    public static bool SeesDistributorItemIdentity(this ClaimsPrincipal user) => user.IsInRole(DistributorRole);

    private static bool IsAdministrator(string role) => string.Equals(role, AdministratorRole, StringComparison.Ordinal);
}
