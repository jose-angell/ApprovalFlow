using ApprovalFlow.Domain.Entities;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Tests.Domain;

public class PurchaseRequestTests
{
    private static Department ActiveDepartment() => Department.Create("Compras", null);

    private static User ActiveRequester(Department? department = null) =>
        User.Create("Ana Pérez", "ana@mail.com", "hash", UserRole.Requester, department ?? ActiveDepartment());

    private static PurchaseRequest NewRequest(User requester) =>
        PurchaseRequest.Create("PR-0001", "Laptops", "Renovación de equipos", 1500.50m, requester);

    // ---------- Create ----------

    [Fact]
    public void Create_WithValidData_SetsDefaults()
    {
        var requester = ActiveRequester();

        var request = PurchaseRequest.Create("  PR-0001 ", " Laptops ", " Renovación de equipos ", 1500.50m, requester);

        Assert.NotEqual(Guid.Empty, request.Id);
        Assert.Equal("PR-0001", request.RequestNumber);
        Assert.Equal("Laptops", request.Title);
        Assert.Equal("Renovación de equipos", request.Justification);
        Assert.Equal(1500.50m, request.TotalAmount);
        Assert.Equal(PurchaseRequestStatus.Pending, request.Status);
        Assert.Equal(requester.Id, request.RequesterId);
        Assert.Equal(requester.DepartmentId, request.DepartmentId);
        Assert.Equal(DateTimeKind.Utc, request.CreatedAt.Kind);
        Assert.Null(request.UpdatedAt);
        Assert.Null(request.Comments);
        Assert.Null(request.ReviewedAt);
        Assert.Null(request.ApprovedAt);
        Assert.Null(request.RejectedAt);
        Assert.Null(request.CancelledAt);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Approver)]
    [InlineData(UserRole.Finance)]
    public void Create_WithNonRequesterRole_Throws(UserRole role)
    {
        var user = User.Create("Otro", "otro@mail.com", "hash", role, ActiveDepartment());

        Assert.Throws<InvalidOperationException>(() => NewRequest(user));
    }

    [Fact]
    public void Create_WithInactiveRequester_Throws()
    {
        var requester = ActiveRequester();
        requester.Deactivate();

        Assert.Throws<InvalidOperationException>(() => NewRequest(requester));
    }

    [Fact]
    public void Create_WithUserWithoutDepartment_Throws()
    {
        // Solo un Admin puede no tener departamento, y además se rechaza por rol.
        // La comprobación de DepartmentId nulo en Create es defensiva.
        var admin = User.Create("Root", "root@mail.com", "hash", UserRole.Admin, null);

        Assert.Throws<InvalidOperationException>(() => NewRequest(admin));
    }

    [Fact]
    public void Create_WithNullRequester_Throws()
    {
        Assert.Throws<ArgumentNullException>(() =>
            PurchaseRequest.Create("PR-0001", "Laptops", "Justificación", 10m, null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_WithZeroOrNegativeAmount_Throws(int amount)
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", "Laptops", "Justificación", amount, ActiveRequester()));
    }

    [Fact]
    public void Create_WithThreeDecimals_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", "Laptops", "Justificación", 10.123m, ActiveRequester()));
    }

    [Fact]
    public void Create_WithTwoDecimals_Succeeds()
    {
        var request = PurchaseRequest.Create("PR-0001", "Laptops", "Justificación", 10.12m, ActiveRequester());

        Assert.Equal(10.12m, request.TotalAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyRequestNumber_Throws(string? requestNumber)
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create(requestNumber!, "Laptops", "Justificación", 10m, ActiveRequester()));
    }

    [Fact]
    public void Create_WithRequestNumberOver30Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create(new string('a', 31), "Laptops", "Justificación", 10m, ActiveRequester()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyTitle_Throws(string? title)
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", title!, "Justificación", 10m, ActiveRequester()));
    }

    [Fact]
    public void Create_WithTitleOver150Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", new string('a', 151), "Justificación", 10m, ActiveRequester()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyJustification_Throws(string? justification)
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", "Laptops", justification!, 10m, ActiveRequester()));
    }

    [Fact]
    public void Create_WithJustificationOver2000Chars_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            PurchaseRequest.Create("PR-0001", "Laptops", new string('a', 2001), 10m, ActiveRequester()));
    }

    // ---------- Update ----------

    [Fact]
    public void Update_ByOwner_ChangesDataAndSetsUpdatedAt()
    {
        var requester = ActiveRequester();
        var request = NewRequest(requester);

        request.Update(requester, " Monitores ", " Nueva justificación ", 99.99m);

        Assert.Equal("Monitores", request.Title);
        Assert.Equal("Nueva justificación", request.Justification);
        Assert.Equal(99.99m, request.TotalAmount);
        Assert.Equal(PurchaseRequestStatus.Pending, request.Status);
        Assert.NotNull(request.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, request.UpdatedAt!.Value.Kind);
    }

    [Fact]
    public void Update_ByNonOwner_Throws()
    {
        var request = NewRequest(ActiveRequester());
        var other = User.Create("Otro", "otro@mail.com", "hash", UserRole.Requester, ActiveDepartment());

        Assert.Throws<InvalidOperationException>(() => request.Update(other, "Monitores", "Justificación", 10m));
    }

    [Fact]
    public void Update_ByInactiveOwner_Throws()
    {
        var requester = ActiveRequester();
        var request = NewRequest(requester);
        requester.Deactivate();

        Assert.Throws<InvalidOperationException>(() => request.Update(requester, "Monitores", "Justificación", 10m));
    }

    [Fact]
    public void Update_WithNullRequester_Throws()
    {
        var request = NewRequest(ActiveRequester());

        Assert.Throws<ArgumentNullException>(() => request.Update(null!, "Monitores", "Justificación", 10m));
    }

    [Fact]
    public void Update_WithInvalidData_ThrowsAndKeepsState()
    {
        var requester = ActiveRequester();
        var request = NewRequest(requester);

        Assert.Throws<ArgumentException>(() => request.Update(requester, "", "Justificación", 10m));
        Assert.Throws<ArgumentException>(() => request.Update(requester, "Monitores", "Justificación", 10.123m));

        Assert.Equal("Laptops", request.Title);
        Assert.Equal(1500.50m, request.TotalAmount);
        Assert.Null(request.UpdatedAt);
    }
}
