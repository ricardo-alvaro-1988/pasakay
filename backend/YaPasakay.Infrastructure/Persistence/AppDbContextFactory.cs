using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace YaPasakay.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        const string connection =
            "Data Source=.\\SQLEXPRESS01;Initial Catalog=YaPasakay;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Application Name=YaPasakay.Api";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connection)
            .Options;

        return new AppDbContext(options);
    }
}
