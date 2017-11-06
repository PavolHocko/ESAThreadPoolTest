using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ESAThreadPoolTest
{
    internal class Program
    {
        private const string DataFileName = "data.txt";
        private const string DllFileName = "transform.dll";
        private const string OutputFileName = "output.txt";
        private const string ResultsFileName = "results.txt";
        private const string TimeSpanFormat = @"mm\:ss\:fffffff";

        private static int _threadCountFinal;
        private static int _currentprocessesCount;
        private static int _currentThreadsCount;
        private static int _maxRealThreadCount;
        private static ushort _minThreadCount;
        private static int _minThreads, _minIOCP, _maxThreads, _maxIOCP;
        private static int _processorCount;
        private static ushort _technique;
        private static string _techniqueName;

        [DllImport(DllFileName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint Transform(uint input);

        /// <summary>
        /// ESAThreadPoolTest is console c# program to test different minimum number of threads System.Threading.ThreadPool creates.
        /// It saves process results (technique name, processor count, current processes count, current thread count, set minimum threads, real maximum thread count and processing time) to results.txt
        /// Run this test with added Windows Batch Script (runMe.cmd), it will run program multiple times with different minimum number of threads.
        /// </summary>
        /// <param name="args">It takes two arguments, first is number of minimum threads, second is technique used to process all data (1 - Task.Factory.StartNew, 2 - Parallel.ForEach)</param>
        private static void Main(string[] args)
        {
            try
            {
                if (CheckFiles())
                {
                    var start = DateTime.Now;

                    if (args.Length < 2 || (!ushort.TryParse(args[0], out _minThreadCount) && _minThreadCount == 0) || !ushort.TryParse(args[1], out _technique))
                    {
                        Console.WriteLine("Wrong input exiting program...");
                        CloseApp();
                    }

                    Console.Clear();

                    Console.WriteLine($"### Overall Start Time: {start.ToLongTimeString()}");
                    Console.WriteLine();
                    _processorCount = Environment.ProcessorCount;
                    _currentprocessesCount = Process.GetProcesses().Count();
                    _currentThreadsCount = Process.GetProcesses().Sum(s => s.Threads.Count);

                    Console.WriteLine($"### Processor cores on this enviroment: {_processorCount}");
                    Console.WriteLine($"### Current processes running: {_currentprocessesCount}");
                    Console.WriteLine($"### Current threads running: {_currentThreadsCount}");

                    ThreadPool.GetMinThreads(out _minThreads, out _minIOCP);
                    ThreadPool.GetMaxThreads(out _maxThreads, out _maxIOCP);
                    Console.WriteLine($"### Current {nameof(ThreadPool)} settings: Threads: {_minThreads} min, {_maxThreads} max");

                    if (_minThreadCount > _maxThreads)
                    {
                        Console.WriteLine($"### Setting number of maximum threads to {_minThreadCount + 20}");
                        ThreadPool.SetMaxThreads(_minThreadCount + 20, _maxIOCP);
                    }
                    Console.WriteLine($"### Setting number of minimum threads to {_minThreadCount}");

                    ThreadPool.SetMinThreads(_minThreadCount, _minIOCP);

                    ThreadPool.GetMinThreads(out _minThreads, out _minIOCP);
                    ThreadPool.GetMaxThreads(out _maxThreads, out _maxIOCP);

                    Console.WriteLine($"### Changed {nameof(ThreadPool)} settings: Threads: {_minThreads} min, {_maxThreads} max");

                    if (_technique == 2)
                    {
                        _techniqueName = "Parallel.ForEach";
                    }
                    else
                    {
                        _technique = 1;
                        _techniqueName = "Task.Factory";
                    }

                    Console.WriteLine();
                    Console.WriteLine($"### Starting process file {DataFileName} with Technique: {_techniqueName}");
                    Console.WriteLine();

                    ProcessFile();

                    var end = DateTime.Now;
                    Console.WriteLine();
                    Console.WriteLine($"### Overall Run Time: {(end - start).ToString(TimeSpanFormat)}");
                    Console.WriteLine($"### You can find output in {OutputFileName}");
                }
                else
                {
                    CloseApp();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Thread.Sleep(1000);
            //CloseApp();
        }

        private static void CloseApp()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine("Press Enter to Exit");
            Console.ReadLine();
            Environment.Exit(0);
        }

        /// <summary>
        /// Check if data.txt and transform.dll are present in the same directory as ESA.exe
        /// </summary>
        /// <returns>True if both of files are present otherwise false</returns>
        private static bool CheckFiles()
        {
            if (!File.Exists(DataFileName))
            {
                Console.WriteLine($"File {DataFileName} doesn't exist");
                return false;
            }

            if (!File.Exists(DllFileName))
            {
                Console.WriteLine($"File {DllFileName} doesn't exist");
                return false;
            }

            return true;
        }


        /// <summary>
        /// Process all data with selected technique from data.txt, transform it with transform.dll and save results time to results.txt
        /// </summary>
        private static void ProcessFile()
        {
            var stopwatch = Stopwatch.StartNew();
            Console.Write("Processing time: ");
            var finalCollection = new Dictionary<int, char>();
            try
            {
                var data = File.ReadAllLines(DataFileName);

                switch (_technique)
                {
                    case 1:
                        {
                            var numbersFromFile = new ConcurrentStack<uint>();

                            Parallel.ForEach(data, (line) =>
                            {
                                if (UInt32.TryParse(line, out uint uintFromString))
                                {
                                    numbersFromFile.Push(uintFromString);
                                }
                            });

                            var distinctedNumbersFromFile = numbersFromFile.Distinct().ToList();
                            _threadCountFinal = _maxRealThreadCount = 0;

                            // Wait for all tasks to complete.
                            var tasks = new Task[distinctedNumbersFromFile.Count()];
                            for (int i = 0; i < distinctedNumbersFromFile.Count(); i++)
                            {
                                var line = distinctedNumbersFromFile[i];

                                tasks[i] = Task.Factory.StartNew(() =>
                                {
                                    ProcessLine(line, finalCollection);
                                });
                            }
                            Task.WaitAll(tasks);
                            break;
                        };
                    case 2:
                        {
                            Parallel.ForEach(data, (line) =>
                            {
                                if (UInt32.TryParse(line, out uint uintFromString))
                                {
                                    ProcessLine(uintFromString, finalCollection);
                                }
                            });
                            break;
                        }

                    default:
                        break;
                }

                File.WriteAllText(OutputFileName, string.Join("", finalCollection.OrderBy(x => x.Key).Select(s => s.Value)));
                stopwatch.Stop();
                var processingTime = $"{stopwatch.Elapsed.ToString(@"mm\:ss\:fffffff")}";

                if (!File.Exists(ResultsFileName))
                {
                    File.Create(ResultsFileName).Close();
                    File.AppendAllText(ResultsFileName, $"{nameof(_techniqueName)}, {nameof(_processorCount)}, {nameof(_currentprocessesCount)}, {nameof(_currentThreadsCount)}, {nameof(_minThreadCount)}, {nameof(_maxRealThreadCount)}, {nameof(processingTime)}" + Environment.NewLine);
                }

                File.AppendAllText(ResultsFileName, $"{_techniqueName}, {_processorCount}, {_currentprocessesCount}, {_currentThreadsCount}, {_minThreadCount}, {_maxRealThreadCount}, {stopwatch.Elapsed.TotalMilliseconds}" + Environment.NewLine);

                Console.WriteLine(processingTime);
                Console.WriteLine();
                Console.WriteLine($"Count of output letters: {finalCollection.Count}");
                Console.WriteLine($"Max thread count: {_maxRealThreadCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.ToString());
                Console.WriteLine("EXCEPTION. Couldn't perform this test.");
                Console.WriteLine(ex);
                throw;
            }
            finally
            {
                finalCollection.Clear();
                finalCollection = null;
            }
        }

        /// <summary>
        /// Transfrom line with transform.dll and save results to final collection 
        /// </summary>
        /// <param name="line">Number from one line in data.txt</param>
        /// <param name="finalCollection">Collection to save results</param>
        private static void ProcessLine(uint line, Dictionary<int, char> finalCollection)
        {
            var threadCount = Interlocked.Increment(ref _threadCountFinal);

            var result = Transform(line);

            var byteResult = BitConverter.GetBytes(result);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(byteResult);
            }
            var letter = Convert.ToChar(byteResult[3]);
            var order = byteResult[2] + (byteResult[1] << 8) + (byteResult[0] << 16);

            lock (finalCollection)
            {
                finalCollection[order] = letter;

                if (_maxRealThreadCount < threadCount)
                {
                    _maxRealThreadCount = threadCount;
                }
            }

            Interlocked.Decrement(ref _threadCountFinal);
        }


    }
}
