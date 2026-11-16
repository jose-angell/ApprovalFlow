namespace ApprovalFlow.Domain.Entities;

public class Department
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Requerido por EF Core.
    private Department() { }

    public static Department Create(string name, string? description)
    {
        var department = new Department
        {
            Id = Guid.NewGuid(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        department.SetData(name, description);
        return department;
    }

    public void Update(string name, string? description) => SetData(name, description);

    public void Activate() => IsActive = true;

    public void Deactivate() => IsActive = false;

    private void SetData(string name, string? description)
    {
        var cleanName = name?.Trim();
        if (string.IsNullOrEmpty(cleanName))
            throw new ArgumentException("El nombre es obligatorio.", nameof(name));
        if (cleanName.Length > NameMaxLength)
            throw new ArgumentException($"El nombre no puede exceder {NameMaxLength} caracteres.", nameof(name));

        var cleanDescription = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleanDescription is { Length: > DescriptionMaxLength })
            throw new ArgumentException($"La descripción no puede exceder {DescriptionMaxLength} caracteres.", nameof(description));

        Name = cleanName;
        Description = cleanDescription;
    }
}
