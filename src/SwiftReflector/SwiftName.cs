// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SwiftReflector
{
    public class SwiftName
    {
        public SwiftName(string name, bool isPunyCode)
        {
            if (name == null)
                throw new ArgumentNullException("name");
            PunyName = name;
            Name = isPunyCode ? name.DePunyCode() : name;
        }

        public string Name { get; private set; }
        public string PunyName { get; private set; }
        public bool HasPunyCode { get { return Name != PunyName; } }

        public override string ToString()
        {
            return HasPunyCode ? String.Format("{0} ({1})", Name, PunyName) : Name;
        }

        static SwiftName emptyName = new SwiftName("", false);
        public static SwiftName Empty { get { return emptyName; } }

        public override bool Equals(object obj)
        {
            var other = obj as SwiftName;
            if (other == null)
                return false;
            return (PunyName == other.PunyName) &&
                (HasPunyCode ? Name == other.Name : true);
        }

        public override int GetHashCode()
        {
            return PunyName.GetHashCode() +
                (HasPunyCode ? Name.GetHashCode() : 0);
        }
    }
}

