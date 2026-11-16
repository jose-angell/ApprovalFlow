using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Domain.Entities;

public class PurchaseRequest
{
    public const int RequestNumberMaxLength = 30;
    public const int TitleMaxLength = 150;
    public const int JustificationMaxLength = 2000;
    public const int CommentsMaxLength = 1000;

    public Guid Id { get; private set; }
    public string RequestNumber { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Justification { get; private set; } = string.Empty;
    public string? Comments { get; private set; }
    public decimal TotalAmount { get; private set; }
    public PurchaseRequestStatus Status { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid DepartmentId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public Guid? ReviewedById { get; private set; }
    public DateTime? ApprovedAt { get; private set; }
    public Guid? ApprovedById { get; private set; }
    public DateTime? RejectedAt { get; private set; }
    public Guid? RejectedById { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public Guid? CancelledById { get; private set; }

    // Requerido por EF Core.
    private PurchaseRequest() { }

    public static PurchaseRequest Create(string requestNumber, string title, string justification, decimal totalAmount, User requester)
    {
        ArgumentNullException.ThrowIfNull(requester);

        if (requester.Role != UserRole.Requester)
            throw new InvalidOperationException("Solo un usuario con rol Requester puede crear solicitudes.");
        if (!requester.IsActive)
            throw new InvalidOperationException("El solicitante está inactivo.");
        if (requester.DepartmentId is null)
            throw new InvalidOperationException("El solicitante no tiene departamento.");

        var cleanRequestNumber = ValidateRequired(requestNumber, RequestNumberMaxLength, "El número de solicitud", nameof(requestNumber));
        var cleanTitle = ValidateRequired(title, TitleMaxLength, "El título", nameof(title));
        var cleanJustification = ValidateRequired(justification, JustificationMaxLength, "La justificación", nameof(justification));
        ValidateTotalAmount(totalAmount);

        return new PurchaseRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = cleanRequestNumber,
            Title = cleanTitle,
            Justification = cleanJustification,
            TotalAmount = totalAmount,
            Status = PurchaseRequestStatus.Pending,
            RequesterId = requester.Id,
            DepartmentId = requester.DepartmentId.Value,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Update(User requester, string title, string justification, decimal totalAmount)
    {
        EnsureIsActiveOwner(requester);
        EnsureStatus(PurchaseRequestStatus.Pending);

        var cleanTitle = ValidateRequired(title, TitleMaxLength, "El título", nameof(title));
        var cleanJustification = ValidateRequired(justification, JustificationMaxLength, "La justificación", nameof(justification));
        ValidateTotalAmount(totalAmount);

        Title = cleanTitle;
        Justification = cleanJustification;
        TotalAmount = totalAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void StartReview(User approver)
    {
        EnsureCanReview(approver);
        EnsureStatus(PurchaseRequestStatus.Pending);

        var now = DateTime.UtcNow;
        Status = PurchaseRequestStatus.InReview;
        ReviewedAt = now;
        ReviewedById = approver.Id;
        UpdatedAt = now;
    }

    public void Approve(User approver, string? comments)
    {
        EnsureCanReview(approver);
        EnsureStatus(PurchaseRequestStatus.InReview);
        var cleanComments = ValidateOptionalComments(comments);

        var now = DateTime.UtcNow;
        Status = PurchaseRequestStatus.Approved;
        Comments = cleanComments;
        ApprovedAt = now;
        ApprovedById = approver.Id;
        UpdatedAt = now;
    }

    public void Reject(User approver, string comments)
    {
        EnsureCanReview(approver);
        EnsureStatus(PurchaseRequestStatus.InReview);
        var cleanComments = ValidateRequired(comments, CommentsMaxLength, "El comentario", nameof(comments));

        var now = DateTime.UtcNow;
        Status = PurchaseRequestStatus.Rejected;
        Comments = cleanComments;
        RejectedAt = now;
        RejectedById = approver.Id;
        UpdatedAt = now;
    }

    public void Cancel(User requester)
    {
        EnsureIsActiveOwner(requester);
        EnsureStatus(PurchaseRequestStatus.Pending);

        var now = DateTime.UtcNow;
        Status = PurchaseRequestStatus.Cancelled;
        CancelledAt = now;
        CancelledById = requester.Id;
        UpdatedAt = now;
    }

    private void EnsureCanReview(User approver)
    {
        ArgumentNullException.ThrowIfNull(approver);

        if (approver.Role != UserRole.Approver)
            throw new InvalidOperationException("Solo un usuario con rol Approver puede revisar solicitudes.");
        if (!approver.IsActive)
            throw new InvalidOperationException("El aprobador está inactivo.");
        if (approver.DepartmentId != DepartmentId)
            throw new InvalidOperationException("El aprobador debe pertenecer al mismo departamento que la solicitud.");
        if (approver.Id == RequesterId)
            throw new InvalidOperationException("El solicitante no puede revisar su propia solicitud.");
    }

    private void EnsureIsActiveOwner(User requester)
    {
        ArgumentNullException.ThrowIfNull(requester);

        if (requester.Id != RequesterId)
            throw new InvalidOperationException("Solo el solicitante dueño de la solicitud puede realizar esta acción.");
        if (!requester.IsActive)
            throw new InvalidOperationException("El solicitante está inactivo.");
    }

    private void EnsureStatus(PurchaseRequestStatus expected)
    {
        if (Status != expected)
            throw new InvalidOperationException($"La solicitud debe estar en estado {expected} (estado actual: {Status}).");
    }

    private static string ValidateRequired(string value, int maxLength, string label, string paramName)
    {
        var clean = value?.Trim();
        if (string.IsNullOrEmpty(clean))
            throw new ArgumentException($"{label} es obligatorio.", paramName);
        if (clean.Length > maxLength)
            throw new ArgumentException($"{label} no puede exceder {maxLength} caracteres.", paramName);
        return clean;
    }

    private static string? ValidateOptionalComments(string? comments)
    {
        if (string.IsNullOrWhiteSpace(comments))
            return null;

        var clean = comments.Trim();
        if (clean.Length > CommentsMaxLength)
            throw new ArgumentException($"El comentario no puede exceder {CommentsMaxLength} caracteres.", nameof(comments));
        return clean;
    }

    private static void ValidateTotalAmount(decimal totalAmount)
    {
        if (totalAmount <= 0)
            throw new ArgumentException("El monto debe ser mayor que 0.", nameof(totalAmount));
        if (decimal.Round(totalAmount, 2) != totalAmount)
            throw new ArgumentException("El monto admite como máximo 2 decimales.", nameof(totalAmount));
    }
}
