using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace NotificationFileChangeTrigger.FileServer;

internal sealed class HttpFileServer
{
    private readonly HttpClient _httpClient;

    public HttpFileServer(
        HttpClient httpClient,
        string username,
        string password,
        Uri baseAddress)
    {
        httpClient.BaseAddress = baseAddress;
        httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Basic",
                BasicAuthToken(username, password));

        _httpClient = httpClient;
    }

    public async IAsyncEnumerable<byte[]> DownloadFile(string filePath)
    {
        using var response = await _httpClient.GetStreamAsync(filePath)
            .ConfigureAwait(false);

        var read = 0;
        var bufferCount = 4096;
        using var binaryReader = new BinaryReader(response);

        do
        {
            var buffer = binaryReader.ReadBytes(bufferCount);
            read = buffer.Length;
            yield return buffer;
        } while (read == bufferCount);
    }

    public async Task DeleteResource(string name, string dirPath)
    {
        var response = await _httpClient
            .PostAsync($"{dirPath}?delete&name={name}&contextquerystring=", null)
            .ConfigureAwait(false);

        if (response.StatusCode != HttpStatusCode.Found)
        {
            var errorMessage = await response.Content
                .ReadAsStringAsync()
                .ConfigureAwait(false);

            throw new DeleteFileException(
                $"Could not delete resource. '{errorMessage}'");
        }
    }

    private static string BasicAuthToken(string username, string password)
    {
        return Convert
            .ToBase64String(ASCIIEncoding.ASCII.GetBytes($"{username}:{password}"));
    }
}
