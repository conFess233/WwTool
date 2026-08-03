using Microsoft.EntityFrameworkCore.Design;

namespace WwTool.Common.Context;

public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new AppDbContextFactory(DatabaseOptions.CreateDefault()).CreateDbContext();
}
