using System.Reflection;

namespace GiselX.Common.Constants;

public static class PermissionConstants
{
    public static class ServiceLevels
    {
        public const string Index = "ServiceLevels.Index";
        public const string Upload = "ServiceLevels.Upload";
    }
    
    public static class Companies
    {
        public const string Index = "Companies.Index";
        public const string Create = "Companies.Create";
        public const string Edit = "Companies.Edit";
        public const string Details = "Companies.Details";
    }
    
    public static class Roles
    {
        public const string Index = "Roles.Index";
        public const string Create = "Roles.Create";
        public const string Edit = "Roles.Edit";
        public const string Delete = "Roles.Delete";
        public const string Details = "Roles.Details";
        public const string RolePermissions = "Roles.RolePermissions";
    }
    
    public static class Users
    {
        public const string Index = "Users.Index";
        public const string Create = "Users.Create";
        public const string Edit = "Users.Edit";
        public const string Suspend = "Users.Suspend";
        public const string Details = "Users.Details";
        public const string UserRoles = "Users.UserRoles";
    }
    
    // Get all permissions from PermissionConstants
    public static List<string> GetAllPermissions()
    {
        var permissionType = typeof(PermissionConstants);
        var members = permissionType.GetNestedTypes();
        var fieldInfos = new List<FieldInfo>();
        foreach (var member in members)
        {
            fieldInfos.AddRange(member.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy));
        }

        return fieldInfos
            .Where(f => f is { IsLiteral: true, IsInitOnly: false } && f.FieldType == typeof(string))
            .Select(f => f.GetValue(null)?.ToString() ?? string.Empty)
            .ToList();
    }
    
    public static Dictionary<string, List<string>> GetGroupedPermissions()
    {
        var permissions = new Dictionary<string, List<string>>();
        var permissionType = typeof(PermissionConstants);
        foreach (var nestedType in permissionType.GetNestedTypes(BindingFlags.Public | BindingFlags.Static))
        {
            var groupName = nestedType.Name;
            var fields =
                nestedType.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            var permissionList = fields
                .Where(f => f.FieldType == typeof(string))
                .Select(f => f.GetValue(null) as string ?? string.Empty)
                .ToList();
            permissions[groupName] = permissionList;
        }
        
        return permissions;
    }
}