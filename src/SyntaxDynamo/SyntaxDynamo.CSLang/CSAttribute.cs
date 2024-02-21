// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Runtime.InteropServices;

namespace SyntaxDynamo.CSLang
{
    public class CSAttribute : LineCodeElementCollection<ICodeElement>
    {
        public CSAttribute(CSIdentifier name, CSArgumentList args, bool isSingleLine = false, bool isReturn = false)
            : base(isSingleLine, false, isSingleLine)
        {
            Add(new SimpleElement("["));
            if (isReturn)
                Add(new SimpleElement("return:"));
            Add(Exceptions.ThrowOnNull(name, nameof(name)));
            if (args != null)
            {
                Add(new SimpleElement("("));
                Add(args);
                Add(new SimpleElement(")"));
            }
            Add(new SimpleElement("]"));
        }

        public CSAttribute(string name, CSArgumentList args)
            : this(new CSIdentifier(name), args)
        {
        }

        public CSAttribute(string name, params ICSExpression[] exprs)
            : this(new CSIdentifier(name), CSArgumentList.FromExpressions(exprs))
        {
        }

        // DllImport("msvcrt.dll", EntryPoint="puts")
        public static CSAttribute DllImport(string dllName, string entryPoint = null)
        {
            CSArgumentList args = new CSArgumentList();
            args.Add(CSConstant.Val(dllName));
            if (entryPoint != null)
                args.Add(new CSAssignment("EntryPoint", CSAssignmentOperator.Assign, CSConstant.Val(entryPoint)));
            return new CSAttribute(new CSIdentifier("DllImport"), args, true);
        }

        public static CSAttribute DllImport(CSBaseExpression dllName, string entryPoint = null)
        {
            CSArgumentList args = new CSArgumentList();
            args.Add(dllName);
            if (entryPoint != null)
                args.Add(new CSAssignment("EntryPoint", CSAssignmentOperator.Assign, CSConstant.Val(entryPoint)));
            return new CSAttribute(new CSIdentifier("DllImport"), args, true);
        }

        public static CSAttribute UnmanagedCallConv(string callconv)
        {
            CSArgumentList args = new CSArgumentList();

            args.Add(new CSAssignment("CallConvs", CSAssignmentOperator.Assign, new CSIdentifier("new Type[] { typeof("+callconv+") }")));
            return new CSAttribute(new CSIdentifier("UnmanagedCallConv"), args, true);
        }

        static CSAttribute returnMarshalAsI1 = new CSAttribute("return: MarshalAs", new CSIdentifier("UnmanagedType.I1"));

        public static CSAttribute ReturnMarshalAsI1 => returnMarshalAsI1;

        public static CSAttribute FieldOffset(int offset)
        {
            CSArgumentList args = new CSArgumentList();
            args.Add(CSConstant.Val(offset));
            return new CSAttribute(new CSIdentifier("FieldOffset"), args, true);
        }

        public static CSAttribute LayoutKind(LayoutKind layout)
        {
            CSArgumentList args = new CSArgumentList();
            args.Add(new CSIdentifier(String.Format("LayoutKind.{0}", layout)));
            return new CSAttribute(new CSIdentifier("StructLayout"), args, true);
        }

        public static CSAttribute FromAttr(Type attribute, CSArgumentList args, bool isSingleLine = false, bool isReturn = false)
        {
            Exceptions.ThrowOnNull(attribute, nameof(attribute));
            if (!attribute.IsSubclassOf(typeof(Attribute)))
                throw new ArgumentException(String.Format("Type {0} is not an Attribute type.", attribute.Name), nameof(attribute));
            var name = attribute.Name.EndsWith("Attribute") ?
                attribute.Name.Substring(0, attribute.Name.Length - "Attribute".Length) : attribute.Name;
            return new CSAttribute(new CSIdentifier(name), args, isSingleLine, isReturn);
        }

        public static CSAttribute MarshalAsFunctionPointer()
        {
            CSArgumentList list = new CSArgumentList();
            list.Add(new CSArgument(new CSIdentifier("UnmanagedType.FunctionPtr")));
            return FromAttr(typeof(MarshalAsAttribute), list, false);
        }
    }
}

