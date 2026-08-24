namespace GiselX.Repository.Interface;

public sealed class PowerBiReportDuplicateNameException : Exception
{
    public PowerBiReportDuplicateNameException()
    {
    }

    public PowerBiReportDuplicateNameException(Exception innerException)
        : base("A Power BI report with this name already exists for the company.", innerException)
    {
    }
}
