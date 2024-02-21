// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using ProductException = SwiftReflector.ExceptionTools.RuntimeException;

namespace SwiftReflector.ExceptionTools
{
    static class ErrorHelper
    {
        public enum WarningLevel
        {
            Error = -1,
            Warning = 0,
            Disable = 1,
        }
        public const string Prefix = "BT";
        static Dictionary<int, WarningLevel> warning_levels;
        public static int Verbosity { get; set; }

        public static WarningLevel GetWarningLevel(int code)
        {
            WarningLevel level;

            if (warning_levels == null)
                return WarningLevel.Warning;

            // code -1: all codes
            if (warning_levels.TryGetValue(-1, out level))
                return level;

            if (warning_levels.TryGetValue(code, out level))
                return level;

            return WarningLevel.Warning;
        }

        public static void SetWarningLevel(WarningLevel level, int? code = null /* if null, apply to all warnings */)
        {
            if (warning_levels == null)
                warning_levels = new Dictionary<int, WarningLevel>();
            if (code.HasValue)
            {
                warning_levels[code.Value] = level;
            }
            else
            {
                warning_levels[-1] = level; // code -1: all codes.
            }
        }

        public static ProductException CreateError(int code, string message, params object[] args)
        {
            return new ProductException(code, true, message, args);
        }

        public static ProductException CreateError(int code, Exception innerException, string message, params object[] args)
        {
            return new ProductException(code, true, innerException, message, args);
        }

        public static ProductException CreateWarning(int code, string message, params object[] args)
        {
            return new ProductException(code, false, message, args);
        }

        public static ProductException CreateWarning(int code, Exception innerException, string message, params object[] args)
        {
            return new ProductException(code, false, innerException, message, args);
        }

        public static void Error(int code, Exception innerException, string message, params object[] args)
        {
            throw new ProductException(code, true, innerException, message, args);
        }

        public static void Error(int code, string message, params object[] args)
        {
            throw new ProductException(code, true, message, args);
        }

        public static void Warning(int code, string message, params object[] args)
        {
            Show(new ProductException(code, false, message, args));
        }

        public static void Warning(int code, Exception innerException, string message, params object[] args)
        {
            Show(new ProductException(code, false, innerException, message, args));
        }

        public static int Show(IEnumerable<Exception> list)
        {
            List<Exception> exceptions = new List<Exception>();
            bool error = false;

            foreach (var e in list)
                CollectExceptions(e, exceptions);

            foreach (var ex in exceptions)
                error |= ShowInternal(ex);

            return error ? 1 : 0;
        }

        static public int Show(Exception e)
        {
            List<Exception> exceptions = new List<Exception>();
            bool error = false;

            CollectExceptions(e, exceptions);

            foreach (var ex in exceptions)
                error |= ShowInternal(ex);

            return error ? 1 : 0;
        }

        static void Exit(int exitCode)
        {
            Environment.Exit(exitCode);
        }

        static void CollectExceptions(Exception ex, List<Exception> exceptions)
        {
            AggregateException ae = ex as AggregateException;

            if (ae != null && ae.InnerExceptions.Count > 0)
            {
                foreach (var ie in ae.InnerExceptions)
                    CollectExceptions(ie, exceptions);
            }
            else
            {
                exceptions.Add(ex);
            }
        }

        static bool ShowInternal(Exception e)
        {
            ProductException mte = (e as ProductException);
            bool error = true;

            if (mte != null)
            {
                error = mte.Error;

                if (!error && GetWarningLevel(mte.Code) == WarningLevel.Disable)
                    return false; // This is an ignored warning.

                Console.Error.WriteLine(mte.ToString());

                if (Verbosity > 1)
                    ShowInner(e);

                if (Verbosity > 2 && !string.IsNullOrEmpty(e.StackTrace))
                    Console.Error.WriteLine(e.StackTrace);
            }
            else
            {
                Console.Error.WriteLine(e.ToString());
                if (Verbosity > 1)
                    ShowInner(e);
                if (Verbosity > 2 && !string.IsNullOrEmpty(e.StackTrace))
                    Console.Error.WriteLine(e.StackTrace);
            }

            return error;
        }

        static void ShowInner(Exception e)
        {
            Exception ie = e.InnerException;
            if (ie == null)
                return;

            if (Verbosity > 3)
            {
                Console.Error.WriteLine("--- inner exception");
                Console.Error.WriteLine(ie);
                Console.Error.WriteLine("---");
            }
            else
            {
                Console.Error.WriteLine("\t{0}", ie.Message);
            }
            ShowInner(ie);
        }
    }
}
