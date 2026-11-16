using System.Net.Mail;
using ApprovalFlow.Domain.Enums;

namespace ApprovalFlow.Domain.Entities;

public class User
{
    public const int FullNameMaxLength = 150;
    public const int EmailMaxLength = 256;

    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Requerido por EF Core.
    private User() { }

    public static User Create(string fullName, string email, string passwordHash, UserRole role, Department? department)
    {
        var cleanFullName = ValidateFullName(fullName);
        var cleanEmail = ValidateEmail(email);
        ValidatePasswordHash(passwordHash);
        ValidateRole(role);
        var departmentId = ResolveDepartmentId(role, department);

        return new User
        {
            Id = Guid.NewGuid(),
            FullName = cleanFullName,
            Email = cleanEmail,
            PasswordHash = passwordHash,
            Role = role,
            DepartmentId = departmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateProfile(string fullName, string email)
    {
        var cleanFullName = ValidateFullName(fullName);
        var cleanEmail = ValidateEmail(email);

        FullName = cleanFullName;
        Email = cleanEmail;
        Touch();
    }

    public void ChangePasswordHash(string passwordHash)
    {
        ValidatePasswordHash(passwordHash);

        PasswordHash = passwordHash;
        Touch();
    }

    public void ChangeRole(UserRole role, Department? department)
    {
        ValidateRole(role);
        var departmentId = ResolveDepartmentId(role, department);

        Role = role;
        DepartmentId = departmentId;
        Touch();
    }

    public void AssignDepartment(Department department)
    {
        ArgumentNullException.ThrowIfNull(department);
        EnsureDepartmentIsActive(department);

        DepartmentId = department.Id;
        Touch();
    }

    public void Activate()
    {
        IsActive = true;
        Touch();
    }

    public void Deactivate()
    {
        IsActive = false;
        Touch();
    }

    private void Touch() => UpdatedAt = DateTime.UtcNow;

    private static string ValidateFullName(string fullName)
    {
        var clean = fullName?.Trim();
        if (string.IsNullOrEmpty(clean))
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(fullName));
        if (clean.Length > FullNameMaxLength)
            throw new ArgumentException($"El nombre completo no puede exceder {FullNameMaxLength} caracteres.", nameof(fullName));
        return clean;
    }

    private static string ValidateEmail(string email)
    {
        var clean = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(clean))
            throw new ArgumentException("El email es obligatorio.", nameof(email));
        if (clean.Length > EmailMaxLength)
            throw new ArgumentException($"El email no puede exceder {EmailMaxLength} caracteres.", nameof(email));

        MailAddress parsed;
        try
        {
            parsed = new MailAddress(clean);
        }
        catch (FormatException)
        {
            throw new ArgumentException("El formato del email no es válido.", nameof(email));
        }

        // Se compara la dirección para rechazar formatos como "Nombre <a@b.com>".
        if (parsed.Address != clean)
            throw new ArgumentException("El formato del email no es válido.", nameof(email));

        return clean;
    }

    private static void ValidatePasswordHash(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("El hash de la contraseña es obligatorio.", nameof(passwordHash));
    }

    private static void ValidateRole(UserRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role), role, "El rol no es un valor definido.");
    }

    private static Guid? ResolveDepartmentId(UserRole role, Department? department)
    {
        if (department is null)
        {
            if (role != UserRole.Admin)
                throw new ArgumentNullException(nameof(department), "El departamento es obligatorio salvo para Admin.");
            return null;
        }

        EnsureDepartmentIsActive(department);
        return department.Id;
    }

    private static void EnsureDepartmentIsActive(Department department)
    {
        if (!department.IsActive)
            throw new InvalidOperationException("El departamento está inactivo.");
    }
}
