using Npgsql;
using PertCPMBot.Data;
using PertCPMBot.Service;
using RepoDb;
 

var builder = WebApplication.CreateBuilder(args);

// 1. Inicializar RepoDb con soporte PostgreSQL
GlobalConfiguration
    .Setup()
    .UsePostgreSql();

// 2. Registrar los mapeos de entidades
EntityMappings.Configure();

// 3. Registrar NpgsqlConnection como factory (transient = nueva instancia por uso)
var connString = builder.Configuration.GetConnectionString("DefaultConnection")!;
builder.Services.AddTransient<NpgsqlConnection>(_ => new NpgsqlConnection(connString));


builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();
builder.Services.AddSingleton<GroqService>();
builder.Services.AddSingleton<WhatsAppService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();
app.UseRouting();

app.MapControllers();

app.Run("http://localhost:5000");
