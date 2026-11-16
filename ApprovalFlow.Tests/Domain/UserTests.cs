using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Tests.Domain;

public class UserTests
{
    private static Department ActiveDepartment() => Department.Create("Compras", null);

    private static Department InactiveDepartment()
    {
        var department = ActiveDepartment();
        department.Deactivate();
        return department;
    }

    private static User ValidRequester(Department? department = null) =>
        User.Create("Ana Pérez", "ana@mail.com", "hash", UserRole.Requester, department ?? ActiveDepartment());

    [Fact]
    public void Create_WithValidData_SetsDefaults()
    {
        var department = ActiveDepartment();

        var user = User.Create("  Ana Pérez ", "ana@mail.com", "hash", UserRole.Requester, department);

        Assert.NotEqual(Guid.Empty, user.Id);
        Assert.Equal("Ana Pérez", user.FullName);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(UserRole.Requester, user.Role);
        Assert.Equal(department.Id, user.DepartmentId);
        Assert.True(user.IsActive);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAt.Kind);
        Assert.Null(user.UpdatedAt);
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var user = User.Create("Ana", "  Ana@Mail.COM ", "hash", UserRole.Requester, ActiveDepartment());

        Assert.Equal("ana@mail.com", user.Email);
    }

    [Theory]
    [InlineData("no-es-email")]
    [InlineData("Ana <ana@mail.com>")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Create_WithInvalidEmail_Throws(string? email)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("Ana", email!, "hash", UserRole.Requester, ActiveDepartment()));
    }

    [Fact]
    public void Create_WithEmailOver256Chars_Throws()
    {
        var email = new string('a', 250) + "@mail.com";

        Assert.Throws<ArgumentException>(() =>
            User.Create("Ana", email, "hash", UserRole.Requester, ActiveDepartment()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyFullName_Throws(string? fullName)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(fullName!, "ana@mail.com", "hash", UserRole.Requester, ActiveDepartment()));
    }

    [Fact]
    public void Create_WithFullNameOver150Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create(new string('a', 151), "ana@mail.com", "hash", UserRole.Requester, ActiveDepartment()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPasswordHash_Throws(string? passwordHash)
    {
        Assert.Throws<ArgumentException>(() =>
            User.Create("Ana", "ana@mail.com", passwordHash!, UserRole.Requester, ActiveDepartment()));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(99)]
    public void Create_WithUndefinedRole_Throws(int role)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            User.Create("Ana", "ana@mail.com", "hash", (UserRole)role, ActiveDepartment()));
    }

    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Approver)]
    [InlineData(UserRole.Finance)]
    public void Create_NonAdminWithoutDepartment_Throws(UserRole role)
    {
        Assert.Throws<ArgumentNullException>(() =>
            User.Create("Ana", "ana@mail.com", "hash", role, null));
    }

    [Fact]
    public void Create_AdminWithoutDepartment_HasNullDepartmentId()
    {
        var user = User.Create("Root", "root@mail.com", "hash", UserRole.Admin, null);

        Assert.Null(user.DepartmentId);
    }

    [Fact]
    public void Create_WithInactiveDepartment_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            User.Create("Ana", "ana@mail.com", "hash", UserRole.Requester, InactiveDepartment()));
    }

    [Fact]
    public void UpdateProfile_WithValidData_ChangesDataAndSetsUpdatedAt()
    {
        var user = ValidRequester();

        user.UpdateProfile(" Ana María ", "NUEVO@Mail.com");

        Assert.Equal("Ana María", user.FullName);
        Assert.Equal("nuevo@mail.com", user.Email);
        Assert.NotNull(user.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, user.UpdatedAt!.Value.Kind);
    }

    [Fact]
    public void UpdateProfile_WithInvalidEmail_ThrowsAndKeepsState()
    {
        var user = ValidRequester();

        Assert.Throws<ArgumentException>(() => user.UpdateProfile("Otro", "no-es-email"));

        Assert.Equal("Ana Pérez", user.FullName);
        Assert.Equal("ana@mail.com", user.Email);
        Assert.Null(user.UpdatedAt);
    }

    [Fact]
    public void ChangePasswordHash_WithValidHash_ChangesHashAndSetsUpdatedAt()
    {
        var user = ValidRequester();

        user.ChangePasswordHash("nuevo-hash");

        Assert.Equal("nuevo-hash", user.PasswordHash);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void ChangePasswordHash_WithEmptyHash_Throws()
    {
        var user = ValidRequester();

        Assert.Throws<ArgumentException>(() => user.ChangePasswordHash(" "));
        Assert.Equal("hash", user.PasswordHash);
    }

    [Fact]
    public void ChangeRole_ToApproverWithDepartment_UpdatesRoleAndDepartment()
    {
        var user = ValidRequester();
        var other = Department.Create("Finanzas", null);

        user.ChangeRole(UserRole.Approver, other);

        Assert.Equal(UserRole.Approver, user.Role);
        Assert.Equal(other.Id, user.DepartmentId);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void ChangeRole_ToNonAdminWithoutDepartment_ThrowsAndKeepsState()
    {
        var user = ValidRequester();
        var departmentId = user.DepartmentId;

        Assert.Throws<ArgumentNullException>(() => user.ChangeRole(UserRole.Approver, null));

        Assert.Equal(UserRole.Requester, user.Role);
        Assert.Equal(departmentId, user.DepartmentId);
        Assert.Null(user.UpdatedAt);
    }

    [Fact]
    public void ChangeRole_ToAdminWithoutDepartment_ClearsDepartment()
    {
        var user = ValidRequester();

        user.ChangeRole(UserRole.Admin, null);

        Assert.Equal(UserRole.Admin, user.Role);
        Assert.Null(user.DepartmentId);
    }

    [Fact]
    public void ChangeRole_WithInactiveDepartment_Throws()
    {
        var user = ValidRequester();

        Assert.Throws<InvalidOperationException>(() => user.ChangeRole(UserRole.Approver, InactiveDepartment()));
    }

    [Fact]
    public void AssignDepartment_WithActiveDepartment_SetsDepartmentIdAndUpdatedAt()
    {
        var user = ValidRequester();
        var other = Department.Create("Finanzas", null);

        user.AssignDepartment(other);

        Assert.Equal(other.Id, user.DepartmentId);
        Assert.NotNull(user.UpdatedAt);
    }

    [Fact]
    public void AssignDepartment_WithInactiveDepartment_Throws()
    {
        var user = ValidRequester();
        var departmentId = user.DepartmentId;

        Assert.Throws<InvalidOperationException>(() => user.AssignDepartment(InactiveDepartment()));

        Assert.Equal(departmentId, user.DepartmentId);
    }

    [Fact]
    public void AssignDepartment_WithNull_Throws()
    {
        var user = ValidRequester();

        Assert.Throws<ArgumentNullException>(() => user.AssignDepartment(null!));
    }

    [Fact]
    public void DeactivateAndActivate_ToggleIsActiveAndSetUpdatedAt()
    {
        var user = ValidRequester();

        user.Deactivate();
        Assert.False(user.IsActive);
        Assert.NotNull(user.UpdatedAt);

        user.Activate();
        Assert.True(user.IsActive);
    }
}
