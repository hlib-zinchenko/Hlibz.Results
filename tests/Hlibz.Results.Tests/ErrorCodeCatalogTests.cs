using System.Reflection;
using System.Reflection.Emit;

namespace Hlibz.Results.Tests;

public sealed class ErrorCodeCatalogTests
{
    [Fact]
    public void All_WithDeclaredCodes_ListsFieldsAndPropertiesSortedByCode()
    {
        ErrorCodeCatalog catalog = new([typeof(CatalogCodes).Assembly]);

        Assert.Equal(
            ["catalog_1001", "catalog_1002", "catalog_1003"],
            catalog.All.Select(errorCode => errorCode.Code));
    }

    [Fact]
    public void All_WithDerivedRecord_KeepsPrefixedCode()
    {
        ErrorCodeCatalog catalog = new([typeof(CatalogCodes).Assembly]);

        Assert.Contains(catalog.All, errorCode => errorCode is CatalogErrorCode);
    }

    [Fact]
    public void Validate_WithDuplicateCodes_Throws()
    {
        ErrorCodeCatalog catalog = new([BuildAssemblyWithDuplicate()]);

        InvalidOperationException exception =
            Assert.Throws<InvalidOperationException>(catalog.Validate);
        Assert.Contains("dup_1", exception.Message, StringComparison.Ordinal);
    }

    private static AssemblyBuilder BuildAssemblyWithDuplicate()
    {
        AssemblyBuilder assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("Duplicates"),
            AssemblyBuilderAccess.Run);
        ModuleBuilder module = assembly.DefineDynamicModule("Duplicates");
        TypeBuilder type = module.DefineType("Codes", TypeAttributes.Public | TypeAttributes.Class);
        FieldBuilder first = type.DefineField(
            "First",
            typeof(ErrorCode),
            FieldAttributes.Public | FieldAttributes.Static);
        FieldBuilder second = type.DefineField(
            "Second",
            typeof(ErrorCode),
            FieldAttributes.Public | FieldAttributes.Static);
        Type built = type.CreateType();
        built.GetField(first.Name)!.SetValue(null, new ErrorCode("dup_1", "One."));
        built.GetField(second.Name)!.SetValue(null, new ErrorCode("dup_1", "Two."));
        return assembly;
    }
}

public sealed record CatalogErrorCode(string Code, string Message)
    : ErrorCode($"catalog_{Code}", Message);

public static class CatalogCodes
{
    public static readonly ErrorCode Second = new CatalogErrorCode("1002", "Second.");

    public static readonly ErrorCode First = new CatalogErrorCode("1001", "First.");

    /// <summary>
    /// The same instance as <see cref="First"/>, so it isn't a duplicate.
    /// </summary>
    public static readonly ErrorCode FirstAlias = First;

    public static ErrorCode Third { get; } = new CatalogErrorCode("1003", "Third.");
}
