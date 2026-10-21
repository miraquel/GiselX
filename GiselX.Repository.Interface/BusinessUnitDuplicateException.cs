namespace GiselX.Repository.Interface;

public sealed class BusinessUnitDuplicateException : Exception
{
    public BusinessUnitDuplicateException(Exception innerException) : base(
        "A business unit with this name already exists.", innerException)
    {
    }
}
