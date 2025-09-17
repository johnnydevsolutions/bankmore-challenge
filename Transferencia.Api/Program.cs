using System.Text;
using ContaCorrente.Infrastructure.Database;
using ContaCorrente.Infrastructure.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("Default") ?? "Data Source=./data/conta.db";
Directory.CreateDirectory("./data");
DbInitializer.EnsureDatabase(cs);

builder.Services.AddSingleton(new ContaRepository(cs));
builder.Services.AddSingleton(new TransferenciaRepository(cs));
builder.Services.AddSingleton(new IdempotenciaRepository(cs));
builder.Services.AddHttpClient("conta", client =>
{
    var baseUrl = builder.Configuration["ContaApi:BaseUrl"] ?? Environment.GetEnvironmentVariable("CONTA_API_URL") ?? "http://localhost:8081";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-secret-key-change";
var keyBytes = Encoding.UTF8.GetBytes(jwtKey);
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
        ClockSkew = TimeSpan.Zero
    };
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
