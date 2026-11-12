using System.Net;
using System.Text;

namespace ModuleMap.Preview;

public sealed class PreviewServer
{
    private const string ReloadScript = """
        <script>
        (() => {
          const version = "__VERSION__";
          const key = "module-map-scroll";
          try {
            const saved = sessionStorage.getItem(key);
            if(saved !== null) {
              scrollTo(0, Number(saved));
              sessionStorage.removeItem(key);
            }
          } catch(e) {}
          setInterval(async () => {
            try {
              const current = await (await fetch("/version", { cache: "no-store" })).text();
              if(current !== version) {
                try { sessionStorage.setItem(key, String(scrollY)); } catch(e) {}
                location.reload();
              }
            } catch(e) {}
          }, 1000);
        })();
        </script>
        """;
    private readonly HttpListener listener;
    private readonly Lock pageLock = new Lock();
    private string page = "";
    private int version;

    public PreviewServer(HttpListener listener)
    {
        this.listener = listener;
    }

    public void Publish(string html)
    {
        lock(pageLock)
        {
            version++;
            page = html + ReloadScript.Replace("__VERSION__", version.ToString());
        }
    }

    public async Task ServeAsync(CancellationToken token)
    {
        listener.Start();
        await using var registration = token.Register(listener.Stop);
        while(!token.IsCancellationRequested)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch(Exception exception) when(exception is HttpListenerException || exception is ObjectDisposedException)
            {
                break;
            }

            await RespondAsync(context);
        }
    }

    private async Task RespondAsync(HttpListenerContext context)
    {
        string body;
        string contentType;
        lock(pageLock)
        {
            bool isVersion = context.Request.Url?.AbsolutePath == "/version";
            body = isVersion ? version.ToString() : page;
            contentType = isVersion ? "text/plain; charset=utf-8" : "text/html; charset=utf-8";
        }

        byte[] bytes = Encoding.UTF8.GetBytes(body);
        context.Response.ContentType = contentType;
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.ContentLength64 = bytes.Length;
        try
        {
            await context.Response.OutputStream.WriteAsync(bytes);
        }
        catch(HttpListenerException)
        {
        }
        finally
        {
            context.Response.Close();
        }
    }
}
