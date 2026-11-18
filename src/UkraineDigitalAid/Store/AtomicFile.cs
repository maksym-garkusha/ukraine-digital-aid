using System.Text;

namespace UkraineDigitalAid.Store;

/// <summary>
/// Запис через тимчасовий файл поруч і перейменування: перерваний запуск лишає
/// попередню версію файлу, а не обрізану нову.
/// </summary>
public static class AtomicFile
{
    public static async Task WriteAsync(string path, Func<Stream, Task> write)
    {
        string temp = path + ".tmp";
        try
        {
            await using (var file = File.Create(temp))
                await write(file);
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            File.Delete(temp);
        }
    }

    public static Task WriteTextAsync(string path, string text, CancellationToken ct) =>
        WriteAsync(path, file => file.WriteAsync(Encoding.UTF8.GetBytes(text), ct).AsTask());
}
