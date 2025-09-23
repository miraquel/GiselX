using GiselX.Common.Constants;
using GiselX.Domain;
using GiselX.Service.Dto;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
public class RolesController : ControllerBase
{
    private readonly RoleManager<AppIdentityRole> _roleManager;

    public RolesController(RoleManager<AppIdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet]
    [Authorize(PermissionConstants.Roles.Index)]
    public Task<ActionResult<ServiceResponse<PagedListDto<RoleDto>>>> Get([FromQuery] PagedListRequestDto request)
    {
        var rolesQuery = _roleManager.Roles;

        // Filtering
        if (!string.IsNullOrEmpty(request.SearchTerm))
        {
            rolesQuery = rolesQuery.Where(r =>
                r.Name != null &&
                (r.Name.Contains(request.SearchTerm) ||
                 r.Description.Contains(request.SearchTerm))
            );
        }

        // Sorting
        if (!string.IsNullOrEmpty(request.SortBy) && typeof(AppIdentityRole).GetProperties().Any(p => p.Name.Equals(request.SortBy, StringComparison.OrdinalIgnoreCase)))
        {
            rolesQuery = request.IsSortAscending ? rolesQuery.OrderBy(e => EF.Property<object>(e, request.SortBy)) : rolesQuery.OrderByDescending(e => EF.Property<object>(e, request.SortBy));
        }

        var recordsTotal = _roleManager.Roles.Count();
        var recordsFiltered = rolesQuery.Count();

        var pageSize = request.PageSize > 0 ? request.PageSize : 10;
        var pageNumber = request.PageNumber > 0 ? request.PageNumber : 1;

        var rolesList = rolesQuery
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var roles = rolesList.Select(r => new RoleDto
        {
            Id = r.Id,
            Name = r.Name ?? string.Empty,
            Description = r.Description
        }).ToList();

        var pagedList = new PagedListDto<RoleDto>(roles, pageNumber, pageSize, recordsTotal, recordsFiltered);

        var response = new ServiceResponse<PagedListDto<RoleDto>>
        {
            Data = pagedList,
            Message = "Roles retrieved successfully.",
            StatusCode = 200,
        };

        return Task.FromResult<ActionResult<ServiceResponse<PagedListDto<RoleDto>>>>(StatusCode(response.StatusCode, response));
    }
}