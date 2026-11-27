using CodeSmellAuditor.Core;
using Xunit;

namespace CodeSmellAuditor.Core.Tests;

public class AppConfigurationTests
{
    [Fact]
    public void Resolve_WithoutInput_UsesDefaults()
    {
        AppConfiguration config = Resolve();

        Assert.Equal(AuditConfiguration.DefaultModelName, config.ModelName);
        Assert.Equal(AuditConfiguration.DefaultModelName, config.FixModelName);
        Assert.Equal(8192, config.NumCtx);
        Assert.Equal(1200, config.NumPredict);
        Assert.Null(config.Stack);
    }

    [Theory]
    [InlineData(null, "flag", "flag")]
    [InlineData("env", "flag", "env")]
    [InlineData("  ", "flag", "flag")]
    [InlineData("env", null, "env")]
    public void Resolve_Model_PrefersEnvironmentOverFlag(string? fromEnvironment, string? flag, string expected)
    {
        AppConfiguration config = Resolve(
            model: flag,
            environment: new() { [AppConfiguration.ModelVariable] = fromEnvironment });

        Assert.Equal(expected, config.ModelName);
    }

    [Theory]
    [InlineData(null, null, "audit")]
    [InlineData(null, "flag", "flag")]
    [InlineData("env", "flag", "env")]
    public void Resolve_FixModel_FallsBackToAuditModel(string? fromEnvironment, string? flag, string expected)
    {
        AppConfiguration config = Resolve(
            model: "audit",
            fixModel: flag,
            environment: new() { [AppConfiguration.FixModelVariable] = fromEnvironment });

        Assert.Equal(expected, config.FixModelName);
    }

    [Theory]
    [InlineData("4096", "500", 4096, 500)]
    [InlineData("many", "", 8192, 1200)]
    public void Resolve_Limits_ReadNumbersFromEnvironment(
        string numCtx,
        string numPredict,
        int expectedCtx,
        int expectedPredict)
    {
        AppConfiguration config = Resolve(environment: new()
        {
            [AppConfiguration.NumCtxVariable] = numCtx,
            [AppConfiguration.NumPredictVariable] = numPredict
        });

        Assert.Equal(expectedCtx, config.ForFileAudit().NumCtx);
        Assert.Equal(expectedPredict, config.ForFileAudit().NumPredict);
    }

    [Fact]
    public void ForSystemAudit_IgnoresFileAuditLimits()
    {
        AppConfiguration config = Resolve(
            model: "audit",
            environment: new() { [AppConfiguration.NumCtxVariable] = "4096" });

        Assert.Equal(AuditConfiguration.ForSystemAudit("audit"), config.ForSystemAudit());
    }

    [Fact]
    public void Resolve_Storage_PrefersEnvironmentThenFlag()
    {
        var environment = new Dictionary<string, string?> { [AppConfiguration.StorageVariable] = "from-env" };

        Assert.Equal("from-env", Resolve(storage: "from-flag", environment: environment).StoragePath);
        Assert.Equal(Path.GetFullPath("from-flag"), Resolve(storage: "from-flag").StoragePath);
    }

    [Fact]
    public void Resolve_Storage_SearchesUpwardsFromBaseDirectory()
    {
        string root = Directory.CreateTempSubdirectory("csa-config-").FullName;
        try
        {
            string storage = Directory.CreateDirectory(Path.Combine(root, "WorkstationStorage")).FullName;
            string nested = Directory.CreateDirectory(Path.Combine(root, "bin", "Debug")).FullName;

            AppConfiguration config = Resolve(baseDirectory: nested);

            Assert.Equal(storage, config.StoragePath);
            Assert.Equal(Path.Combine(storage, "Rules"), config.RulesPath);
            Assert.Equal(Path.Combine(storage, "Targets"), config.TargetsPath);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AppConfiguration Resolve(
        string? storage = null,
        string? model = null,
        string? fixModel = null,
        Dictionary<string, string?>? environment = null,
        string? baseDirectory = null) =>
        AppConfiguration.Resolve(
            storage,
            model,
            fixModel,
            stack: null,
            name => environment?.GetValueOrDefault(name),
            baseDirectory ?? AppContext.BaseDirectory);
}
