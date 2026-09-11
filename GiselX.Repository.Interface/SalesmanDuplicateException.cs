namespace GiselX.Repository.Interface;

public sealed class SalesmanDuplicateException : Exception
{
    public SalesmanDuplicateException(Exception innerException) : base(
        "A salesman with this code already exists for the company.", innerException)
    {
    }
}
