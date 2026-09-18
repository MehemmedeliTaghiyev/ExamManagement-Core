using System.Security.Claims;
using Exam.Core.Interfaces;
using Exam.Infrastructure;
using Exam.Infrastructure.Filters;
using Exam.Infrastructure.Repositories;
using Exam.Infrastructure.Services;
using Exam.Infrastructure.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerUI;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddControllers(options =>
    {
        options.Filters.Add<EnsureUserAccessFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IExamService, ExamService>();
builder.Services.AddScoped<ISubmissionService, SubmissionService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentTenant>();
builder.Services.AddScoped<IQuestionService, QuestionService>();
builder.Services.AddScoped<IUserAdminService, UserAdminService>();
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddScoped<IAdminNotificationService, AdminNotificationService>();
builder.Services.AddHostedService<TrialReminderHostedService>();
builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();
builder.Services.AddScoped<EnsureUserAccessFilter>();
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 52_428_800;
    options.ValueLengthLimit = 52_428_800;
});
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800;
});

builder.Services.AddDbContext<ExamDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")
    ));

var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["Secret"] ?? "SUPER_SECRET_KEY_THAT_IS_AT_LEAST_32_BYTES_LONG_12345!";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RoleClaimType = ClaimTypes.Role,
        NameClaimType = ClaimTypes.Name,
        ValidIssuer = jwtSettings["Issuer"] ?? "ExamApi",
        ValidAudience = jwtSettings["Audience"] ?? "ExamClient",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
});

builder.Services.AddAuthorization();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, context, cancellationToken) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes.Add("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter your JWT token"
        });

        document.SecurityRequirements.Add(new OpenApiSecurityRequirement
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

        return Task.CompletedTask;
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExamDbContext>();
    try { db.Database.EnsureCreated(); }
    catch { /* host may not allow EnsureCreated; import SQL instead */ }
    void TrySql(string sql)
    {
        try { db.Database.ExecuteSqlRaw(sql); }
        catch { /* column/index may already exist, or table not created yet */ }
    }

    TrySql("IF COL_LENGTH('Users', 'IsDeleted') IS NULL ALTER TABLE Users ADD IsDeleted bit NOT NULL CONSTRAINT DF_Users_IsDeleted DEFAULT(0);");
    TrySql("IF COL_LENGTH('Users', 'DeletedAt') IS NULL ALTER TABLE Users ADD DeletedAt datetime2 NULL;");
    TrySql("IF COL_LENGTH('Users', 'IsAccessEnabled') IS NULL ALTER TABLE Users ADD IsAccessEnabled bit NOT NULL CONSTRAINT DF_Users_IsAccessEnabled DEFAULT(1);");
    TrySql("IF COL_LENGTH('Users', 'TeacherId') IS NULL ALTER TABLE Users ADD TeacherId int NULL;");
    TrySql("IF COL_LENGTH('Users', 'UserName') IS NULL ALTER TABLE Users ADD UserName nvarchar(80) NULL;");
    TrySql("IF COL_LENGTH('Users', 'GroupName') IS NULL ALTER TABLE Users ADD GroupName nvarchar(80) NULL;");
    TrySql("IF COL_LENGTH('Users', 'FirstName') IS NULL ALTER TABLE Users ADD FirstName nvarchar(80) NULL;");
    TrySql("IF COL_LENGTH('Users', 'LastName') IS NULL ALTER TABLE Users ADD LastName nvarchar(80) NULL;");
    TrySql("IF COL_LENGTH('Users', 'Phone') IS NULL ALTER TABLE Users ADD Phone nvarchar(40) NULL;");
    TrySql("IF COL_LENGTH('Users', 'TrialEndsAt') IS NULL ALTER TABLE Users ADD TrialEndsAt datetime2 NULL;");
    TrySql("IF COL_LENGTH('Users', 'TrialMessage') IS NULL ALTER TABLE Users ADD TrialMessage nvarchar(1000) NULL;");
    TrySql("IF COL_LENGTH('Users', 'TrialNotifiedAt') IS NULL ALTER TABLE Users ADD TrialNotifiedAt datetime2 NULL;");
    TrySql("IF COL_LENGTH('Exams', 'TeacherId') IS NULL ALTER TABLE Exams ADD TeacherId int NULL;");
    TrySql("IF COL_LENGTH('Questions', 'InputKind') IS NULL ALTER TABLE Questions ADD InputKind nvarchar(20) NOT NULL CONSTRAINT DF_Questions_InputKind DEFAULT('Choice');");
    TrySql("IF COL_LENGTH('Questions', 'CorrectText') IS NULL ALTER TABLE Questions ADD CorrectText nvarchar(500) NULL;");
    TrySql(@"
IF COL_LENGTH('Users', 'UserName') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_UserName' AND object_id = OBJECT_ID('Users'))
AND NOT EXISTS (
    SELECT UserName FROM Users
    WHERE UserName IS NOT NULL AND UserName <> N''
    GROUP BY UserName HAVING COUNT(*) > 1)
    CREATE UNIQUE INDEX UX_Users_UserName ON Users(UserName) WHERE UserName IS NOT NULL AND UserName <> N'';
");
}

Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "wwwroot"));

app.MapOpenApi();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi/v1.json", "Exam Management API v1");
    options.RoutePrefix = "swagger";
});

app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append("Access-Control-Allow-Origin", "*");
        if (ctx.File.Name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            ctx.Context.Response.Headers.ContentType = "application/pdf";
            ctx.Context.Response.Headers.Append("Content-Disposition", "inline");
        }
    }
});
app.UseCors("AllowReactApp");
app.UseRouting();
app.UseCors("AllowReactApp");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
