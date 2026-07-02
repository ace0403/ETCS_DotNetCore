using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using ETCS.Pos.Bridge.Models;
using ETCS.Pos.Bridge.Services;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace ETCS.Pos.Bridge.Http;

public sealed class BridgeHttpServer : IDisposable
{
    private const string Prefix = "http://127.0.0.1:5050/";
    private static readonly JsonSerializerSettings JsonSettings = new()
    {
        ContractResolver = new CamelCasePropertyNamesContractResolver()
    };
    private readonly HttpListener _listener = new();
    private readonly IbonusSoapService _ibonus = new();
    private readonly ReceiptPrintService _printer = new();
    private CancellationTokenSource? _cts;
    private Thread? _worker;

    public void Start()
    {
        if (_listener.IsListening)
        {
            return;
        }

        _listener.Prefixes.Add(Prefix);
        _listener.Start();
        _cts = new CancellationTokenSource();
        _worker = new Thread(ListenLoop) { IsBackground = true, Name = "ETCSPosBridgeHttp" };
        _worker.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        if (_listener.IsListening)
        {
            _listener.Stop();
        }
        _worker?.Join(TimeSpan.FromSeconds(3));
        _cts?.Dispose();
        _cts = null;
        _worker = null;
    }

    private void ListenLoop()
    {
        while (_cts is { IsCancellationRequested: false })
        {
            HttpListenerContext? context = null;
            try
            {
                context = _listener.GetContext();
                HandleRequest(context);
            }
            catch (HttpListenerException) when (_cts?.IsCancellationRequested == true)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                if (context?.Response is { } response)
                {
                    WriteJson(response, HttpStatusCode.InternalServerError, new { message = ex.Message });
                }
            }
        }
    }

    private void HandleRequest(HttpListenerContext context)
    {
        var request = context.Request;
        var response = context.Response;
        response.Headers.Add("Access-Control-Allow-Origin", "*");
        response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
        response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

        if (string.Equals(request.HttpMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
        {
            response.StatusCode = (int)HttpStatusCode.NoContent;
            response.Close();
            return;
        }

        var path = request.Url?.AbsolutePath?.TrimEnd('/') ?? string.Empty;

        if (string.Equals(path, "/health", StringComparison.OrdinalIgnoreCase))
        {
            WriteJson(response, HttpStatusCode.OK, new HealthResponse
            {
                LocalIp = GetLocalIp()
            });
            return;
        }

        if (string.Equals(path, "/ibonus/connect-test", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            var terminalIp = request.QueryString["terminalIp"] ?? string.Empty;
            var result = _ibonus.TestConnection(terminalIp);
            WriteJson(response, result.IsReachable ? HttpStatusCode.OK : HttpStatusCode.BadRequest, result);
            return;
        }

        if (string.Equals(path, "/ibonus/purchase", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var body = ReadBody(request);
            var model = JsonConvert.DeserializeObject<IbonusPurchaseRequest>(body) ?? new IbonusPurchaseRequest();
            var result = _ibonus.Purchase(model);
            WriteJson(response, result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest, result);
            return;
        }

        if (string.Equals(path, "/ibonus/undo", StringComparison.OrdinalIgnoreCase)
            && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var body = ReadBody(request);
            var model = JsonConvert.DeserializeObject<IbonusUndoRequest>(body) ?? new IbonusUndoRequest();
            var result = _ibonus.Undo(model);
            WriteJson(response, result.IsSuccess ? HttpStatusCode.OK : HttpStatusCode.BadRequest, result);
            return;
        }

        if ((string.Equals(path, "/print/receipt", StringComparison.OrdinalIgnoreCase)
             || string.Equals(path, "/print/undo-receipt", StringComparison.OrdinalIgnoreCase))
            && string.Equals(request.HttpMethod, "POST", StringComparison.OrdinalIgnoreCase))
        {
            var body = ReadBody(request);
            var model = JsonConvert.DeserializeObject<ReceiptPrintRequest>(body) ?? new ReceiptPrintRequest();
            if (string.Equals(path, "/print/undo-receipt", StringComparison.OrdinalIgnoreCase))
            {
                model.IsUndo = true;
            }

            try
            {
                _printer.Print(model);
                WriteJson(response, HttpStatusCode.OK, new { isSuccess = true, message = "Receipt sent to printer." });
            }
            catch (Exception ex)
            {
                WriteJson(response, HttpStatusCode.BadRequest, new { isSuccess = false, message = ex.Message });
            }

            return;
        }

        WriteJson(response, HttpStatusCode.NotFound, new { message = "Not found." });
    }

    private static string ReadBody(HttpListenerRequest request)
    {
        using var reader = new StreamReader(request.InputStream, request.ContentEncoding ?? Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteJson(HttpListenerResponse response, HttpStatusCode status, object payload)
    {
        var json = JsonConvert.SerializeObject(payload, JsonSettings);
        var buffer = Encoding.UTF8.GetBytes(json);
        response.StatusCode = (int)status;
        response.ContentType = "application/json";
        response.ContentEncoding = Encoding.UTF8;
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    private static string GetLocalIp()
    {
        try
        {
            var host = Dns.GetHostName();
            var entry = Dns.GetHostEntry(host);
            foreach (var address in entry.AddressList)
            {
                if (address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    return address.ToString();
                }
            }
        }
        catch
        {
            // ignored
        }

        return "127.0.0.1";
    }

    public void Dispose()
    {
        Stop();
        _listener.Close();
    }
}
