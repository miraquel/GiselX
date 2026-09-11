namespace GiselX.Repository.Interface;

public sealed class ChannelDuplicateException : Exception
{
    public ChannelDuplicateException(Exception innerException) : base(
        "A channel with this code already exists for the company.", innerException)
    {
    }
}
