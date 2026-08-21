namespace GiselX.Tests;

public sealed class PowerBiReportRepositoryContractTests
{
    [Fact]
    public void Normal_viewer_query_requires_id_company_and_active_state()
    {
        var source = ReadRepository();

        Assert.Contains("Id = @Id", source, StringComparison.Ordinal);
        Assert.Contains("CompanyId = @CompanyId", source, StringComparison.Ordinal);
        Assert.Contains("IsActive = 1", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Menu_query_orders_by_display_order_then_name()
    {
        var source = ReadRepository();

        Assert.Contains("ORDER BY DisplayOrder, Name", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Admin_list_filters_by_optional_company_parameter()
    {
        var source = ReadRepository();

        Assert.Contains("(@CompanyId IS NULL OR CompanyId = @CompanyId)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_name_query_excludes_the_current_report_with_a_parameter()
    {
        var source = ReadRepository();

        Assert.Contains("Name = @Name", source, StringComparison.Ordinal);
        Assert.Contains("Id <> @ExcludeId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Mutations_use_parameters_for_report_values()
    {
        var source = ReadRepository();

        Assert.Contains("@EmbedUrl", source, StringComparison.Ordinal);
        Assert.Contains("@DisplayOrder", source, StringComparison.Ordinal);
        Assert.Contains("@ModifiedBy", source, StringComparison.Ordinal);
        Assert.Contains("@ModifiedDate", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Delete_targets_the_exact_report_id()
    {
        var source = ReadRepository();

        Assert.Contains("DELETE FROM PowerBiReport WHERE Id = @Id;", source, StringComparison.Ordinal);
    }

    private static string ReadRepository() => File.ReadAllText(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "GiselX.Repository", "PowerBiReportRepository.cs"));
}
