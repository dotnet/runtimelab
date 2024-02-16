// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

//#define CRASH_ON_EXCEPTION

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;

namespace SwiftReflector
{
    public class ErrorHandling
    {
        object messagesLock = new object();
        List<ReflectorError> messages;

        public ErrorHandling()
        {
            messages = new List<ReflectorError>();
            SkippedTypes = new List<string>();
            SkippedFunctions = new List<string>();
        }

        public IEnumerable<ReflectorError> Messages
        {
            get { return messages; }
        }

        public IEnumerable<ReflectorError> Errors
        {
            get { return messages.Where((v) => !v.IsWarning); }
        }

        public IEnumerable<ReflectorError> Warnings
        {
            get { return messages.Where((v) => v.IsWarning); }
        }

        public List<string> SkippedTypes { get; private set; }
        public List<string> SkippedFunctions { get; private set; }

        public void Add(ErrorHandling eh)
        {
            lock (messagesLock)
            {
                lock (eh.messagesLock)
                {
                    messages.AddRange(eh.messages);
                }
            }
        }

        public void Add(params ReflectorError[] errors)
        {
            lock (messagesLock)
            {
                messages.AddRange(errors);
            }
        }

        public void Add(Exception exception)
        {
            lock (messagesLock)
            {
#if CRASH_ON_EXCEPTION
			ExceptionDispatchInfo.Capture (exception).Throw ();
#else
                messages.Add(new ReflectorError(exception));
#endif
            }
        }

        public bool AnyMessages
        {
            get
            {
                lock (messagesLock)
                {
                    return messages.Count > 0;
                }
            }
        }

        public bool AnyErrors
        {
            get
            {
                lock (messagesLock)
                {
                    return messages.Any((v) => !v.IsWarning);
                }
            }
        }

        public int WarningCount
        {
            get
            {
                lock (messagesLock)
                {
                    return messages.Count((v) => v.IsWarning);
                }
            }
        }

        public int ErrorCount
        {
            get
            {
                lock (messagesLock)
                {
                    return messages.Count((v) => !v.IsWarning);
                }
            }
        }

        public int Show(int verbosity)
        {
            // ErrorHelper.Verbosity = verbosity;
            // return ErrorHelper.Show (messages.Select ((v) => v.Exception));
            return verbosity;
        }
    }
}
