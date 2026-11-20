using ApprovalFlow.Api.Authorization;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Departments.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalFlow.Api.Controllers;

[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController(IDepartmentUseCase departmentUseCase) : ControllerBase
{
    private readonly IDepartmentUseCase _departmentUseCase = departmentUseCase;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> GetAll(CancellationToken ct)
        => Ok(await _departmentUseCase.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _departmentUseCase.GetByIdAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentDto dto, CancellationToken ct)
    {
        var department = await _departmentUseCase.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = department.Id }, department);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpdateDepartmentDto dto, CancellationToken ct)
        => Ok(await _departmentUseCase.UpdateAsync(id, dto, ct));

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Activate(Guid id, CancellationToken ct)
    {
        await _departmentUseCase.ActivateAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/deactivate")]
    [Authorize(Roles = Roles.Admin)]
    public async Task<IActionResult> Deactivate(Guid id, CancellationToken ct)
    {
        await _departmentUseCase.DeactivateAsync(id, ct);
        return NoContent();
    }
}
