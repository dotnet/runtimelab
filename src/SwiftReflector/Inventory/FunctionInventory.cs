// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using SwiftReflector.ExceptionTools;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory
{
    public class FunctionInventory : Inventory<OverloadInventory>
    {
        int sizeofMachinePointer;
        public FunctionInventory(int sizeofMachinePointer)
        {
            this.sizeofMachinePointer = sizeofMachinePointer;
        }
        public override void Add(TLDefinition tld, Stream srcStm)
        {
            TLFunction tlf = tld as TLFunction;
            if (tlf == null)
                throw ErrorHelper.CreateError(ReflectorError.kInventoryBase + 10, $"expected a top-level function but got a {tld.GetType().Name}");

            lock (valuesLock)
            {
                OverloadInventory overloads = null;
                if (!values.TryGetValue(tlf.Name, out overloads))
                {
                    overloads = new OverloadInventory(tlf.Name, sizeofMachinePointer);
                    values.Add(tlf.Name, overloads);
                }
                overloads.Add(tlf, srcStm);
            }
        }

        public TLFunction ContainsEquivalentFunction(TLFunction func)
        {
            // for finding a func with the same name and signature which may or may not be a thunk
            var nameMatched = MethodsWithName(func.Signature.Name);
            if (nameMatched.Count == 0)
                return null;
            foreach (var candidate in nameMatched)
            {
                if (ArgumentsMatch(candidate, func) && ReturnsMatch(candidate, func) && candidate.Signature.CanThrow == func.Signature.CanThrow)
                    return candidate;
            }
            return null;
        }

        public void ReplaceFunction(TLFunction original, TLFunction replacement)
        {
            var overloads = values[original.Name];
            if (overloads == null)
                throw new ArgumentException("original function name must be in the inventory");
            if (!overloads.Functions.Remove(original))
                throw new ArgumentException("original function not found");
            overloads.Functions.Add(replacement);
        }

        static bool ArgumentsMatch(TLFunction candidate, TLFunction func)
        {
            if (candidate.Signature.ParameterCount != func.Signature.ParameterCount)
                return false;

            for (int i = 0; i < candidate.Signature.ParameterCount; i++)
            {
                if (!candidate.Signature.GetParameter(i).Equals(func.Signature.GetParameter(i)))
                    return false;
            }
            return true;
        }

        static bool ReturnsMatch(TLFunction candidate, TLFunction func)
        {
            return candidate.Signature.ReturnType.Equals(func.Signature.ReturnType);
        }

        public IEnumerable<Tuple<SwiftName, TLFunction>> AllMethodsNoCDTor()
        {
            foreach (SwiftName key in values.Keys)
            {
                OverloadInventory oi = values[key];
                foreach (TLFunction tlf in oi.Functions)
                    yield return new Tuple<SwiftName, TLFunction>(key, tlf);
            }
        }

        public List<TLFunction> MethodsWithName(SwiftName name)
        {
            var result = new List<TLFunction>();
            foreach (var oi in Values)
            {
                if (oi.Name.Name == name.Name)
                    result.AddRange(oi.Functions);
            }
            return result;
        }

        public List<TLFunction> MethodsWithName(string name)
        {
            SwiftName sn = new SwiftName(name, false);
            return MethodsWithName(sn);
        }

        public List<TLFunction> AllocatingConstructors()
        {
            return MethodsWithName(Decomposer.kSwiftAllocatingConstructorName);
        }

        public List<TLFunction> DeallocatingDestructors()
        {
            return MethodsWithName(Decomposer.kSwiftDeallocatingDestructorName);
        }
    }

}

