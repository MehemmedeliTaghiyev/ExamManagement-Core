using Exam.Infrastructure;
using Exam.Infrastructure.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Exam.Tests;

public class ExamApiFactory : WebApplicationFactory<Program>
{
    public string DatabaseName { get; } = "teacher-directory-" + Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            var remove = services.Where(d =>
                    d.ServiceType == typeof(ExamDbContext)
                    || d.ServiceType == typeof(DbContextOptions<ExamDbContext>)
                    || d.ServiceType == typeof(IDbContextOptionsConfiguration<ExamDbContext>)
                    || (d.ServiceType.IsGenericType
                        && d.ServiceType.GetGenericTypeDefinition() == typeof(DbContextOptions<>)))
                .ToList();
            foreach (var descriptor in remove)
            {
                services.Remove(descriptor);
            }

            services.RemoveAll<IHostedService>();
            services.AddDbContext<ExamDbContext>(options => options.UseInMemoryDatabase(DatabaseName));
        });
    }
}
