// Copyright (C) 2025, The Duplicati Team
// https://duplicati.com, hello@duplicati.com
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS
// OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using Duplicati.Library.Common.IO;
using Duplicati.Library.Main;
using NUnit.Framework;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Duplicati.UnitTest
{

    /// <summary>
    /// Tests the tools.
    /// </summary>
    public class ToolTests : BasicSetupHelper
    {

        /// <summary>
        /// Tests passing all arguments to the main method of the remote synchronization tool.
        /// </summary>
        [Test]
        public void TestMainMethodParsesArgumentsCorrectly()
        {
            string[][] testCases =
            {
                ["source", "destination", "--parse-arguments-only"],
                ["source", "destination", "--parse-arguments-only", "--dry-run"],
                ["source", "destination", "--parse-arguments-only", "--force"],
                ["source", "destination", "--parse-arguments-only", "--dry-run", "--force"],
                ["source", "destination", "--parse-arguments-only", "--verify-contents"],
                ["source", "destination", "--parse-arguments-only", "--verify-get-after-put"],
                ["source", "destination", "--parse-arguments-only", "--retry", "3"],
                ["source", "destination", "--parse-arguments-only", "--log-level", "Debug"],
                ["source", "destination", "--parse-arguments-only", "--log-level", "Information"],
                ["source", "destination", "--parse-arguments-only", "--log-level", "Profiling"],
                ["source", "destination", "--parse-arguments-only", "--log-level", "Verbose"],
                ["source", "destination", "--parse-arguments-only", "--log-file", "somefile.log"],
                ["source", "destination", "--parse-arguments-only", "--progress"],
                ["source", "destination", "--parse-arguments-only", "--retention"],
                ["source", "destination", "--parse-arguments-only", "--confirm"],
                ["source", "destination", "--parse-arguments-only", "--global-options", "someglobalkey=someglobalvalue", "anotherglobalkey=anotherglobalvalue"],
                ["source", "destination", "--parse-arguments-only", "--src-options", "somesrckey=somesrcvalue", "anothersrckey=anothersrcvalue"],
                ["source", "destination", "--parse-arguments-only", "--dst-options", "somedstkey=somedstvalue", "anotherdstkey=anotherdstvalue"],
                [
                    "source", "destination", "--parse-arguments-only",
                    "--dry-run", "--force", "--verify-contents", "--retry", "3", "--log-level", "Debug", "--log-file", "somefile.log", "--progress", "--retention", "--confirm",
                    "--global-options", "someglobalkey=someglobalvalue", "anotherglobalkey=anotherglobalvalue",
                    "--src-options", "somesrckey=somesrcvalue", "anothersrckey=anothersrcvalue",
                    "--dst-options", "somedstkey=somedstvalue", "anotherdstkey=anotherdstvalue"
                ],
            };

            foreach (var args in testCases)
            {
                int result = RemoteSynchronization.Program.Main(args).ConfigureAwait(false).GetAwaiter().GetResult();
                Assert.AreEqual(0, result, $"Failed for args: {string.Join(" ", args)}");
            }

            int failed_result = RemoteSynchronization.Program.Main(["source", "destination", "--bogus-option"]).ConfigureAwait(false).GetAwaiter().GetResult();
            Assert.AreEqual(1, failed_result, "Invalid option did not return 1");
        }

        /// <summary>
        /// Tests that the remote synchronization tool works with an empty source to an empty destination.
        /// </summary>
        [Test]
        [Category("Tools")]
        public void TestEmptySourceAndDestination()
        {
            var l1 = Path.Combine(TARGETFOLDER, "empty_src");
            var l2 = Path.Combine(TARGETFOLDER, "l2");

            Directory.CreateDirectory(l1);
            Directory.CreateDirectory(l2);

            var args = new string[] { $"file://{l1}", $"file://{l2}", "--confirm" };

            var async_call = RemoteSynchronization.Program.Main(args);
            var return_code = async_call.ConfigureAwait(false).GetAwaiter().GetResult();

            Assert.AreEqual(0, return_code, "Remote synchronization tool did not return 0.");
            Assert.IsTrue(DirectoriesAndContentsAreEqual(l1, l2), "Synchronized directories are not equal");
        }

        /// <summary>
        /// Tests the original inded use of the remote synchronization tool on an empty destination.
        /// </summary>
        [Test]
        [Category("Tools")]
        public void TestRemoteSynchronization()
        {
            var l1 = Path.Combine(TARGETFOLDER, "l1");
            var l2 = Path.Combine(TARGETFOLDER, "l2");
            var l1r = Path.Combine(RESTOREFOLDER, "l1_restore");
            var l2r = Path.Combine(RESTOREFOLDER, "l2_restore");

            var options = TestOptions;

            var now = DateTime.Now;
            GenerateTestData(DATAFOLDER, 10, 3, 3, 1024 * 1024).Wait();
            Console.WriteLine($"Generated test data in {DATAFOLDER} in {DateTime.Now - now}");

            // Create the directories if they do not exist
            foreach (var p in new string[] { l1, l2, l1r, l2r })
            {
                if (!SystemIO.IO_OS.DirectoryExists(p))
                    SystemIO.IO_OS.DirectoryCreate(p);
            }

            // Backup the first level
            using (var c = new Controller($"file://{l1}", options, null))
            {
                now = DateTime.Now;
                var results = c.Backup([DATAFOLDER]);
                Assert.AreEqual(0, results.Errors.Count());
                Assert.AreEqual(0, results.Warnings.Count());
                Console.WriteLine($"Backed up {results.AddedFiles} files to {l1} in {DateTime.Now - now}");
            }

            // Call the tool
            now = DateTime.Now;
            var exe = RemoteSynchronization.Program.Main;
            string[] args = [
                $"file://{l1}", $"file://{l2}",
                "--global-options", ..options.Select(x => $"{x.Key}={x.Value}"),
                // Pass along multi token options to test that the parser won't fail
                "--src-options", "ssh-accept-any-fingerprints=true", "ssh-keyfile=/path/to/keyfile",
                "--dst-options", "some-other-key=value", "another-key=value2",
                "--confirm"
            ];
            var async_call = exe(args);
            async_call.Wait();
            Assert.AreEqual(0, async_call.Result, "Remote synchronization tool did not return 0.");
            Console.WriteLine($"Remote synchronization tool returned 0 in {DateTime.Now - now}");

            // Verify that the directories are equal
            Assert.IsTrue(DirectoriesAndContentsAreEqual(l1, l2), "Synchronized directories are not equal");

            // Try to restore the first level
            options["restore-path"] = l1r;
            using (var c = new Controller($"file://{l1}", options, null))
            {
                now = DateTime.Now;
                var results = c.Restore([Path.Combine(DATAFOLDER, "*")]);
                Assert.AreEqual(0, results.Errors.Count());
                Assert.AreEqual(0, results.Warnings.Count());
                Console.WriteLine($"Restored {results.RestoredFiles} files to {options["restore-path"]} in {DateTime.Now - now}");
            }
            Assert.IsTrue(DirectoriesAndContentsAreEqual(DATAFOLDER, l1r), "Restored first level files is not equal to original files");

            // Try to restore the second level
            options["restore-path"] = l2r;
            using (var c = new Controller($"file://{l2}", options, null))
            {
                now = DateTime.Now;
                var results = c.Restore([]);
                Assert.AreEqual(0, results.Errors.Count());
                Assert.AreEqual(0, results.Warnings.Count());
                Console.WriteLine($"Restored {results.RestoredFiles} files to {options["restore-path"]} in {DateTime.Now - now}");
            }
            Assert.IsTrue(DirectoriesAndContentsAreEqual(DATAFOLDER, l2r), "Restored second level files is not equal to original files");
        }

        //
        // Helper methods
        //

        /// <summary>
        /// Compares two directories and their contents.
        /// </summary>
        /// <param name="d1">The first directory.</param>
        /// <param name="d2">The second directory.</param>
        /// <returns>`true` if the two directories contain exactly the same files and if the contents of the files are equivalent. `false` otherwise.</returns>
        public static bool DirectoriesAndContentsAreEqual(string d1, string d2)
        {
            // Recursively get the files in the two directories
            var f1s = Directory.EnumerateFiles(d1, "*", SearchOption.AllDirectories).Select(x => x[(d1.Length + 1)..]).OrderDescending();
            var f2s = Directory.EnumerateFiles(d2, "*", SearchOption.AllDirectories).Select(x => x[(d2.Length + 1)..]).OrderDescending();

            // If the two directories do not contain the same files, return false
            if (!f1s.SequenceEqual(f2s))
                return false;

            // Check that each file pair exist and have the same content
            foreach (var f1 in f1s)
            {
                var f1full = Path.Combine(d1, f1);
                if (!File.Exists(f1full))
                    return false;

                var f2full = Path.Combine(d2, f1);
                if (!File.Exists(f2full))
                    return false;

                var c1 = File.ReadAllText(f1full);
                var c2 = File.ReadAllText(f2full);

                if (!c1.SequenceEqual(c2))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Generates test data in the specified directory.
        /// </summary>
        /// <param name="dir">The directory to fill with the generated data.</param>
        /// <param name="n_files">How many files the directory should have.</param>
        /// <param name="n_dirs">How many subdirectories the directory should have.</param>
        /// <param name="n_levels">How deep the number of subdirectories within subdirectories should go.</param>
        /// <param name="max_file_size">The maximum size of the files to generate.</param>
        public static async Task GenerateTestData(string dir, int n_files, int n_dirs, int n_levels, int max_file_size)
        {
            if (!SystemIO.IO_OS.DirectoryExists(dir))
                SystemIO.IO_OS.DirectoryCreate(dir);

            var fs = Enumerable.Range(0, n_files)
                .Select(i => GenerateTestFile(dir, i, max_file_size));
            var ds = n_levels > 0 ?
                Enumerable.Range(0, n_dirs)
                    .Select(i =>
                    {
                        var subdir = Path.Combine(dir, $"dir_{i}");
                        return GenerateTestData(subdir, n_files, n_dirs, n_levels - 1, max_file_size);
                    })
                : [];

            await Task.WhenAll([.. fs, .. ds]);
        }

        public static async Task GenerateTestFile(string dir, int i, int max_file_size)
        {
            var rnd = new Random();
            var file = Path.Combine(dir, $"file_{i}.txt");
            var size = rnd.Next(1, max_file_size);
            var data = new byte[size];
            rnd.NextBytes(data);
            await File.WriteAllBytesAsync(file, data);
        }

    }
}