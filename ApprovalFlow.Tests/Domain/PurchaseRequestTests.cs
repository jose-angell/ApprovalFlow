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

    // ---------- Helpers de transiciones ----------

    private static User ApproverIn(Department department, string email = "jefe@mail.com") =>
        User.Create("Jefe", email, "hash", UserRole.Approver, department);

    // Devuelve la solicitud en el estado pedido, junto con su departamento, solicitante y aprobador.
    private static (PurchaseRequest Request, Department Department, User Requester, User Approver) RequestIn(PurchaseRequestStatus status)
    {
        var department = ActiveDepartment();
        var requester = ActiveRequester(department);
        var approver = ApproverIn(department);
        var request = NewRequest(requester);

        switch (status)
        {
            case PurchaseRequestStatus.Pending:
                break;
            case PurchaseRequestStatus.InReview:
                request.StartReview(approver);
                break;
            case PurchaseRequestStatus.Approved:
                request.StartReview(approver);
                request.Approve(approver, null);
                break;
            case PurchaseRequestStatus.Rejected:
                request.StartReview(approver);
                request.Reject(approver, "No procede");
                break;
            case PurchaseRequestStatus.Cancelled:
                request.Cancel(requester);
                break;
        }

        return (request, department, requester, approver);
    }

    private static void AssertUtcSet(DateTime? date)
    {
        Assert.NotNull(date);
        Assert.Equal(DateTimeKind.Utc, date!.Value.Kind);
    }

    // ---------- StartReview ----------

    [Fact]
    public void StartReview_ByValidApprover_MovesToInReviewAndSetsAudit()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.Pending);

        request.StartReview(approver);

        Assert.Equal(PurchaseRequestStatus.InReview, request.Status);
        Assert.Equal(approver.Id, request.ReviewedById);
        AssertUtcSet(request.ReviewedAt);
        AssertUtcSet(request.UpdatedAt);
    }

    [Fact]
    public void StartReview_WithNullApprover_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.Pending);

        Assert.Throws<ArgumentNullException>(() => request.StartReview(null!));
    }

    [Theory]
    [InlineData(UserRole.Requester)]
    [InlineData(UserRole.Finance)]
    public void StartReview_ByNonApproverRole_Throws(UserRole role)
    {
        var (request, department, _, _) = RequestIn(PurchaseRequestStatus.Pending);
        var actor = User.Create("Otro", "otro@mail.com", "hash", role, department);

        Assert.Throws<InvalidOperationException>(() => request.StartReview(actor));
    }

    [Fact]
    public void StartReview_ByAdmin_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.Pending);
        var admin = User.Create("Root", "root@mail.com", "hash", UserRole.Admin, null);

        Assert.Throws<InvalidOperationException>(() => request.StartReview(admin));
    }

    [Fact]
    public void StartReview_ByInactiveApprover_Throws()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.Pending);
        approver.Deactivate();

        Assert.Throws<InvalidOperationException>(() => request.StartReview(approver));
    }

    [Fact]
    public void StartReview_ByApproverOfOtherDepartment_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.Pending);
        var outsider = ApproverIn(Department.Create("Finanzas", null), "fuera@mail.com");

        Assert.Throws<InvalidOperationException>(() => request.StartReview(outsider));
    }

    [Fact]
    public void StartReview_ByTheRequesterHimself_Throws()
    {
        // El solicitante pasa a ser Approver del mismo departamento y pretende revisar lo suyo.
        var department = ActiveDepartment();
        var requester = ActiveRequester(department);
        var request = NewRequest(requester);
        requester.ChangeRole(UserRole.Approver, department);

        Assert.Throws<InvalidOperationException>(() => request.StartReview(requester));
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.InReview)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void StartReview_FromNonPendingStatus_Throws(PurchaseRequestStatus status)
    {
        var (request, _, _, approver) = RequestIn(status);

        Assert.Throws<InvalidOperationException>(() => request.StartReview(approver));
    }

    // ---------- Approve ----------

    [Fact]
    public void Approve_ByValidApprover_MovesToApprovedAndSetsAudit()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        request.Approve(approver, "  Todo en orden ");

        Assert.Equal(PurchaseRequestStatus.Approved, request.Status);
        Assert.Equal("Todo en orden", request.Comments);
        Assert.Equal(approver.Id, request.ApprovedById);
        AssertUtcSet(request.ApprovedAt);
        AssertUtcSet(request.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Approve_WithoutComments_StoresNull(string? comments)
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        request.Approve(approver, comments);

        Assert.Null(request.Comments);
    }

    [Fact]
    public void Approve_WithCommentsOver1000Chars_Throws()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        Assert.Throws<ArgumentException>(() => request.Approve(approver, new string('a', 1001)));
        Assert.Equal(PurchaseRequestStatus.InReview, request.Status);
    }

    [Fact]
    public void Approve_ByAnotherApproverOfSameDepartment_Succeeds()
    {
        var (request, department, _, _) = RequestIn(PurchaseRequestStatus.InReview);
        var other = ApproverIn(department, "otro-jefe@mail.com");

        request.Approve(other, null);

        Assert.Equal(PurchaseRequestStatus.Approved, request.Status);
        Assert.Equal(other.Id, request.ApprovedById);
    }

    [Fact]
    public void Approve_ByNonApproverRole_Throws()
    {
        var (request, department, _, _) = RequestIn(PurchaseRequestStatus.InReview);
        var finance = User.Create("Fin", "fin@mail.com", "hash", UserRole.Finance, department);

        Assert.Throws<InvalidOperationException>(() => request.Approve(finance, null));
    }

    [Fact]
    public void Approve_ByInactiveApprover_Throws()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);
        approver.Deactivate();

        Assert.Throws<InvalidOperationException>(() => request.Approve(approver, null));
    }

    [Fact]
    public void Approve_ByApproverOfOtherDepartment_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.InReview);
        var outsider = ApproverIn(Department.Create("Finanzas", null), "fuera@mail.com");

        Assert.Throws<InvalidOperationException>(() => request.Approve(outsider, null));
    }

    [Fact]
    public void Approve_ByTheRequesterHimself_Throws()
    {
        var department = ActiveDepartment();
        var requester = ActiveRequester(department);
        var approver = ApproverIn(department);
        var request = NewRequest(requester);
        request.StartReview(approver);
        requester.ChangeRole(UserRole.Approver, department);

        Assert.Throws<InvalidOperationException>(() => request.Approve(requester, null));
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Pending)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Approve_FromNonInReviewStatus_Throws(PurchaseRequestStatus status)
    {
        var (request, _, _, approver) = RequestIn(status);

        Assert.Throws<InvalidOperationException>(() => request.Approve(approver, null));
    }

    // ---------- Reject ----------

    [Fact]
    public void Reject_ByValidApprover_MovesToRejectedAndSetsAudit()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        request.Reject(approver, " Fuera de presupuesto ");

        Assert.Equal(PurchaseRequestStatus.Rejected, request.Status);
        Assert.Equal("Fuera de presupuesto", request.Comments);
        Assert.Equal(approver.Id, request.RejectedById);
        AssertUtcSet(request.RejectedAt);
        AssertUtcSet(request.UpdatedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithEmptyComments_Throws(string? comments)
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        Assert.Throws<ArgumentException>(() => request.Reject(approver, comments!));
        Assert.Equal(PurchaseRequestStatus.InReview, request.Status);
    }

    [Fact]
    public void Reject_WithCommentsOver1000Chars_Throws()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);

        Assert.Throws<ArgumentException>(() => request.Reject(approver, new string('a', 1001)));
    }

    [Fact]
    public void Reject_ByNonApproverRole_Throws()
    {
        var (request, department, _, _) = RequestIn(PurchaseRequestStatus.InReview);
        var finance = User.Create("Fin", "fin@mail.com", "hash", UserRole.Finance, department);

        Assert.Throws<InvalidOperationException>(() => request.Reject(finance, "No"));
    }

    [Fact]
    public void Reject_ByInactiveApprover_Throws()
    {
        var (request, _, _, approver) = RequestIn(PurchaseRequestStatus.InReview);
        approver.Deactivate();

        Assert.Throws<InvalidOperationException>(() => request.Reject(approver, "No"));
    }

    [Fact]
    public void Reject_ByApproverOfOtherDepartment_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.InReview);
        var outsider = ApproverIn(Department.Create("Finanzas", null), "fuera@mail.com");

        Assert.Throws<InvalidOperationException>(() => request.Reject(outsider, "No"));
    }

    [Fact]
    public void Reject_ByTheRequesterHimself_Throws()
    {
        var department = ActiveDepartment();
        var requester = ActiveRequester(department);
        var approver = ApproverIn(department);
        var request = NewRequest(requester);
        request.StartReview(approver);
        requester.ChangeRole(UserRole.Approver, department);

        Assert.Throws<InvalidOperationException>(() => request.Reject(requester, "No"));
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.Pending)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Reject_FromNonInReviewStatus_Throws(PurchaseRequestStatus status)
    {
        var (request, _, _, approver) = RequestIn(status);

        Assert.Throws<InvalidOperationException>(() => request.Reject(approver, "No"));
    }

    // ---------- Cancel ----------

    [Fact]
    public void Cancel_ByOwnerWhenPending_MovesToCancelledAndSetsAudit()
    {
        var (request, _, requester, _) = RequestIn(PurchaseRequestStatus.Pending);

        request.Cancel(requester);

        Assert.Equal(PurchaseRequestStatus.Cancelled, request.Status);
        Assert.Equal(requester.Id, request.CancelledById);
        AssertUtcSet(request.CancelledAt);
        AssertUtcSet(request.UpdatedAt);
    }

    [Fact]
    public void Cancel_ByNonOwner_Throws()
    {
        var (request, department, _, _) = RequestIn(PurchaseRequestStatus.Pending);
        var other = User.Create("Otro", "otro@mail.com", "hash", UserRole.Requester, department);

        Assert.Throws<InvalidOperationException>(() => request.Cancel(other));
    }

    [Fact]
    public void Cancel_ByInactiveOwner_Throws()
    {
        var (request, _, requester, _) = RequestIn(PurchaseRequestStatus.Pending);
        requester.Deactivate();

        Assert.Throws<InvalidOperationException>(() => request.Cancel(requester));
    }

    [Fact]
    public void Cancel_WithNullRequester_Throws()
    {
        var (request, _, _, _) = RequestIn(PurchaseRequestStatus.Pending);

        Assert.Throws<ArgumentNullException>(() => request.Cancel(null!));
    }

    [Theory]
    [InlineData(PurchaseRequestStatus.InReview)]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void Cancel_FromNonPendingStatus_Throws(PurchaseRequestStatus status)
    {
        var (request, _, requester, _) = RequestIn(status);

        Assert.Throws<InvalidOperationException>(() => request.Cancel(requester));
    }

    // ---------- Estados finales ----------

    [Theory]
    [InlineData(PurchaseRequestStatus.Approved)]
    [InlineData(PurchaseRequestStatus.Rejected)]
    [InlineData(PurchaseRequestStatus.Cancelled)]
    public void FinalStatus_RejectsEveryMethod(PurchaseRequestStatus status)
    {
        var (request, _, requester, approver) = RequestIn(status);

        Assert.Throws<InvalidOperationException>(() => request.Update(requester, "Nuevo", "Justificación", 10m));
        Assert.Throws<InvalidOperationException>(() => request.StartReview(approver));
        Assert.Throws<InvalidOperationException>(() => request.Approve(approver, null));
        Assert.Throws<InvalidOperationException>(() => request.Reject(approver, "No"));
        Assert.Throws<InvalidOperationException>(() => request.Cancel(requester));
        Assert.Equal(status, request.Status);
    }

    [Fact]
    public void Update_FromInReview_Throws()
    {
        var (request, _, requester, _) = RequestIn(PurchaseRequestStatus.InReview);

        Assert.Throws<InvalidOperationException>(() => request.Update(requester, "Nuevo", "Justificación", 10m));
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
