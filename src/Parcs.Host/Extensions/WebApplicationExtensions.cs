using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Parcs.Data.Context;
using System.Net;

namespace Parcs.Host.Extensions
{
    public static class WebApplicationExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this WebApplication webApplication)
        {
            return webApplication.UseExceptionHandler(a => a.Run(context =>
            {
                var exception = context.Features.Get<IExceptionHandlerFeature>().Error;

                var problemDetails = new ProblemDetails
                {
                    Type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.6.1",
                    Status = (int)HttpStatusCode.InternalServerError,
                };

                if (exception is null)
                {
                    return context.Response.WriteAsJsonAsync(problemDetails);
                }

                var exceptionType = exception.GetType();

                if (exceptionType == typeof(ArgumentException) ||
                    exceptionType == typeof(ArgumentNullException) ||
                    exceptionType == typeof(ArgumentOutOfRangeException))
                {
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;

                    return context.Response.WriteAsJsonAsync(
                        new ProblemDetails
                        {
                            Title = "One or more validation errors occured.",
                            Type = "https://www.rfc-editor.org/rfc/rfc7231#section-6.5.1",
                            Status = StatusCodes.Status400BadRequest,
                            Detail = exception.Message,
                        });
                }

                if (webApplication.Environment.IsDevelopment())
                {
                    problemDetails.Title = exception.Message;
                    problemDetails.Detail = exception.StackTrace;
                }
                else
                {
                    problemDetails.Detail = exception.Message;
                }

                return context.Response.WriteAsJsonAsync(problemDetails);
            }));
        }

        public static IApplicationBuilder MigrateDatabase(this WebApplication webApplication)
        {
            using var serviceScope = webApplication.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

            var parcsDbContext = serviceScope.ServiceProvider.GetService<ParcsDbContext>();
            parcsDbContext.Database.Migrate();

            return webApplication;
        }
    }
}