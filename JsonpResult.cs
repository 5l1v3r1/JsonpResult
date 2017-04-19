
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Formatters.Json.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Internal;
using System.Buffers;


namespace SmartMap.NetPlatform.Core.Common
{
    public class JsonpResult : JsonResult
    {
        public JsonpResult(object value,string callbackName):base(value)
        {
            CallbackName = callbackName;           
        }

        public JsonpResult(object value) : this(value,"jsoncallback")
        {
        }

        public string CallbackName { get; set; }

        public override Task ExecuteResultAsync(ActionContext context)
        {
            var services = context.HttpContext.RequestServices;
            var executor = services.GetRequiredService<JsonpResultExecutor>();
            return executor.ExecuteAsync(context, this, CallbackName);
        }
    }

    public static class ControllerExtensions
    {
        public static JsonpResult Jsonp(this Controller controller, object data, string callbackName = "callback")
        {
            return new JsonpResult(data, callbackName);       
        }

        //public static T DeserializeObject<T>(this Controller controller, string key) where T : class
        //{
        //    var value = controller.HttpContext.Request.Query[key];
        //    if (string.IsNullOrEmpty(value))
        //    {
        //        return null;
        //    }
        //    JsonSerializer javaScriptSerializer = new JsonSerializer();
           
        //    return javaScriptSerializer.Deserialize<T>(value);
        //}
    }

    /// <summary>
    /// Executes a <see cref="JsonResult"/> to write to the response.
    /// </summary>
    public class JsonpResultExecutor
    {
        private static readonly string DefaultContentType = new MediaTypeHeaderValue("application/x-javascript")
        {
            Encoding = Encoding.UTF8
        }.ToString();

        private readonly IArrayPool<char> _charPool;

        /// <summary>
        /// Creates a new <see cref="JsonResultExecutor"/>.
        /// </summary>
        /// <param name="writerFactory">The <see cref="IHttpResponseStreamWriterFactory"/>.</param>
        /// <param name="logger">The <see cref="ILogger{JsonResultExecutor}"/>.</param>
        /// <param name="options">The <see cref="IOptions{MvcJsonOptions}"/>.</param>
        /// <param name="charPool">The <see cref="ArrayPool{Char}"/> for creating <see cref="T:char[]"/> buffers.</param>
        public JsonpResultExecutor(
            IHttpResponseStreamWriterFactory writerFactory,
            ILogger<JsonResultExecutor> logger,
            IOptions<MvcJsonOptions> options,
            ArrayPool<char> charPool)
        {
            if (writerFactory == null)
            {
                throw new ArgumentNullException(nameof(writerFactory));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            if (charPool == null)
            {
                throw new ArgumentNullException(nameof(charPool));
            }

            WriterFactory = writerFactory;
            Logger = logger;
            Options = options.Value;
            _charPool = new JsonArrayPool<char>(charPool);
        }

        /// <summary>
        /// Gets the <see cref="ILogger"/>.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Gets the <see cref="MvcJsonOptions"/>.
        /// </summary>
        protected MvcJsonOptions Options { get; }

        /// <summary>
        /// Gets the <see cref="IHttpResponseStreamWriterFactory"/>.
        /// </summary>
        protected IHttpResponseStreamWriterFactory WriterFactory { get; }

        /// <summary>
        /// Executes the <see cref="JsonResult"/> and writes the response.
        /// </summary>
        /// <param name="context">The <see cref="ActionContext"/>.</param>
        /// <param name="result">The <see cref="JsonResult"/>.</param>
        /// <returns>A <see cref="Task"/> which will complete when writing has completed.</returns>
        public Task ExecuteAsync(ActionContext context, JsonResult result,string callbackName)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (result == null)
            {
                throw new ArgumentNullException(nameof(result));
            }
            string CallbackName = callbackName;
            var response = context.HttpContext.Response;
            var request = context.HttpContext.Request;
            string jsoncallback = ((context.RouteData.Values[CallbackName] as string) ?? request.Query[CallbackName]) ?? CallbackName;

            string resolvedContentType = null;
            Encoding resolvedContentTypeEncoding = null;
            ResponseContentTypeHelper.ResolveContentTypeAndEncoding(
                result.ContentType,
                response.ContentType,
                DefaultContentType,
                out resolvedContentType,
                out resolvedContentTypeEncoding);

            response.ContentType = resolvedContentType;

            if (result.StatusCode != null)
            {
                response.StatusCode = result.StatusCode.Value;
            }

            var serializerSettings = result.SerializerSettings ?? Options.SerializerSettings;

            //Logger.JsonResultExecuting(result.Value);
            using (var writer = WriterFactory.CreateWriter(response.Body, resolvedContentTypeEncoding))
            {
                writer.Write(string.Format("{0}(", jsoncallback));
                using (var jsonWriter = new JsonTextWriter(writer))
                {
                    jsonWriter.ArrayPool = _charPool;
                    jsonWriter.CloseOutput = false;
       
                    var jsonSerializer = JsonSerializer.Create(serializerSettings);
                    jsonSerializer.Serialize(jsonWriter, result.Value);
                }
                writer.Write(")");
            }

            return TaskCache.CompletedTask;
        }
    }

}
