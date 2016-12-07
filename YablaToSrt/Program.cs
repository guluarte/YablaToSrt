using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Linq;

namespace YablaToSrt
{
    class Program
    {
        static void Main(string[] args)
        {
            var htmlDir = ConfigurationManager.AppSettings["HtmlDir"];
            var mediaDir = ConfigurationManager.AppSettings["MediaDir"];

            Console.WriteLine($"HmlDir: {htmlDir}");
            Console.WriteLine($"MediaDir: {mediaDir}");

            var trasncriptPath = $"{mediaDir}Transcript\\";
            var translationPath = $"{mediaDir}Translation\\";

            Directory.CreateDirectory(trasncriptPath);
            Directory.CreateDirectory(translationPath);
            
            var htmlFiles = Directory.GetFiles(htmlDir, "*.*", SearchOption.AllDirectories)
                .Where(f => f.ToLower().EndsWith(".html") || f.ToLower().EndsWith(".htm"))
                .ToList();

            var failedFiles = new List<FailedFile>();

            foreach (var htmlFile in htmlFiles)
            {
                try
                {
                    Console.WriteLine("Procesing {0}", htmlFile);

                    var html = File.ReadAllText(htmlFile);

                    var captionsPattern = new Regex(@"var CAPTIONS = \[(.*?)\];", RegexOptions.Multiline);
                    var mediaIdPattern = new Regex(@"var MEDIA_ID = (.*?);");
                    var fileNamePattern = new Regex(@"var FILE_NAME = '(.*?)';");

                    Match captionsMatch = captionsPattern.Match(html);
                    Match mediIdMatch = mediaIdPattern.Match(html);
                    Match fileNameMatch = fileNamePattern.Match(html);

                    var captionsJson = JArray.Parse($"[{captionsMatch.Groups[1].Value}]");
                    var mediaId = mediIdMatch.Groups[1].Value;
                    var oldVideoFileName = $"{mediaId}_{fileNameMatch.Groups[1].Value}";

                    var newVideoFileName = oldVideoFileName;

                    var newVideoFileNameWithoutExt = Path.GetFileNameWithoutExtension(newVideoFileName);

                    // cehck if video exitx
                    if (!File.Exists($"{mediaDir}{oldVideoFileName}"))
                    {
                        failedFiles.Add(new FailedFile
                        {
                            FileName = htmlFile,
                            Error = "Video does not exists"
                        });
                        continue;
                    }

                    using (
                        var subWriterTranscript =
                            new SubtitleCreator(File.CreateText($"{trasncriptPath}{newVideoFileNameWithoutExt}.srt")))
                    using (
                        var subWriterTranscriptLocal =
                            new SubtitleCreator(File.CreateText($"{mediaDir}{newVideoFileNameWithoutExt}.fr.srt")))
                    using (
                        var subWriterTranslation =
                            new SubtitleCreator(File.CreateText($"{translationPath}{newVideoFileNameWithoutExt}.srt")))
                    using (
                        var subWriterTranslationLocal =
                            new SubtitleCreator(File.CreateText($"{mediaDir}{newVideoFileNameWithoutExt}.en.srt")))
                    {
                        foreach (var caption in captionsJson)
                        {
                            //transcript, translation, time_in, time_out

                            var transcript = (string)caption["transcript"];
                            var translation = (string)caption["translation"];
                            var timeIn = (double)caption["time_in"];
                            var timeOut = (double)caption["time_out"];

                            subWriterTranscript.AddSubtitle(timeIn, timeOut, transcript);
                            subWriterTranscriptLocal.AddSubtitle(timeIn, timeOut, transcript);

                            subWriterTranslation.AddSubtitle(timeIn, timeOut, translation);
                            subWriterTranslationLocal.AddSubtitle(timeIn, timeOut, translation);


                        }
                    }

                }
                catch (Exception ex)
                {
                    failedFiles.Add(new FailedFile
                    {
                        FileName = htmlFile,
                        Error = ex.Message
                    });
                }

            }
            if (failedFiles.Any())
            {
                Console.WriteLine("Files with error:");
                foreach (var failedFile in failedFiles)
                {
                    Console.WriteLine("File: {0}", failedFile.FileName);
                    Console.WriteLine("Error: {0}", failedFile.Error);
                }
            }

            Console.WriteLine("Done");
            Console.ReadLine();

        }

        public class FailedFile
        {
            public string FileName { get; set; }
            public string Error { get; set; }
        }

        public class SubtitleCreator : IDisposable
        {
            private readonly StreamWriter _writer;
            private int _num;

            public SubtitleCreator(StreamWriter writer)
            {
                _writer = writer;
                _num = 0;
            }

            public void AddSubtitle(double timeIn, double timeOut, string text)
            {
                _num++;
                var timespanIn = TimeSpan.FromSeconds(timeIn);
                var timespanOut = TimeSpan.FromSeconds(timeOut);

                _writer.WriteLine(_num);
                _writer.WriteLine("{0} --> {1}", getTimeSpanString(timespanIn), getTimeSpanString(timespanOut));
                _writer.WriteLine("{0}", text);
                _writer.WriteLine(Environment.NewLine);
            }

            private string getTimeSpanString(TimeSpan timeSpan)
            {
                return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00},{timeSpan.Milliseconds:000}";
            }

            public void Dispose()
            {
                _writer?.Dispose();
            }
        }
    }
}