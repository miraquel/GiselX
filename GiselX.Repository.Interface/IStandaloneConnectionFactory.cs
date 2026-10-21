using System.Data.Common;

namespace GiselX.Repository.Interface;

/// <summary>
/// Creates a connection that is deliberately outside the scoped ambient transaction, for work that
/// must not enlist in it — a view component rendered on every page, or a background job whose scope
/// outlives the query because it then talks to an SMTP server.
/// </summary>
public interface IStandaloneConnectionFactory
{
    DbConnection CreateConnection();
}
