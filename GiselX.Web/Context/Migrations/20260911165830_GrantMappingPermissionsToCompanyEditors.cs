using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GiselX.Web.Context.Migrations
{
    /// <summary>
    /// The mapping and master-data screens used to be gated on Companies.Edit. They now carry their own
    /// permissions, so every role that could reach them before must receive those permissions or the
    /// screens go dark on an existing database — the startup seeder only grants permissions while the
    /// user table is still empty, which is never true in production.
    ///
    /// The permission values are written out rather than read from PermissionConstants on purpose: a
    /// migration records what was granted on this date, and must not change when that class does.
    /// </summary>
    public partial class GrantMappingPermissionsToCompanyEditors : Migration
    {
        private const string GrantedPermissions = """
            (N'StockTemplateMappings.Index'), (N'StockTemplateMappings.Manage'), (N'StockTemplateMappings.Delete'),
            (N'DistributorTemplateMappings.Index'), (N'DistributorTemplateMappings.Manage'), (N'DistributorTemplateMappings.Delete'),
            (N'ItemMappings.Index'), (N'ItemMappings.Manage'), (N'ItemMappings.Delete'), (N'ItemMappings.Import'),
            (N'BusinessUnits.Index'), (N'BusinessUnits.Manage'), (N'BusinessUnits.Delete'),
            (N'Channels.Index'), (N'Channels.Manage'), (N'Channels.Delete'),
            (N'Salesmen.Index'), (N'Salesmen.Manage'), (N'Salesmen.Delete')
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"""
                WITH Granted(Value) AS (SELECT Value FROM (VALUES {GrantedPermissions}) AS v(Value)),
                     Editors(RoleId) AS (
                        SELECT DISTINCT RoleId FROM identity.AspNetRoleClaims
                        WHERE ClaimType = N'permission' AND ClaimValue = N'Companies.Edit')
                INSERT INTO identity.AspNetRoleClaims (RoleId, ClaimType, ClaimValue)
                SELECT e.RoleId, N'permission', g.Value
                FROM Editors e
                CROSS JOIN Granted g
                WHERE NOT EXISTS (
                    SELECT 1 FROM identity.AspNetRoleClaims existing
                    WHERE existing.RoleId = e.RoleId
                      AND existing.ClaimType = N'permission'
                      AND existing.ClaimValue = g.Value);
                """);

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder) =>
            migrationBuilder.Sql($"""
                WITH Granted(Value) AS (SELECT Value FROM (VALUES {GrantedPermissions}) AS v(Value))
                DELETE claim
                FROM identity.AspNetRoleClaims claim
                JOIN Granted g ON g.Value = claim.ClaimValue
                WHERE claim.ClaimType = N'permission'
                  AND EXISTS (
                    SELECT 1 FROM identity.AspNetRoleClaims editor
                    WHERE editor.RoleId = claim.RoleId
                      AND editor.ClaimType = N'permission'
                      AND editor.ClaimValue = N'Companies.Edit');
                """);
    }
}
