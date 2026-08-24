using System.Reflection;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class PowerBiReportsControllerTests
{
    [Fact]
    public void Controller_requires_authenticated_user()
    {
        Assert.Contains(typeof(PowerBiReportsController).GetCustomAttributes(true),
            value => value is AuthorizeAttribute attribute && string.IsNullOrEmpty(attribute.Roles));
    }

    [Fact]
    public void View_does_not_accept_a_company_id()
    {
        var parameters = typeof(PowerBiReportsController).GetMethod(nameof(PowerBiReportsController.View),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!.GetParameters();

        Assert.Collection(parameters,
            parameter => Assert.Equal(typeof(int), parameter.ParameterType),
            parameter => Assert.Equal(typeof(CancellationToken), parameter.ParameterType));
    }

    [Fact]
    public void View_has_a_required_integer_route_that_cannot_invoke_the_service_for_non_numeric_ids()
    {
        var route = Assert.Single(typeof(PowerBiReportsController)
            .GetMethod(nameof(PowerBiReportsController.View),
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)!
            .GetCustomAttributes<HttpGetAttribute>());

        Assert.Equal("/PowerBiReports/View/{id:int}", route.Template);
        Assert.DoesNotContain("{id?}", route.Template, StringComparison.Ordinal);
    }

    [Fact]
    public async Task View_returns_not_found_for_missing_inactive_or_other_company_report()
    {
        var controller = new PowerBiReportsController(new ViewerService(data: null, statusCode: 404));

        Assert.IsType<NotFoundResult>(await controller.View(42, default));
    }

    [Fact]
    public async Task View_renders_company_scoped_viewer_model()
    {
        var dto = new PowerBiReportViewerDto(42, "Sales", "https://app.powerbi.com/view?r=abc");
        var service = new ViewerService(dto, 200);
        var token = new CancellationToken(canceled: true);

        var result = Assert.IsType<ViewResult>(await new PowerBiReportsController(service).View(42, token));

        Assert.Same(dto, result.Model);
        Assert.Equal(42, service.ViewerId);
        Assert.Equal(token, service.ViewerToken);
    }

    [Fact]
    public void Viewer_view_uses_encoded_model_attributes_safe_fallback_and_versioned_assets()
    {
        var view = ReadSource("GiselX.Web", "Views", "PowerBiReports", "View.cshtml");

        Assert.Contains("@model GiselX.Service.Dto.PowerBiReportViewerDto", view, StringComparison.Ordinal);
        Assert.Contains("src=\"@Model.EmbedUrl\"", view, StringComparison.Ordinal);
        Assert.Contains("title=\"@Model.Name\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"power-bi-viewer\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"power-bi-fullscreen-status\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", view, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", view, StringComparison.Ordinal);
        Assert.Contains("~/css/power-bi-reports.css\" asp-append-version=\"true\"", view, StringComparison.Ordinal);
        Assert.Contains("~/js/power-bi-reports.js\" asp-append-version=\"true\"", view, StringComparison.Ordinal);

        var viewerStart = view.IndexOf("id=\"power-bi-viewer\"", StringComparison.Ordinal);
        Assert.True(viewerStart < view.IndexOf("id=\"power-bi-fullscreen\"", StringComparison.Ordinal));
        Assert.True(viewerStart < view.IndexOf("id=\"power-bi-fullscreen-status\"", StringComparison.Ordinal));
        Assert.True(viewerStart < view.IndexOf("id=\"power-bi-container\"", StringComparison.Ordinal));
        Assert.True(viewerStart < view.IndexOf("target=\"_blank\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Viewer_view_exposes_accessible_retry_of_the_validated_url_inside_the_fullscreen_subtree()
    {
        var view = ReadSource("GiselX.Web", "Views", "PowerBiReports", "View.cshtml");

        Assert.Contains("id=\"power-bi-retry\"", view, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"power-bi-frame\"", view, StringComparison.Ordinal);
        Assert.Contains("data-report-url=\"@Model.EmbedUrl\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"power-bi-loading-label\"", view, StringComparison.Ordinal);
        Assert.Contains("id=\"power-bi-load-status\"", view, StringComparison.Ordinal);
        Assert.Contains("role=\"status\" aria-live=\"polite\"", view, StringComparison.Ordinal);
        Assert.DoesNotContain("Html.Raw", view, StringComparison.OrdinalIgnoreCase);

        var viewerStart = view.IndexOf("id=\"power-bi-viewer\"", StringComparison.Ordinal);
        Assert.True(viewerStart < view.IndexOf("id=\"power-bi-retry\"", StringComparison.Ordinal));
    }

    [Fact]
    public void Viewer_styles_fill_the_viewport_in_fullscreen_after_mobile_sizing()
    {
        var styles = ReadSource("GiselX.Web", "wwwroot", "css", "power-bi-reports.css");

        Assert.Contains("#power-bi-viewer:fullscreen", styles, StringComparison.Ordinal);
        Assert.Contains("#power-bi-viewer:fullscreen .power-bi-frame", styles, StringComparison.Ordinal);
        Assert.Contains("background: var(--bs-body-bg)", styles, StringComparison.Ordinal);
        Assert.Contains("width: 100vw", styles, StringComparison.Ordinal);
        Assert.Contains("height: 100vh", styles, StringComparison.Ordinal);
        Assert.Contains("min-height: 100vh", styles, StringComparison.Ordinal);
    }

    [Fact]
    public void Viewer_script_hides_unsupported_fullscreen_and_reports_rejections_accessibly()
    {
        var script = ReadSource("GiselX.Web", "wwwroot", "js", "power-bi-reports.js");

        Assert.Contains("const supportsFullscreen = Boolean(viewer?.requestFullscreen)", script, StringComparison.Ordinal);
        Assert.Contains("fullscreenButton?.classList.add('d-none')", script, StringComparison.Ordinal);
        Assert.Contains("try {", script, StringComparison.Ordinal);
        Assert.Contains("catch", script, StringComparison.Ordinal);
        Assert.Contains("fullscreenStatus.textContent", script, StringComparison.Ordinal);
        Assert.Contains("document.fullscreenElement === viewer", script, StringComparison.Ordinal);
        Assert.Contains("document.exitFullscreen", script, StringComparison.Ordinal);
        Assert.Contains("fullscreenchange", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Viewer_script_moves_focus_into_fullscreen_and_restores_it_on_exit()
    {
        var script = ReadSource("GiselX.Web", "wwwroot", "js", "power-bi-reports.js");

        Assert.Contains("let focusBeforeFullscreen = null", script, StringComparison.Ordinal);
        Assert.Contains("focusBeforeFullscreen = document.activeElement", script, StringComparison.Ordinal);
        Assert.Contains("fullscreenButton?.focus()", script, StringComparison.Ordinal);
        Assert.Contains("focusBeforeFullscreen?.focus()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Viewer_script_retry_resets_loading_accessibility_state_and_reloads_only_the_server_url()
    {
        var script = ReadSource("GiselX.Web", "wwwroot", "js", "power-bi-reports.js");

        Assert.Contains("const retryButton = document.getElementById('power-bi-retry')", script,
            StringComparison.Ordinal);
        Assert.Contains("const reportUrl = frame?.dataset.reportUrl", script, StringComparison.Ordinal);
        Assert.Contains("retryButton?.addEventListener('click'", script, StringComparison.Ordinal);
        Assert.Contains("loading?.classList.remove('d-none')", script, StringComparison.Ordinal);
        Assert.Contains("loadingLabel.textContent = 'Reloading report'", script, StringComparison.Ordinal);
        Assert.Contains("loadStatus.textContent = 'Reloading report.'", script, StringComparison.Ordinal);
        Assert.Contains("frame.src = reportUrl", script, StringComparison.Ordinal);
        Assert.DoesNotContain("innerHTML", script, StringComparison.Ordinal);
    }

    private static string ReadSource(params string[] pathParts)
    {
        var path = Path.Combine([FindSolutionRoot(), .. pathParts]);
        Assert.True(File.Exists(path), $"Expected source file at '{path}'.");
        return File.ReadAllText(path);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate GiselX.sln.");
    }

    private sealed class ViewerService(PowerBiReportViewerDto? data, int statusCode) : IPowerBiReportService
    {
        public int? ViewerId { get; private set; }
        public CancellationToken ViewerToken { get; private set; }

        public Task<ServiceResponse<PowerBiReportViewerDto?>> GetViewerAsync(int id, CancellationToken token)
        {
            ViewerId = id;
            ViewerToken = token;
            return Task.FromResult(new ServiceResponse<PowerBiReportViewerDto?> { Data = data, StatusCode = statusCode });
        }

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportDto>>> ListAdminAsync(int? companyId,
            CancellationToken token) => throw new NotSupportedException();

        public Task<ServiceResponse<PowerBiReportDto?>> GetAdminAsync(int id, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<IReadOnlyList<CompanyOptionDto>>> ListCompanyOptionsAsync(CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<int>> GetNextDisplayOrderAsync(int companyId, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse<int>> SaveAsync(SavePowerBiReportDto dto, string username, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse> SetActiveAsync(int id, bool isActive, string username, CancellationToken token) =>
            throw new NotSupportedException();

        public Task<ServiceResponse> DeleteAsync(int id, CancellationToken token) => throw new NotSupportedException();

        public Task<ServiceResponse<IReadOnlyList<PowerBiReportMenuItemDto>>> ListMenuAsync(CancellationToken token) =>
            throw new NotSupportedException();
    }
}
