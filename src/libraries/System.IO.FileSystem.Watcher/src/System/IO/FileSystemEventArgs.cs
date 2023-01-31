// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.IO
{
    /// <devdoc>
    ///    Provides data for the directory events: <see cref='System.IO.FileSystemWatcher.Changed'/>, <see cref='System.IO.FileSystemWatcher.Created'/>, <see cref='System.IO.FileSystemWatcher.Deleted'/>.
    /// </devdoc>
    public class FileSystemEventArgs : EventArgs
    {
        private readonly WatcherChangeTypes _changeType;
        private readonly string? _name;
        private readonly string _fullPath;

        /// <devdoc>
        /// Initializes a new instance of the <see cref='System.IO.FileSystemEventArgs'/> class.
        /// </devdoc>
        public FileSystemEventArgs(WatcherChangeTypes changeType, string directory, string? name)
        {
            ArgumentNullException.ThrowIfNull(directory);

            _changeType = changeType;
            _name = name;
            _fullPath = Combine(directory, name);
        }

        /// <summary>Combines a directory path and a relative file name into a single path.</summary>
        /// <param name="directory">The directory path.</param>
        /// <param name="name">The file name.</param>
        /// <returns>The combined name.</returns>
        /// <remarks>
        /// This is like Path.Combine, except without argument validation,
        /// and a separator is used even if the name argument is empty.
        /// </remarks>
        internal static string Combine(string directory, string? name)
        {
            bool hasSeparator = directory.Length != 0 && PathInternal.IsDirectorySeparator(directory[^1]);

            return hasSeparator ?
                directory + name :
                directory + Path.DirectorySeparatorChar + name;
        }

        /// <devdoc>
        ///    Gets one of the <see cref='System.IO.WatcherChangeTypes'/> values.
        /// </devdoc>
        public WatcherChangeTypes ChangeType
        {
            get
            {
                return _changeType;
            }
        }

        /// <devdoc>
        ///    Gets the fully qualified path of the affected file or directory.
        /// </devdoc>
        public string FullPath
        {
            get
            {
                return _fullPath;
            }
        }


        /// <devdoc>
        ///       Gets the name of the affected file or directory.
        /// </devdoc>
        public string? Name
        {
            get
            {
                return _name;
            }
        }
    }
}
