// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using SwiftReflector.Demangling;

namespace SwiftReflector.Inventory
{
    public class ProtocolInventory : Inventory<ProtocolContents>
    {
        int sizeofMachinePointer;
        public ProtocolInventory(int sizeofMachinePointer)
        {
            this.sizeofMachinePointer = sizeofMachinePointer;
        }

        public override void Add(TLDefinition tld, Stream srcStm)
        {
            lock (valuesLock)
            {
                SwiftName className = ClassInventory.ToClassName(tld);
                SwiftClassName formalName = ClassInventory.ToFormalClassName(tld);
                ProtocolContents contents = null;
                if (!values.TryGetValue(className, out contents))
                {
                    contents = new ProtocolContents(formalName, sizeofMachinePointer);
                    values.Add(className, contents);
                }
                contents.Add(tld, srcStm);
            }
        }
    }
}

