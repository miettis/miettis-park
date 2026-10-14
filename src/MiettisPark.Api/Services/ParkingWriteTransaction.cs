using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiettisPark.Api.Data;

namespace MiettisPark.Api.Services;

public interface IParkingWriteTransactionFactory
{
    Task<IParkingWriteTransaction> BeginAsync(CancellationToken cancellationToken);
}

public interface IParkingWriteTransaction : IAsyncDisposable
{
    Task<ParkingGarage?> LockGarageAsync(int garageId, CancellationToken cancellationToken);
    Task<ParkingLot?> LockLotAsync(int garageId, int lotId, CancellationToken cancellationToken);
    Task CommitAsync(CancellationToken cancellationToken);
}

public sealed class ParkingWriteTransactionFactory(ParkingDbContext database) : IParkingWriteTransactionFactory
{
    public async Task<IParkingWriteTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        var transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        return new ParkingWriteTransaction(database, transaction);
    }

    private sealed class ParkingWriteTransaction(ParkingDbContext database, IDbContextTransaction transaction) : IParkingWriteTransaction
    {
        public Task<ParkingGarage?> LockGarageAsync(int garageId, CancellationToken cancellationToken)
        {
            return database.ParkingGarages.FromSqlInterpolated($"SELECT * FROM parking_garages WHERE id = {garageId} FOR UPDATE")
                .AsNoTracking().SingleOrDefaultAsync(cancellationToken);
        }

        public Task<ParkingLot?> LockLotAsync(int garageId, int lotId, CancellationToken cancellationToken)
        {
            return database.ParkingLots.FromSqlInterpolated($"SELECT * FROM parking_lots WHERE parking_garage_id = {garageId} AND id = {lotId} FOR UPDATE")
                .SingleOrDefaultAsync(cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken) => transaction.CommitAsync(cancellationToken);
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }
}
