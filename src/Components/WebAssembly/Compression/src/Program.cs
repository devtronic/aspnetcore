using System;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Components.WebAssembly.Build.BrotliCompression
{
    class Program
    {
        private const int _error = -1;

        static async Task<int> Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.Error.WriteLine("Invalid argument count. Usage: 'blazor-brotli <<source>> <<target>>'");
                return _error;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(args[1]));

            using var sourceStream = File.OpenRead(args[0]);
            using var fileStream = new FileStream(args[1], FileMode.Create);
            using var stream = new BrotliStream(fileStream, CompressionLevel.Optimal);

            await sourceStream.CopyToAsync(stream);

            return 0;
        }
    }
}
