using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using GiselX.Service.Dto.Common;
using GiselX.Common.Extensions;
using GiselX.Service.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly UserManager<AppIdentityUser> _userManager;

    public UsersController(UserManager<AppIdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpGet]
    [Authorize(PermissionConstants.Users.Index)]
    public async Task<ActionResult<ServiceResponse<UserDto>>> Get([FromQuery] PagedListRequestDto request)
    {
        var usersQuery = _userManager.Users;

        // Filtering
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            usersQuery = usersQuery.Where(u =>
                u.UserName != null && u.Email != null &&
                u.LastName != null &&
                (u.UserName.Contains(request.SearchTerm) ||
                 u.Email.Contains(request.SearchTerm) ||
                 u.FirstName.Contains(request.SearchTerm) ||
                 u.LastName.Contains(request.SearchTerm))
            );
        }
        
        // check if the property exists on AppIdentityUser
        if (!string.IsNullOrEmpty(request.SortBy) && typeof(AppIdentityUser).GetProperties().Any(p => p.Name.Equals(request.SortBy, StringComparison.OrdinalIgnoreCase)))
        {
            usersQuery = request.IsSortAscending ? usersQuery.OrderBy(e => EF.Property<object>(e, request.SortBy)) : usersQuery.OrderByDescending(e => EF.Property<object>(e, request.SortBy));
        }

        var recordsTotal = _userManager.Users.Count();
        var recordsFiltered = usersQuery.Count();

        // DataTables uses start/length, our DTO uses PageNumber/PageSize
        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;

        var usersList = usersQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var users = new List<UserDto>();
        foreach (var u in usersList)
        {
            var roles = await _userManager.GetRolesAsync(u);
            users.Add(new UserDto
            {
                Id = u.Id,
                UserName = u.UserName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                FirstName = u.FirstName,
                LastName = u.LastName ?? string.Empty,
                // Active is true only if LockoutEnd is null or in the past AND LockoutEnabled is false or not set
                Active = !(u.LockoutEnd != null && u.LockoutEnd > DateTimeOffset.UtcNow && u.LockoutEnabled),
                Roles = string.Join(", ", roles)
            });
        }

        var pagedList = new PagedListDto<UserDto>(users, pageNumber, pageSize, recordsTotal, recordsFiltered);

        var response = new ServiceResponse<PagedListDto<UserDto>>
        {
            Data = pagedList,
            Message = "Users retrieved successfully.",
            StatusCode = 200,
        };
        
        return StatusCode(response.StatusCode, response);
    }
}