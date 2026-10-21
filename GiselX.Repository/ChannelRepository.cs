using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public sealed class ChannelRepository(IDbConnection connection, IDbTransaction transaction)
    : IChannelRepository
{
    private const string Columns =
        "Id, Code, Name, NotificationEmails, IsActive, CreatedDate, CreatedBy, ModifiedDate, ModifiedBy";

    public async Task<IReadOnlyList<Channel>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT {Columns}
            FROM Channel
            WHERE @ActiveOnly = 0 OR IsActive = 1
            ORDER BY Code;
            """;
        var values = await connection.QueryAsync<Channel>(new CommandDefinition(sql,
            new { ActiveOnly = activeOnly }, transaction, cancellationToken: cancellationToken));
        return values.ToList();
    }

    public Task<Channel?> GetAsync(int id, CancellationToken cancellationToken) =>
        connection.QuerySingleOrDefaultAsync<Channel>(new CommandDefinition(
            $"SELECT {Columns} FROM Channel WHERE Id = @Id;",
            new { Id = id }, transaction, cancellationToken: cancellationToken));

    public async Task<int> InsertAsync(Channel channel, CancellationToken cancellationToken)
    {
        try
        {
            return await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                INSERT INTO Channel (Code, Name, NotificationEmails, IsActive, CreatedDate, CreatedBy)
                OUTPUT INSERTED.Id
                VALUES (@Code, @Name, @NotificationEmails, @IsActive, @CreatedDate, @CreatedBy);
                """, channel, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ChannelDuplicateException(exception);
        }
    }

    public async Task UpdateAsync(Channel channel, CancellationToken cancellationToken)
    {
        int affected;
        try
        {
            affected = await connection.ExecuteAsync(new CommandDefinition("""
                UPDATE Channel
                SET Code = @Code, Name = @Name, NotificationEmails = @NotificationEmails, IsActive = @IsActive,
                    ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
                WHERE Id = @Id;
                """, channel, transaction, cancellationToken: cancellationToken));
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            throw new ChannelDuplicateException(exception);
        }
        if (affected == 0) throw new InvalidOperationException("The channel was not found.");
    }

    public async Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken)
    {
        var affected = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE Channel
            SET IsActive = @IsActive, ModifiedDate = @ModifiedDate, ModifiedBy = @ModifiedBy
            WHERE Id = @Id;
            """, new { Id = id, IsActive = isActive, ModifiedBy = modifiedBy, ModifiedDate = modifiedDate },
            transaction, cancellationToken: cancellationToken));
        if (affected == 0) throw new InvalidOperationException("The channel was not found.");
    }

    public async Task<ChannelDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        const string sql = """
            DELETE channel
            FROM Channel channel
            WHERE channel.Id = @Id
              AND NOT EXISTS (SELECT 1 FROM Company WHERE ChannelId = channel.Id);

            SELECT CASE
                WHEN @@ROWCOUNT = 1 THEN 1
                WHEN EXISTS (SELECT 1 FROM Channel WHERE Id = @Id)
                    AND EXISTS (SELECT 1 FROM Company WHERE ChannelId = @Id) THEN 2
                ELSE 0
            END;
            """;
        return (ChannelDeleteResult)await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new { Id = id }, transaction, cancellationToken: cancellationToken));
    }
}
