using NUnit.Framework;
using NUnit.Framework.Internal;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using WixToolset.Dtf.WindowsInstaller;

namespace Slido.BuildTasks.Tests
{
    public class Tests
    {
        private readonly string OriginalMsiFilename = "database.msi";
        private readonly string TestDatabaseFileName = "test_database.msi";
        private readonly string TestTargetsFile = "test.targets";
        private string TestDirectory;
        private string TestMsiPath;
        private string OriginalMsiPath;

        private readonly string ExpectedSerilogVersion = "4.3.0.1";
        private readonly string ExpectedSystemTextJsonVersion = "10.0.0.0";

        [SetUp]
        public void Setup()
        {
            TestDirectory = TestContext.CurrentContext.TestDirectory;
            OriginalMsiPath = Path.Combine(TestDirectory, OriginalMsiFilename);
            TestMsiPath = Path.Combine(TestDirectory, TestDatabaseFileName);

            // duplicate database.msi to keep the original file intact
            try
            {
                if (!File.Exists(OriginalMsiPath))
                {
                    Assert.Fail($"Original database.msi not found at : {OriginalMsiPath}");
                    return;
                }
                File.Copy(OriginalMsiPath, TestMsiPath, true);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to duplicate database: {ex.Message}");
            }
        }

        [TearDown]
        public void Cleanup()
        {
            // Clean up test files
            if (File.Exists(TestMsiPath))
            {
                try
                {
                    File.Delete(TestMsiPath);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        [Test]
        public void AlwaysPass()
        {
            Assert.Pass();
        }

        [Test]
        public async Task ChangeFileVersion_UpdatesVersionInTestMsi()
        {
            // Arrange 
            var originalSerilogVersion = GetFileVersionFromMsi("Serilog.dll");
            var originalSystemTextJsonVersion = GetFileVersionFromMsi("System.Text.Json.dll");

            // Act
            var result = await RunMSBuild();

            // Assert
            Assert.That(result.ExitCode, Is.EqualTo(0));

            var newSerilogVersion = GetFileVersionFromMsi("Serilog.dll");
            var newSystemTextJsonVersion = GetFileVersionFromMsi("System.Text.Json.dll");

            // versions are from test.targets
            Assert.That(newSerilogVersion, Is.EqualTo(ExpectedSerilogVersion));
            Assert.That(newSystemTextJsonVersion, Is.EqualTo(ExpectedSystemTextJsonVersion));

        }

        private string GetFileVersionFromMsi(string fileName)
        {
            try
            {
                using var database = new Database(TestMsiPath, DatabaseOpenMode.ReadOnly);
                using var view = database.OpenView("SELECT `File`, `Version` FROM `File`") ;

                view.Execute();

                Record result;
                while ((result = view.Fetch()) != null)
                {
                    using (result)
                    {
                        var fileId = result[1]?.ToString();
                        var version = result[2]?.ToString();

                        if (fileId?.Equals(fileName, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            return version;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading version for {fileName}: {ex.Message}");
            }

            return null;
        }

        private Task<MSBuildResult> RunMSBuild()
        {
            var tcs = new TaskCompletionSource<MSBuildResult>();
            var processInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = $"msbuild {TestTargetsFile} " +
                    $"/t:ChangeFileVersion " +
                    $"/p:MsiPath={TestDatabaseFileName} " +
                    $"/p:NewSerilogVersion={ExpectedSerilogVersion} " +
                    $"/p:NewSystemTextJsonVersion={ExpectedSystemTextJsonVersion}",
                WorkingDirectory = TestDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var output = new StringBuilder();
            var errors = new StringBuilder();

            try
            {
                using var process = Process.Start(processInfo);
                process.EnableRaisingEvents = true;

                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                        Console.WriteLine($"MSBUILD: {e.Data}");
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        errors.AppendLine(e.Data);
                        Console.WriteLine($"MSBUILD ERROR: {e.Data}");
                    }
                };

                process.Exited += (sender, e) =>
                {
                    var result = new MSBuildResult
                    {
                        ExitCode = process.ExitCode,
                        Output = output.ToString() + Environment.NewLine + errors.ToString()
                    };
                    tcs.SetResult(result);
                    process.Dispose();
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                return tcs.Task;
            }
            catch (Exception ex)
            {
                tcs.TrySetResult(
                     new MSBuildResult
                     {
                         ExitCode = -1,
                         Output = $"Failed to run MSBuild: {ex.Message}"
                     }
                );
                return tcs.Task;
            }
        }

        public class MSBuildResult
        {
            public int ExitCode { get; set; }
            public string Output { get; set; }
        }
    }
}
