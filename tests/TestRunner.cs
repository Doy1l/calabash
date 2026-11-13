using System;
using System.Collections.Generic;

namespace Calabash.Tests
{
    /// <summary>Минимальный раннер: без NuGet, чтобы тесты запускались где угодно.</summary>
    public static class TestRunner
    {
        static int _passed;
        static int _failed;
        static readonly List<string> _failures = new List<string>();
        static string _current = "";

        public static void Run(string name, Action test)
        {
            _current = name;
            try
            {
                test();
                _passed++;
                Console.WriteLine($"  PASS  {name}");
            }
            catch (Exception ex)
            {
                _failed++;
                _failures.Add($"{name}: {ex.Message}");
                Console.WriteLine($"  FAIL  {name}");
                Console.WriteLine($"        {ex.Message}");
            }
        }

        public static void Section(string title)
        {
            Console.WriteLine();
            Console.WriteLine($"--- {title} ---");
        }

        public static int Summary()
        {
            Console.WriteLine();
            Console.WriteLine(new string('=', 62));
            Console.WriteLine($"  passed: {_passed}   failed: {_failed}   total: {_passed + _failed}");
            if (_failed > 0)
            {
                Console.WriteLine();
                Console.WriteLine("  FAILURES:");
                foreach (var f in _failures)
                    Console.WriteLine($"   - {f}");
            }
            Console.WriteLine(new string('=', 62));
            return _failed == 0 ? 0 : 1;
        }

        // ---------- assert-хелперы ----------

        public static void True(bool condition, string message = null)
        {
            if (!condition)
                throw new Exception(message ?? "ожидалось true, получено false");
        }

        public static void False(bool condition, string message = null)
        {
            if (condition)
                throw new Exception(message ?? "ожидалось false, получено true");
        }

        public static void Eq<T>(T expected, T actual, string what = null)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                string prefix = what == null ? "" : what + ": ";
                throw new Exception($"{prefix}ожидалось {expected}, получено {actual}");
            }
        }

        public static void Near(float expected, float actual, float tol = 0.001f, string what = null)
        {
            if (Math.Abs(expected - actual) > tol)
            {
                string prefix = what == null ? "" : what + ": ";
                throw new Exception($"{prefix}ожидалось {expected} (±{tol}), получено {actual}");
            }
        }

        public static void Greater(float actual, float threshold, string what = null)
        {
            if (actual <= threshold)
            {
                string prefix = what == null ? "" : what + ": ";
                throw new Exception($"{prefix}ожидалось > {threshold}, получено {actual}");
            }
        }

        public static void Less(float actual, float threshold, string what = null)
        {
            if (actual >= threshold)
            {
                string prefix = what == null ? "" : what + ": ";
                throw new Exception($"{prefix}ожидалось < {threshold}, получено {actual}");
            }
        }

        public static void Throws<TException>(Action action, string what = null) where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                return;
            }
            catch (Exception ex)
            {
                throw new Exception($"{(what ?? "")}: ожидалось {typeof(TException).Name}, получено {ex.GetType().Name}");
            }
            throw new Exception($"{(what ?? "")}: ожидалось {typeof(TException).Name}, исключения не было");
        }
    }
}
