using System.Diagnostics;
using System.IO;

namespace AudioLoadRemover
{
    public class DebugOutput : IDisposable
    {
        public DebugOutput(string videoPath)
        {
            this.folderPath = Path.Combine(Path.GetTempPath(), $"{FolderPrefix}-{DateTime.Now:yyyyMMddTHHmmss}-{Path.GetFileNameWithoutExtension(videoPath)}");
            Directory.CreateDirectory(folderPath);

            this.logWriter = new StreamWriter(Path.Combine(this.folderPath, LogFileName));
        }

        public void Log(string message)
        {
            Trace.WriteLine(message);

            this.logWriter.WriteLine(message);
        }

        public string Folder
        {
            get { return this.folderPath; }
        }

        public void Dispose()
        {
            this.logWriter.Dispose();
        }

        private const string FolderPrefix = "AudioLoadRemover";
        private const string LogFileName = "audio-load-remover.log";

        private readonly string folderPath;
        private readonly TextWriter logWriter;
    }
}
