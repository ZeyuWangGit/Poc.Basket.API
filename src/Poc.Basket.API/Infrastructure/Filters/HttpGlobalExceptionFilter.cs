namespace Poc.Basket.API.Infrastructure.Filters
{
    public class HttpGlobalExceptionFilter: IExceptionFilter
    {
        private readonly IWebHostEnvironment _webHostEnvironment;
        private readonly ILogger<HttpGlobalExceptionFilter> _logger;
        public HttpGlobalExceptionFilter(IWebHostEnvironment webHostEnvironment, ILogger<HttpGlobalExceptionFilter> logger)
        {
            _webHostEnvironment = webHostEnvironment;
            _logger = logger;
        }
        public void OnException(ExceptionContext context)
        {
            _logger.LogError(new EventId(context.Exception.HResult), context.Exception, context.Exception.Message);
            if (context.Exception.GetType() == typeof(BasketDomainException))
            {
                var jsonError = new JsonErrorResponse
                {
                    Messages = new[] {context.Exception.Message}
                };
                context.Result = new BadRequestObjectResult(jsonError);
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
            else
            {
                var jsonError = new JsonErrorResponse
                {
                    Messages = new[] { "An error occurred. Try it again." }
                };

                if (_webHostEnvironment.IsDevelopment())
                {
                    jsonError.DeveloperMessage = context.Exception;
                }
                context.Result = new InternalServerErrorObjectResult(jsonError);
                context.HttpContext.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }

            context.ExceptionHandled = true;
        }
    }
}
