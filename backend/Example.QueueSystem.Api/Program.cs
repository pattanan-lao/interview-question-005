using Example.QueueSystem.Application;
using Example.QueueSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("QueueDb")
    ?? throw new InvalidOperationException(
        "Connection string 'QueueDb' is not configured. Copy appsettings.Development.json.example " +
        "to appsettings.Development.json and fill in your local PostgreSQL password.");

builder.Services.AddSingleton<IQueueRepository>(_ => new QueueRepository(connectionString));
builder.Services.AddScoped<IQueueService, QueueService>();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevClient", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

await QueueSchema.EnsureCreatedAsync(connectionString);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("AngularDevClient");
app.UseAuthorization();
app.MapControllers();

app.Run();
