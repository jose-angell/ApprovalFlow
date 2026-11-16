using ApprovalFlow.Domain.Entities;

namespace ApprovalFlow.Tests.Domain;

public class DepartmentTests
{
    [Fact]
    public void Create_WithValidData_SetsDefaults()
    {
        var department = Department.Create("  Compras  ", "  Área de compras ");

        Assert.NotEqual(Guid.Empty, department.Id);
        Assert.Equal("Compras", department.Name);
        Assert.Equal("Área de compras", department.Description);
        Assert.True(department.IsActive);
        Assert.Equal(DateTimeKind.Utc, department.CreatedAt.Kind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_Throws(string? name)
    {
        Assert.Throws<ArgumentException>(() => Department.Create(name!, null));
    }

    [Fact]
    public void Create_WithNameOver100Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() => Department.Create(new string('a', 101), null));
    }

    [Fact]
    public void Create_WithNameOf100Chars_Succeeds()
    {
        var department = Department.Create(new string('a', 100), null);
        Assert.Equal(100, department.Name.Length);
    }

    [Fact]
    public void Create_WithDescriptionOver500Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() => Department.Create("Compras", new string('a', 501)));
    }

    [Fact]
    public void Create_WithBlankDescription_StoresNull()
    {
        var department = Department.Create("Compras", "   ");
        Assert.Null(department.Description);
    }

    [Fact]
    public void Update_WithValidData_ChangesNameAndDescription()
    {
        var department = Department.Create("Compras", null);

        department.Update(" Finanzas ", "Desc");

        Assert.Equal("Finanzas", department.Name);
        Assert.Equal("Desc", department.Description);
    }

    [Fact]
    public void Update_WithInvalidName_ThrowsAndKeepsState()
    {
        var department = Department.Create("Compras", "Desc");

        Assert.Throws<ArgumentException>(() => department.Update("", "Otra"));

        Assert.Equal("Compras", department.Name);
        Assert.Equal("Desc", department.Description);
    }

    [Fact]
    public void Deactivate_ThenActivate_TogglesIsActive()
    {
        var department = Department.Create("Compras", null);

        department.Deactivate();
        Assert.False(department.IsActive);

        department.Activate();
        Assert.True(department.IsActive);
    }
}
