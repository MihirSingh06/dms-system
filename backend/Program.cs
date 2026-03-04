using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.EntityFrameworkCore;
using backend;
using backend.Services;
using OfficeOpenXml;

// EPPlus License (Required for v8+)
ExcelPackage.License.SetNonCommercialPersonal("Mihir Singh");

var builder = WebApplication.CreateBuilder(args);

// ✅ IMPORTANT: Ensure environment variables are loaded
builder.Configuration.AddEnvironmentVariables();

// =========================
// SERVICES
// =========================

builder.Services.AddScoped<AiExtractionService>();
builder.Services.AddScoped<FileService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddHttpClient<OcrService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=dms.db"));

// =========================
// CORS
// =========================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins(
                "https://dms-system-ruby.vercel.app",
                "https://ideal-xylophone-5p6g9x779qjhjgj-5173.app.github.dev",
                "https://ideal-xylophone-5p6g9x779qjhjgj-5078.app.github.dev",
                "http://localhost:5173"
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
        });
});

// =========================
// CONTROLLERS
// =========================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters
            .Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });

builder.Services.AddEndpointsApiExplorer();

// =========================
// SWAGGER + JWT
// =========================
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter: Bearer {your token}"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// =========================
// JWT AUTH
// =========================
var jwtKey = "THIS_IS_A_SUPER_LONG_DEV_SECRET_KEY_1234567890";
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),

        RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
        NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"
    };
});

var app = builder.Build();

// =========================
// MIDDLEWARE
// =========================
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => "API is running");

// =========================
// DB MIGRATION + SEED
// =========================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    SeedData.SeedUsers(db);
}

app.Run();