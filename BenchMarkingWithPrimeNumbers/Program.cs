using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BenchMarkingWithPrimeNumbers
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            int n = 0b1_00000_00000_00000_00000_00000_00000;

            int[] primes = ComputeNode.FindAllPrimesUnder(n);
            StringBuilder sb = new StringBuilder();

            foreach (int I in primes)
            {
                sb.Append($"{I},");
            }


            using (StreamWriter sw = new StreamWriter("Prime Number.txt"))
            {
                sw.Write(sb.ToString());
            }

            Console.WriteLine(sb.ToString());
        }
    }

    class ComputeNode {

        public static int BaseTaskSize = 65536*4;

        protected int _start;
        protected int _end;

        protected ComputeNode _left;
        protected ComputeNode _right;

        public ComputeNode(int start, int end)
        {
            _start = start;
            _end = end;
            Branch();
        }

        /// <summary>
        ///     Branch all the way until base tasks 
        ///     * Branching must be symmetrical. 
        /// </summary>
        protected void Branch()
        {
            if (_end - _start >= ComputeNode.BaseTaskSize)
            {
                int mid = (_start + _end) / 2;
                _left = new ComputeNode(_start, mid);
                _right = new ComputeNode(mid, _end);
            }
            // Base task, leaf node, do nothing. 
        }

        public void AddAllLeafTasks(Queue<Task<SortedSet<int>>> taskBucket)
        {
            if (_left is null) // leaf node, return tasks
            {
                Task<SortedSet<int>> baseTask = new Task<SortedSet<int>>(
                        () => {
                            SortedSet<int> res = new SortedSet<int>();
                            for (int I = _start; I < _end; I++)
                            {
                                if (BruteForcePrimeTest(I))
                                    res.Add(I);
                            }
                            return res;
                        }
                    );
                taskBucket.Enqueue(baseTask);
            }
            else {
                _left.AddAllLeafTasks(taskBucket);
                _right.AddAllLeafTasks(taskBucket);
            }

        }

        static bool BruteForcePrimeTest(int n)
        {
            if (n == 2) return true;
            if (n % 2 == 0) return false;
            for (int I = 3; I < Math.Sqrt(n) + 1; I++)
            {
                if (n % I == 0) return false;
            }
            return true;
        }


        public static int[] FindAllPrimesUnder(int n)
        {
            if (n <= 1024) throw new Exception("Input too small to compuate in parallel.");
            ComputeNode rootNode = new ComputeNode(2, n);
            var listOftasks = new Queue<Task<SortedSet<int>>>();
            rootNode.AddAllLeafTasks(listOftasks);

            Console.WriteLine($"List of Tasks count: {listOftasks.Count}");

            var taskRunner = new TaskRunner<SortedSet<int>>(listOftasks);
            taskRunner.RunParallel();

            List<int> primes = new List<int>(); 
            foreach (SortedSet<int> batch in taskRunner._results)
            {
                foreach (int prime in batch)
                {
                    primes.Add(prime);
                }
            }
            primes.Sort();

            return primes.ToArray();
        }

    }

    /// <summary>
    ///     T is the return type of the task. 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class TaskRunner<T>
    {
        int _threadsAllowed = Environment.ProcessorCount;
        Queue<Task<T>> _tasks;
        public Queue<T> _results;
        public TaskRunner(Queue<Task<T>> listOfTasks)
        {
            _tasks = listOfTasks;
            _results = new Queue<T>();
        }

        public void RunParallel()
        {
            Thread[] threads = new Thread[_threadsAllowed];
            for (int I = 0; I < threads.Length; I++)
            {
                threads[I] = GetThread();
                threads[I].Start();
            }
            for (int I = 0; I < threads.Length; I++)
            {
                threads[I].Join();
            }

        }

        public Thread GetThread()
        {
            Thread t = new Thread(
                    () =>
                    {
                        while (true)
                        {
                            Task<T> task = GetTask();
                            if (task is null) break;
                            task.Start();
                            AddResult(task.Result);
                        }
                    }
                );
            return t;
        }

        public Task<T> GetTask()
        {
            lock (this)
            {
                if (_tasks.Count == 0)
                {
                    return null;
                }
                return _tasks.Dequeue();
            }
        }

        public void AddResult(T result)
        {
            lock(this)
            _results.Enqueue(result);
        }

    }
}
