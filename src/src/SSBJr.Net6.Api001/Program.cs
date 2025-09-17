using Microsoft.EntityFrameworkCore;
using SSBJr.Net6.Api001.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure EF Core with SQL Server and enable transient error resiliency
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApiDbContext>(options =>
    options.UseSqlServer(connectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorNumbersToAdd: null);
    }));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Attempt to apply migrations / ensure database created at startup with simple retry
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var context = services.GetRequiredService<ApiDbContext>();
        // Try to apply migrations, retry if transient failures occur
        var attempts = 0;
        var success = false;
        while (!success && attempts < 6)
        {
            try
            {
                attempts++;
                var pending = context.Database.GetPendingMigrations();
                if (pending != null && pending.Any())
                {
                    context.Database.Migrate();
                    logger.LogInformation("Applied migrations.");
                }
                else
                {
                    // No migrations available: ensure database created from model
                    context.Database.EnsureCreated();
                    logger.LogInformation("EnsureCreated executed (no migrations found).");
                }

                success = true;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Database init attempt {Attempt} failed, retrying in 5s...", attempts);
                Thread.Sleep(5000);
            }
        }
        if (!success)
        {
            logger.LogError("Could not initialize database after several attempts.");
        }
    }
    catch (Exception ex)
    {
        var logger2 = services.GetRequiredService<ILogger<Program>>();
        logger2.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();
