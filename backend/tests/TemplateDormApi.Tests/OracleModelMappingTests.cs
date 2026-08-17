using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TemplateDormApi.Data;
using TemplateDormApi.Models;

namespace TemplateDormApi.Tests;

public sealed class OracleModelMappingTests
{
    [Fact]
    public void ViolationRecord_UsesOnlyRecordIdAsOraclePrimaryKey()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/test")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(ViolationRecord));

        Assert.NotNull(entity);
        Assert.Equal("D_VIOLATION_RECORD", entity.GetTableName());
        Assert.Equal(nameof(ViolationRecord.RecordId), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.DoesNotContain(entity.GetProperties(), property => property.Name == "FeeId");
    }

    [Fact]
    public void AuditDetails_UsesUnquotedOracleColumnName()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/test")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(AuditEvent));
        var table = StoreObjectIdentifier.Table("D_AUDIT_EVENT", null);

        Assert.Equal("DETAILS", entity!.FindProperty(nameof(AuditEvent.Details))!.GetColumnName(table));
    }

    [Fact]
    public void HygieneComment_UsesOracleCommentColumnNameWithoutEmbeddedQuotes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/test")
            .Options;

        using var context = new AppDbContext(options);
        var entity = context.Model.FindEntityType(typeof(HygieneComment));
        var table = StoreObjectIdentifier.Table("D_HYGIENE_COMMENT", null);

        Assert.Equal("COMMENT", entity!.FindProperty(nameof(HygieneComment.CommentText))!.GetColumnName(table));
    }

    [Fact]
    public void UserAccount_IdIsGeneratedByOracle()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseOracle("User Id=test;Password=test;Data Source=localhost:1521/test")
            .Options;

        using var context = new AppDbContext(options);
        var accountId = context.Model.FindEntityType(typeof(UserAccount))!
            .FindProperty(nameof(UserAccount.AccountId));

        Assert.Equal(ValueGenerated.OnAdd, accountId!.ValueGenerated);
    }
}
