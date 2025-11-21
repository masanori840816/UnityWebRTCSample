using UnityEngine;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text;

public class WebAccessor: IDisposable
{
    private HttpClient httpClient = null;
    public Action<string> OnMessage = null;
    public WebAccessor()
    {
        this.httpClient = new HttpClient();
        this.httpClient.Timeout = Timeout.InfiniteTimeSpan;
    }
    public async void ConnectAsync(string baseUrl, string userName, 
        SynchronizationContext mainContext, CancellationToken cancellationToken)
    {
        httpClient.DefaultRequestHeaders.Accept.Clear();
        httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("text/event-stream"));

        try
        {
            string url = $"{baseUrl}/sse?user={userName}";
            using (var response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
                    {
                        string line = await reader.ReadLineAsync();
                        if (string.IsNullOrEmpty(line) == false)
                        {
                            mainContext.Post(_ => {
                                OnMessage?.Invoke(line);
                            }, null);
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            Debug.Log("canceled");
        }
        catch (Exception e)
        {
            Debug.LogError($"Exception: {e.Message}");
        }
    }
    public async Task PostAsync(string baseUrl, ClientMessage message)
    {
        string jsonValue = JsonUtility.ToJson(message);
        var content = new StringContent(
            jsonValue,
            Encoding.UTF8,
            "application/json"
        );
        try
        {
            string url = $"{baseUrl}/sse/message";
            HttpResponseMessage response = await httpClient.PostAsync(url, content);

            response.EnsureSuccessStatusCode(); 
            string responseBody = await response.Content.ReadAsStringAsync();
            Debug.Log($"Status: {response.StatusCode} body: {responseBody}");
        }
        catch (HttpRequestException e)
        {
            Debug.Log($"[PostAsync]: {e.Message}");
        }
    }
    public void Dispose()
    {
        this.httpClient?.Dispose();
    }
}
