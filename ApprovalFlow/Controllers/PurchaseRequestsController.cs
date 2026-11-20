using ApprovalFlow.Api.Authorization;
using ApprovalFlow.Api.Extensions;
using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.PurchaseRequests.Dtos;
using ApprovalFlow.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApprovalFlow.Api.Controllers;

[ApiController]
[Route("api/purchase-requests")]
[Authorize]
public class PurchaseRequestsController(IPurchaseRequestUseCase purchaseRequestUseCase) : ControllerBase
{
    private readonly IPurchaseRequestUseCase _purchaseRequestUseCase = purchaseRequestUseCase;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PurchaseRequestDto>>> GetAll([FromQuery] PurchaseRequestStatus? status, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.GetAllAsync(User.GetUserId(), status, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PurchaseRequestDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.GetByIdAsync(id, User.GetUserId(), ct));

    [HttpPost]
    [Authorize(Roles = Roles.Requester)]
    public async Task<ActionResult<PurchaseRequestDto>> Create(CreatePurchaseRequestDto dto, CancellationToken ct)
    {
        var request = await _purchaseRequestUseCase.CreateAsync(User.GetUserId(), dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = request.Id }, request);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Requester)]
    public async Task<ActionResult<PurchaseRequestDto>> Update(Guid id, UpdatePurchaseRequestDto dto, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.UpdateAsync(id, User.GetUserId(), dto, ct));

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = Roles.Requester)]
    public async Task<ActionResult<PurchaseRequestDto>> Cancel(Guid id, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.CancelAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:guid}/start-review")]
    [Authorize(Roles = Roles.Approver)]
    public async Task<ActionResult<PurchaseRequestDto>> StartReview(Guid id, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.StartReviewAsync(id, User.GetUserId(), ct));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = Roles.Approver)]
    public async Task<ActionResult<PurchaseRequestDto>> Approve(Guid id, ApprovePurchaseRequestDto? dto, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.ApproveAsync(id, User.GetUserId(), dto ?? new ApprovePurchaseRequestDto(), ct));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = Roles.Approver)]
    public async Task<ActionResult<PurchaseRequestDto>> Reject(Guid id, RejectPurchaseRequestDto dto, CancellationToken ct)
        => Ok(await _purchaseRequestUseCase.RejectAsync(id, User.GetUserId(), dto, ct));
}
