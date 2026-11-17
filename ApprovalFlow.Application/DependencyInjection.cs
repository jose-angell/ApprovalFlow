using ApprovalFlow.Application.Abstractions.UseCases;
using ApprovalFlow.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace ApprovalFlow.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IUserUseCase, UserUseCase>();
        return services;
    }
}
