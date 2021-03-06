using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Ocelot.Errors;
using Ocelot.Infrastructure.RequestData;
using Ocelot.Logging;
using Ocelot.Middleware;

namespace Ocelot.Responder.Middleware
{
    public class ResponderMiddleware : OcelotMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IHttpResponder _responder;
        private readonly IErrorsToHttpStatusCodeMapper _codeMapper;
        private readonly IOcelotLogger _logger;

        public ResponderMiddleware(RequestDelegate next, 
            IHttpResponder responder,
            IOcelotLoggerFactory loggerFactory,
            IRequestScopedDataRepository requestScopedDataRepository, 
            IErrorsToHttpStatusCodeMapper codeMapper)
            :base(requestScopedDataRepository)
        {
            _next = next;
            _responder = responder;
            _codeMapper = codeMapper;
            _logger = loggerFactory.CreateLogger<ResponderMiddleware>();

        }

        public async Task Invoke(HttpContext context)
        {
            _logger.LogDebug("started error responder middleware");

            await _next.Invoke(context);

            _logger.LogDebug("calling next middleware");

            if (PipelineError)
            {
                _logger.LogDebug("there is a pipeline error, getting errors");

                var errors = PipelineErrors;

                _logger.LogDebug("received errors setting error response");

                await SetErrorResponse(context, errors);
            }
            else
            {
                _logger.LogDebug("no pipeline error, setting response");

                var setResponse = await _responder.SetResponseOnHttpContext(context, HttpResponseMessage);

                if (setResponse.IsError)
                {
                    _logger.LogDebug("error setting response, returning error to client");

                    await SetErrorResponse(context, setResponse.Errors);
                }
            }
        }

        private async Task SetErrorResponse(HttpContext context, List<Error> errors)
        {
            var statusCode = _codeMapper.Map(errors);

            if (!statusCode.IsError)
            {
                await _responder.SetErrorResponseOnContext(context, statusCode.Data);
            }
            else
            {
                await _responder.SetErrorResponseOnContext(context, 500);
            }
        }
    }
}