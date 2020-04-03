using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Components.WebAssembly.Build.BrotliCompression
{
    class Program
    {
        private const int _error = -1;

        static async Task<int> Main(string[] args)
        {
            if (args.Length != 1)
            {
                Console.Error.WriteLine("Invalid argument count. Usage: 'blazor-brotli <<path-to-manifest>>'");
                return _error;
            }

            var manifestPath = args[0];
            if (!File.Exists(manifestPath))
            {
                Console.Error.WriteLine($"Manifest '{manifestPath}' does not exist.");
                return -1;
            }

            using var manifestStream = File.OpenRead(manifestPath);

            var manifest = await JsonSerializer.DeserializeAsync<ManifestData>(manifestStream);
            var result = 0;
            Parallel.ForEach(manifest.FilesToCompress, async (file) =>
            {
                var inputPath = file.Source;
                var inputSource = file.InputSource;
                var targetCompressionPath = file.Target;

                if (!File.Exists(inputSource) ||
                    (File.Exists(targetCompressionPath) && File.GetLastWriteTime(inputSource) > File.GetLastWriteTime(targetCompressionPath)))
                {
                    // Incrementalism. If input source doesn't exist or it exists and is not newer than the expected output, do nothing.
                    if (!File.Exists(inputSource))
                    {
                        Console.WriteLine($"Skipping '{inputPath}' because '{inputSource}' does not exist.");
                    }
                    else
                    {
                        Console.WriteLine($"Skipping '{inputPath}' because '{inputSource}' is newer than '{targetCompressionPath}'.");
                    }
                    return;
                }

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetCompressionPath));

                    using var sourceStream = File.OpenRead(inputPath);
                    using var fileStream = new FileStream(targetCompressionPath, FileMode.Create);
                    using var stream = new BrotliStream(fileStream, CompressionLevel.Optimal);

                    await sourceStream.CopyToAsync(stream);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine(e);
                    result = -1;
                }
            });

            return result;
        }

        private class ManifestData
        {
            public CompressedFile[] FilesToCompress { get; set; }
        }

        private class CompressedFile
        {
            public string Source { get; set; }

            public string InputSource { get; set; }

            public string Target { get; set; }
        }
    }
}
