using Microsoft.EntityFrameworkCore.Design;
using WwTool.Core.Infrastructure;

namespace WwTool.Core.Data;

/// <summary>
/// 供 <c>dotnet ef</c> 生成迁移时创建上下文，运行时不使用。
/// </summary>
public sealed class AppDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new AppDbContextFactory(DatabaseOptions.FromPaths(new AppPaths())).CreateDbContext();
}
