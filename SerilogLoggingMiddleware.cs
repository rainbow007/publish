using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Serilog;
using Microsoft.AspNetCore.Http;

namespace ServiceApplication.Middlewares
{
    public class SerilogLoggingMiddleware
    {
        private readonly RequestDelegate _next;

        public SerilogLoggingMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            var RequestBody = await ReadRequestBody(context.Request);
            var Host = context.Request.Host;
            var Scheme = context.Request.Scheme;
            var Path = context.Request.Path;
            var Method = context.Request.Method;
            var QueryString = context.Request.QueryString;
            Log.Information("Request: {Scheme} {Host}{Path} {QueryString}", Scheme, Host, Path, QueryString);
            Log.Information("Request Method: {Method}", Method);
            Log.Information("Request Body: {request}", RequestBody); 

            //Copy a pointer to the original response body stream
            var originalBodyStream = context.Response.Body;

            //Create a new memory stream...
            using (var responseBody = new MemoryStream())
            {
                //...and use that for the temporary response body
                context.Response.Body = responseBody;

                //Continue down the Middleware pipeline, eventually returning to this class
                await _next(context);

                //Format the response from the server
                var response = await FormatResponse(context.Response);
                Log.Information("Response Body: {response}", response);

                //Copy the contents of the new memory stream (which contains the response) to the original stream, which is then returned to the client.
                await responseBody.CopyToAsync(originalBodyStream);
            }
        }

        private async Task<string> ReadRequestBody(HttpRequest request)
        {
            //This line allows us to set the reader for the request back at the beginning of its stream.
            // request.EnableRewind();
            HttpRequestRewindExtensions.EnableBuffering(request);

            var body = request.Body;
            //We now need to read the request stream. First, we create a new byte[] with the same length as the request stream...
            var buffer = new byte[Convert.ToInt32(request.ContentLength)];
            //...Then we copy the entire request stream into the new buffer.
            await request.Body.ReadAsync(buffer, 0, buffer.Length);
            //We convert the byte[] into a string using UTF8 encoding...
            string requestBody = Encoding.UTF8.GetString(buffer);
            //..and finally, assign the read body back to the request body, which is allowed because of EnableRewind()
            body.Seek(0, SeekOrigin.Begin);
            request.Body = body;

            return $"{requestBody}";
        }

        private async Task<string> FormatResponse(HttpResponse response)
        {
            //We need to read the response stream from the beginning...
            response.Body.Seek(0, SeekOrigin.Begin);

            //...and copy it into a string
            string text = await new StreamReader(response.Body).ReadToEndAsync();

            //We need to reset the reader for the response so that the client can read it.
            response.Body.Seek(0, SeekOrigin.Begin);

            //Return the string for the response, including the status code (e.g. 200, 404, 401, etc.)
            return $"{response.StatusCode}: {text}";
        }
    }
}
