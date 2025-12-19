using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Text;
using Microsoft.Playwright;
using Xunit;

namespace SD.ProjectName.TestUI.WebTest
{
    [CollectionDefinition("playwright-webapp", DisableParallelization = true)]
    public class PlaywrightWebAppCollection : ICollectionFixture<WebAppFixture>
    {
    }

    public class WebAppFixture : IAsyncLifetime
    {
        private readonly string _baseUrl;
        private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"identity-tests-{Guid.NewGuid():N}.db");
        private readonly string _connectionString;
        private readonly StringBuilder _stderr = new();
        private Process? _process;

        public WebAppFixture()
        {
            var port = GetFreePort();
            _baseUrl = $"http://localhost:{port}";
            _connectionString = $"Data Source={_databasePath};Cache=Shared";
        }

        public string BaseUrl => _baseUrl;
        public string DatabasePath => _databasePath;
        public string ConnectionString => _connectionString;

        public async Task InitializeAsync()
        {
            Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });

            if (File.Exists(_databasePath))
            {
                File.Delete(_databasePath);
            }

            var projectPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Application/SD.ProjectName.WebApp"));
            var startInfo = new ProcessStartInfo("dotnet", $"run --no-build --urls {_baseUrl}")
            {
                WorkingDirectory = projectPath,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
            startInfo.Environment["DisableHttpsRedirection"] = "true";
            startInfo.Environment["DisableMigrations"] = "true";
            startInfo.Environment["ConnectionStrings__SqliteConnection"] = _connectionString;
            startInfo.Environment["UseFakeExternalAuth"] = "true";

            _process = Process.Start(startInfo);
            if (_process != null)
            {
                _process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        Console.WriteLine($"[webapp-out] {e.Data}");
                    }
                };
                _process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        _stderr.AppendLine(e.Data);
                        Console.WriteLine($"[webapp-err] {e.Data}");
                    }
                };
                _process.BeginOutputReadLine();
                _process.BeginErrorReadLine();
            }

            await WaitForReady();
        }

        public async Task DisposeAsync()
        {
            if (_process != null && !_process.HasExited)
            {
                _process.Kill(true);
                await _process.WaitForExitAsync();
            }
        }

        private async Task WaitForReady()
        {
            using var client = new HttpClient();
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow - start < TimeSpan.FromSeconds(120))
            {
                if (_process?.HasExited == true)
                {
                    var errors = _stderr.ToString();
                    throw new InvalidOperationException(string.IsNullOrWhiteSpace(errors)
                        ? "Application exited early."
                        : $"Application exited early. Errors: {errors}");
                }

                try
                {
                    var response = await client.GetAsync($"{_baseUrl}/");
                    if (response.StatusCode is HttpStatusCode.OK or HttpStatusCode.Redirect)
                    {
                        return;
                    }
                }
                catch
                {
                    // ignored until server is ready
                }

                await Task.Delay(500);
            }

            throw new TimeoutException("Web application did not start in time for Playwright tests.");
        }

        private static int GetFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
