using PayBridge.SDK;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

IConfiguration Configuration = builder.Configuration;

// Add services to the container.
var logger = new LoggerConfiguration().WriteTo.Console()
  .ReadFrom.Configuration(builder.Configuration)
  .Enrich.FromLogContext()
  .CreateLogger();
builder.Logging.ClearProviders();
builder.Logging.AddSerilog(logger);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
//builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();

// Add DB repository based on the configured provider
builder.Services.AddDBRepository(builder.Configuration, "SqlServer");
builder.Services.AddControllers();
builder.Services.AddSwaggerGen();

// Bind gateway selection and credentials from the configuration stack.
// Keep credentials in user-secrets locally or a managed secret store in production.
builder.Services.AddPayBridge(builder.Configuration);
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    //app.MapOpenApi();
}

app.UseHttpsRedirection();
app.MapControllers();
app.Run();
