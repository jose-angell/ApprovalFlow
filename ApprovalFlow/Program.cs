using ApprovalFlow.Api.Extensions;
using ApprovalFlow.Api.Middleware;
using ApprovalFlow.Application;
using ApprovalFlow.Infrastructure;
using ApprovalFlow.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddJwtAuthentication(builder.Configuration);

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSwaggerWithBearer();
}
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

var app = builder.Build();

await app.Services.SeedAdminAsync();

// Configure the HTTP request pipeline.

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
