using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiettisPark.Api.Data;
using MiettisPark.Api.Services;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace MiettisPark.Api.Tests;

public sealed class TestClock : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => Now;
}

// Test-only serialization and snapshot rollback, not PostgreSQL transaction semantics.
public sealed class TestCoordinator
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
    private TaskCompletionSource? rendezvous;
    private int arrivals;
    public void ArmPair()
    {
        arrivals = 0;
        rendezvous = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public async Task MeetAsync()
    {
        var current = rendezvous;
        if (current != null)
        {
            if (Interlocked.Increment(ref arrivals) == 2)
            {
                rendezvous = null;
                current.SetResult();
            }

            await current.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}

public sealed class SaveProbe : SaveChangesInterceptor
{
    public int Calls { get; private set; }
    public int? FailAt { get; set; }
    public Exception? Failure { get; set; }
    public List<string> Writes { get; } = [];
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Calls++;
        var database = eventData.Context!;
        database.ChangeTracker.DetectChanges();
        Writes.Add(string.Join(",", database.ChangeTracker.Entries<Visit>()
            .Where(x => x.State is EntityState.Added or EntityState.Modified)
            .Select(x => $"{x.State}:{x.Entity.Status}")));
        if (Calls == FailAt)
        {
            throw Failure ?? new InvalidOperationException("Injected save failure");
        }

        return ValueTask.FromResult(result);
    }
}

public sealed class TestTransactions(ParkingDbContext database, TestCoordinator? coordinator = null) : IParkingWriteTransactionFactory
{
    private readonly TestCoordinator coordinator = coordinator ?? new();
    public List<string> Events { get; } = [];
    public Exception? CommitFailure { get; set; }
    public async Task<IParkingWriteTransaction> BeginAsync(CancellationToken cancellationToken)
    {
        await coordinator.MeetAsync();
        await coordinator.Gate.WaitAsync(cancellationToken);
        Events.Add("begin");
        database.ChangeTracker.Clear();
        return new Transaction(this, database);
    }

    private sealed class Transaction : IParkingWriteTransaction
    {
        private readonly TestTransactions owner;
        private readonly ParkingDbContext database;
        private readonly List<Visit> visits;
        private readonly List<Parking> parkings;
        private readonly List<ParkingLot> lots;
        private readonly List<ParkingGarage> garages;
        private bool committed;
        public Transaction(TestTransactions owner, ParkingDbContext database)
        {
            this.owner = owner;
            this.database = database;
            visits = Snapshot<Visit>();
            parkings = Snapshot<Parking>();
            lots = Snapshot<ParkingLot>();
            garages = Snapshot<ParkingGarage>();
            foreach (var garage in garages)
            {
                garage.LayoutCells = garage.LayoutCells.Select(cell => cell with { }).ToList();
                garage.ParkingFeeConfiguration = garage.ParkingFeeConfiguration.Select(tier => new ParkingFeeTier
                {
                    Amount = tier.Amount,
                    PerUnit = tier.PerUnit,
                    StartDuration = tier.StartDuration,
                    EndDuration = tier.EndDuration
                }).ToList();
            }
        }

        private List<T> Snapshot<T>() where T : class
        {
            return database.Set<T>().AsNoTracking().ToList()
                .Select(row => (T)database.Entry(row).CurrentValues.Clone().ToObject()).ToList();
        }

        public Task<ParkingGarage?> LockGarageAsync(int garageId, CancellationToken cancellationToken)
        {
            owner.Events.Add($"garage:{garageId}");
            return database.ParkingGarages.SingleOrDefaultAsync(x => x.Id == garageId, cancellationToken);
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

            committed = true;
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                if (!committed)
                {
                    database.ChangeTracker.Clear();
                    database.Parkings.RemoveRange(database.Parkings);
                    database.Visits.RemoveRange(database.Visits);
                    database.ParkingLots.RemoveRange(database.ParkingLots);
                    database.ParkingGarages.RemoveRange(database.ParkingGarages);
                    database.SaveChanges();
                    database.ChangeTracker.Clear();
                    database.ParkingGarages.AddRange(garages);
                    database.ParkingLots.AddRange(lots);
                    database.Visits.AddRange(visits);
                    database.Parkings.AddRange(parkings);
                    database.SaveChanges();
                    database.ChangeTracker.Clear();
                    owner.Events.Add("rollback");
                }
            }
            finally
            {
                owner.Events.Add("dispose");
                owner.coordinator.Gate.Release();
            }

            return ValueTask.CompletedTask;
        }
    }
}
public sealed class TestStore : IDisposable
{
    private readonly DbContextOptions<ParkingDbContext> options;
    public SaveProbe Probe { get; } = new();
    public TestCoordinator Coordinator { get; } = new();
    public TestClock Clock { get; } = new();
    public ParkingDbContext Database { get; }
    public TestTransactions Transactions { get; }
    public ParkingService Service { get; }
    public GarageManagementService Management { get; }

    public ParkingOptions Options { get; } = new();
    public TestStore()
    {
        options = new DbContextOptionsBuilder<ParkingDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).AddInterceptors(Probe).Options;
        Database = NewContext();
        Database.Database.EnsureCreated();
        Transactions = new TestTransactions(Database, Coordinator);
        Management = new GarageManagementService(Database, Transactions);
        Service = new ParkingService(Database, Clock, new ParkingFeeCalculator(), Transactions, Microsoft.Extensions.Options.Options.Create(Options));
    }

    public ParkingDbContext NewContext() => new(options);
    public ParkingService NewService(ParkingDbContext database) => new(database, Clock, new ParkingFeeCalculator(), new TestTransactions(database, Coordinator), Microsoft.Extensions.Options.Options.Create(Options));
    public void Dispose() => Database.Dispose();
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string name = Guid.NewGuid().ToString();
    public TestClock Clock { get; } = new();
    public string? DuplicateWindow { get; set; }
    public SaveProbe Probe { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:InitializeOnStartup"] = "false",
            ["ConnectionStrings:Parking"] = "Host=unused;Database=unused",
            ["Parking:DuplicateEntryWindow"] = DuplicateWindow ?? "00:02:00"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ParkingDbContext>();
            services.RemoveAll<DbContextOptions<ParkingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<ParkingDbContext>>();
            services.AddDbContext<ParkingDbContext>(options => options.UseInMemoryDatabase(name).AddInterceptors(Probe));
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IParkingWriteTransactionFactory>();
            services.AddSingleton<TestCoordinator>();
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
