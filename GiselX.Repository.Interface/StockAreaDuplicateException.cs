namespace GiselX.Repository.Interface;

public sealed class StockAreaDuplicateException : Exception
{
    public StockAreaDuplicateException(Exception innerException) : base(
        "An area with this name already exists for this company.", innerException)
    {
    }
}
