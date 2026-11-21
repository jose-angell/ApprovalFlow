using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Auth;
using ApprovalFlow.Application.Departments;
using ApprovalFlow.Application.PurchaseRequests;
using ApprovalFlow.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ApprovalFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserUseCase, UserUseCase>();
        services.AddScoped<IAuthUseCase, AuthUseCase>();
        services.AddScoped<IUserSessionUseCase, UserSessionUseCase>();
        services.AddScoped<IDepartmentUseCase, DepartmentUseCase>();
        services.AddScoped<IPurchaseRequestUseCase, PurchaseRequestUseCase>();
        return services;
    }
}
