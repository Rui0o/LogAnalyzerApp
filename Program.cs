using LogAnalyzerApp.Components;
using LogAnalyzerApp.Services;
using Microsoft.AspNetCore.Http.Features;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// When running as a published standalone app, use the URL from config
// so users can simply double-click to launch without certificate issues.
// Admins can change the URL in appsettings.json (e.g. http://0.0.0.0:5000 for LAN hosting).
if (!builder.Environment.IsDevelopment())
{
    var url = builder.Configuration["StandaloneUrls"] ?? "http://localhost:5000";
    builder.WebHost.UseUrls(url);
}


builder.Services.AddScoped<LogProcessorService>();
builder.Services.AddScoped<AppStateService>();

// Singleton: survives across Blazor circuits so download tokens remain valid
// even after the circuit that generated them has already navigated away.
builder.Services.AddSingleton<DownloadFileService>();
builder.Services.AddSingleton<UploadFileService>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddHubOptions(options =>
    {
        options.MaximumReceiveMessageSize = 524288000; // 500 MB
        options.EnableDetailedErrors = true;
        options.HandshakeTimeout = TimeSpan.FromSeconds(60);
        options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

if (app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseAntiforgery();

// Streams the filtered log file directly to the browser as a file download.
// Using a temp file on disk instead of a MemoryStream avoids loading the entire
// filtered output (potentially 100s of MB) into server RAM and avoids Base64
// encoding overhead. The token is single-use; the file is deleted after serving.
// Receives raw binary body — single stream direct to disk, no multipart buffering overhead.
app.MapPost("/upload", async (HttpRequest request, UploadFileService uploadService) =>
{
    var bodySizeFeature = request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
    if (bodySizeFeature is { IsReadOnly: false })
        bodySizeFeature.MaxRequestBodySize = 500L * 1024 * 1024;

    var tempPath = Path.GetTempFileName();
    await using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 131072);
    await request.Body.CopyToAsync(fs);

    if (fs.Length == 0)
    {
        File.Delete(tempPath);
        return Results.BadRequest("No file");
    }

    return Results.Text(uploadService.Register(tempPath));
})
.DisableAntiforgery();

app.MapGet("/download/{token}", (string token, DownloadFileService downloadService) =>
{
    var filePath = downloadService.GetAndRemove(token);
    if (filePath == null || !File.Exists(filePath))
        return Results.NotFound();

    // FileOptions.DeleteOnClose: the OS deletes the file once the last handle is closed,
    // which happens after ASP.NET Core finishes sending the response stream to the client.
    var stream = new FileStream(
        filePath,
        FileMode.Open,
        FileAccess.Read,
        FileShare.Read,
        bufferSize: 81920,
        FileOptions.SequentialScan | FileOptions.DeleteOnClose);

    return Results.File(stream, "text/plain", "log_extract.txt");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Auto-open the browser when running as a standalone app (not in Development/IDE).
if (!app.Environment.IsDevelopment())
{
    app.Lifetime.ApplicationStarted.Register(() =>
    {
        // Open the first configured URL in the default browser.
        var listenUrl = app.Configuration["StandaloneUrls"] ?? "http://localhost:5000";
        // If listening on 0.0.0.0 (all interfaces), open localhost in the browser instead.
        var browserUrl = listenUrl.Replace("0.0.0.0", "localhost");
        try { Process.Start(new ProcessStartInfo(browserUrl) { UseShellExecute = true }); }
        catch { /* best-effort: user can open manually */ }
    });
}

app.Run();
