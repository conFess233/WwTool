using Microsoft.EntityFrameworkCore.Design;

namespace WwTool.Core.Data;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new AppDbContextFactory(DatabaseOptions.CreateDefault()).CreateDbContext();
}
