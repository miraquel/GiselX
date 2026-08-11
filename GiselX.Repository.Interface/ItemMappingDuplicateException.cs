namespace GiselX.Repository.Interface;

public sealed class ItemMappingDuplicateException : Exception
{
    public ItemMappingDuplicateException()
    {
    }

    public ItemMappingDuplicateException(Exception innerException)
        : base("A mapping with this Distributor Item ID already exists for the company.", innerException)
    {
    }
}
