using System.Data.Common;
using IotGateway.Common.Exceptions;
using IotGateway.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IotGateway.WebAPI.Services;

internal static class DeviceTransactions
{
    public static async Task<T> RunAsync<T>(AppDbContext db, Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
                var result = await operation();
                await transaction.CommitAsync(cancellationToken);
                return result;
            }
            catch (Exception exception) when (IsBusy(exception))
            {
                db.ChangeTracker.Clear();
                if (attempt >= 4)
                    throw new AppException("设备数据正被并发修改，请稍后重试。", 409);
                await Task.Delay(50 * (attempt + 1), cancellationToken);
            }
            catch (Exception exception) when (IsConstraint(exception))
            {
                db.ChangeTracker.Clear();
                throw new AppException("设备编号重复，或资源仍被其他记录引用。", 409);
            }
        }
    }

    public static async Task LockAsync(AppDbContext db, long id, CancellationToken cancellationToken)
    {
        // First SQL statement in the transaction: a database write lock serializes all telemetry
        // for this device across processes, before reading the persisted per-rule alarm cooldown.
        var affected = await db.Devices.Where(d => d.Id == id)
            .ExecuteUpdateAsync(update => update.SetProperty(d => d.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (affected == 0)
            throw new NotFoundException("设备", id);
    }

    internal static bool IsConstraint(Exception exception) => Codes(exception, [19], [1062, 1451, 1452]);
    private static bool IsBusy(Exception exception) => Codes(exception, [5, 6], [1205, 1213]);

    private static bool Codes(Exception exception, int[] sqlite, int[] mysql)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is not DbException)
                continue;
            var type = current.GetType();
            if (type.FullName == "Microsoft.Data.Sqlite.SqliteException" &&
                type.GetProperty("SqliteErrorCode")?.GetValue(current) is int sqliteCode && sqlite.Contains(sqliteCode))
                return true;
            if (type.FullName == "MySqlConnector.MySqlException" &&
                type.GetProperty("Number")?.GetValue(current) is int mysqlCode && mysql.Contains(mysqlCode))
                return true;
        }
        return false;
    }
}
