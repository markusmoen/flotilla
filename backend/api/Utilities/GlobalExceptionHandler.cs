using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace Api.Utilities
{
    /// <summary>
    /// Converts exceptions that escape a controller action into a ProblemDetails response.
    /// </summary>
    public class GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment
    ) : IExceptionHandler
    {
        private static int MapToStatusCode(Exception exception) =>
            exception switch
            {
                InstallationNotFoundException => StatusCodes.Status404NotFound,
                PlantNotFoundException => StatusCodes.Status404NotFound,
                MissionNotFoundException => StatusCodes.Status404NotFound,
                MissionRunNotFoundException => StatusCodes.Status404NotFound,
                MissionTaskNotFoundException => StatusCodes.Status404NotFound,
                InspectionNotFoundException => StatusCodes.Status404NotFound,
                RobotNotFoundException => StatusCodes.Status404NotFound,

                InspectionAreaExistsException => StatusCodes.Status409Conflict,
                ExclusionAreaExistsException => StatusCodes.Status409Conflict,
                RobotBusyException => StatusCodes.Status409Conflict,
                RobotNotInSameInstallationAsMissionException => StatusCodes.Status409Conflict,

                InvalidPolygonException => StatusCodes.Status400BadRequest,
                MultipleInspectionAreasFoundException => StatusCodes.Status400BadRequest,
                RobotPreCheckFailedException => StatusCodes.Status400BadRequest,
                UnsupportedRobotCapabilityException => StatusCodes.Status400BadRequest,
                InvalidDataException => StatusCodes.Status400BadRequest,

                UnauthorizedAccessException => StatusCodes.Status403Forbidden,

                _ => StatusCodes.Status500InternalServerError,
            };

        public async ValueTask<bool> TryHandleAsync(
            HttpContext httpContext,
            Exception exception,
            CancellationToken cancellationToken
        )
        {
            var statusCode = MapToStatusCode(exception);

            if (statusCode >= StatusCodes.Status500InternalServerError)
            {
                logger.LogError(
                    exception,
                    "Unhandled exception during {Method} {Path}",
                    httpContext.Request.Method,
                    httpContext.Request.Path
                );
            }
            else
            {
                logger.LogWarning(
                    exception,
                    "Request rejected with {StatusCode} during {Method} {Path}",
                    statusCode,
                    httpContext.Request.Method,
                    httpContext.Request.Path
                );
            }

            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = ReasonPhrases.GetReasonPhrase(statusCode),
                Detail =
                    statusCode < StatusCodes.Status500InternalServerError
                        ? exception.Message
                        : null,
                Instance = httpContext.Request.Path,
            };
            problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

            if (environment.IsDevelopment())
            {
                problemDetails.Extensions["exception"] = exception.ToString();
            }

            httpContext.Response.StatusCode = statusCode;
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken: cancellationToken
            );
            return true;
        }
    }
}
