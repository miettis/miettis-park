using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;

namespace MiettisPark.Api.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

// This seam records ordering; it does not simulate PostgreSQL locks, transactions or constraints.
public sealed class TestTransactions(ParkingDbContext database) : IParkingWriteTransactionFactory
{
    public List<string> Events { get; } = [];
    public Exception? CommitFailure { get; set; }
    public Task<IParkingWriteTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        Events.Add("begin");
        return Task.FromResult<IParkingWriteTransaction>(new Transaction(this, database));
    }

    private sealed class Transaction(TestTransactions owner, ParkingDbContext database) : IParkingWriteTransaction
    {
        public Task<ParkingGarage?> LockGarageAsync(int garageId, CancellationToken cancellationToken)
        {
            owner.Events.Add($"garage:{garageId}");
            return database.ParkingGarages.AsNoTracking().SingleOrDefaultAsync(x => x.Id == garageId, cancellationToken);
        }

        public Task<ParkingLot?> LockLotAsync(int garageId, int lotId, CancellationToken cancellationToken)
        {
            owner.Events.Add($"lot:{lotId}");
            return database.ParkingLots.SingleOrDefaultAsync(x => x.Id == lotId && x.ParkingGarageId == garageId, cancellationToken);
        }

        public Task CommitAsync(CancellationToken cancellationToken)
        {
            owner.Events.Add("commit");
            if (owner.CommitFailure != null)
            {
                throw owner.CommitFailure;
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            owner.Events.Add("dispose");
            return ValueTask.CompletedTask;
        }
    }
}

public sealed class TestStore : IDisposable
{
    private readonly DbContextOptions<ParkingDbContext> options = new DbContextOptionsBuilder<ParkingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    public TestClock Clock { get; } = new();
    public ParkingDbContext Database { get; }
    public TestTransactions Transactions { get; }
    public ParkingService Service { get; }

    public TestStore()
    {
        Database = NewContext();
        Database.Database.EnsureCreated();
        Transactions = new TestTransactions(Database);
        Service = new ParkingService(Database, Clock, new ParkingFeeCalculator(), Transactions);
    }

    public ParkingDbContext NewContext() => new(options);
    public ParkingService NewService(ParkingDbContext database) => new(database, Clock, new ParkingFeeCalculator(), new TestTransactions(database));
    public void Dispose() => Database.Dispose();
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string name = Guid.NewGuid().ToString();
    public TestClock Clock { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:InitializeOnStartup"] = "false",
            ["ConnectionStrings:Parking"] = "Host=unused;Database=unused"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ParkingDbContext>();
            services.RemoveAll<DbContextOptions<ParkingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ParkingDbContext>>();
            services.AddDbContext<ParkingDbContext>(options => options.UseInMemoryDatabase(name));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IParkingWriteTransactionFactory>();
            services.AddScoped<IParkingWriteTransactionFactory, TestTransactions>();
        });
    }

    public HttpClient CreateSeededClient()
    {
        var client = CreateClient();
        using var scope = Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ParkingDbContext>().Database.EnsureCreated();
        return client;
    }
}
