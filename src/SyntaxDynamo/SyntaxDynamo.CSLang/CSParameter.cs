// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using SyntaxDynamo;


namespace SyntaxDynamo.CSLang
{
    public class CSParameter : DelegatedSimpleElement
    {
        public CSParameter(CSType type, CSIdentifier name,
                            CSParameterKind parameterKind = CSParameterKind.None,
                            CSConstant defaultValue = null)
        {
            CSType = Exceptions.ThrowOnNull(type, nameof(type));
            Name = Exceptions.ThrowOnNull(name, nameof(name));
            ParameterKind = parameterKind;
            DefaultValue = defaultValue;
        }

        public CSParameter(CSType type, string name,
                            CSParameterKind parameterKind = CSParameterKind.None,
                            CSConstant defaultValue = null)
            : this(type, new CSIdentifier(name), parameterKind, defaultValue)
        {
        }

        public CSParameter(string type, string name,
                            CSParameterKind parameterKind = CSParameterKind.None,
                            CSConstant defaultValue = null)
            : this(new CSSimpleType(type), new CSIdentifier(name), parameterKind, defaultValue)
        {
        }


        protected override void LLWrite(ICodeWriter writer, object o)
        {
            if (this.ParameterKind != CSParameterKind.None)
            {
                writer.Write(ToParameterKindString(this.ParameterKind), false);
                writer.Write(' ', false);
            }
            this.CSType.WriteAll(writer);
            writer.Write(' ', true);
            Name.WriteAll(writer);
            if ((Object)DefaultValue != null)
            {
                writer.Write(" = ", true);
                DefaultValue.WriteAll(writer);
            }
        }

        static string ToParameterKindString(CSParameterKind parameterKind)
        {
            switch (parameterKind)
            {
                case CSParameterKind.None:
                    return "";
                case CSParameterKind.Out:
                    return "out";
                case CSParameterKind.Ref:
                    return "ref";
                case CSParameterKind.This:
                    return "this";
                case CSParameterKind.Params:
                    return "params";
                default:
                    throw new ArgumentOutOfRangeException(nameof(parameterKind), "unexpected parameter kind " + parameterKind.ToString());
            }
        }

        public CSType CSType { get; private set; }
        public CSIdentifier Name { get; private set; }
        public CSConstant DefaultValue { get; private set; }
        public CSParameterKind ParameterKind { get; private set; }
    }

    public class CSParameterList : CommaListElementCollection<CSParameter>
    {
        public CSParameterList(IEnumerable<CSParameter> parameters)
            : base()
        {
            if (parameters != null)
                AddRange(parameters);
        }
        public CSParameterList(params CSParameter[] parameters)
            : base()
        {
            if (parameters != null)
                AddRange(parameters);
        }

        public CSParameterList() : this((IEnumerable<CSParameter>)null) { }

        public CSParameterList(CSParameter parameter) : this(new CSParameter[] { parameter }) { }

        public CSParameterList And(CSParameter parameter)
        {
            Add(Exceptions.ThrowOnNull(parameter, nameof(parameter)));
            return this;
        }

        public CSParameterList And(string type, string identifier,
                                    CSParameterKind parameterKind = CSParameterKind.None,
                                    CSConstant defaultValue = null)
        {
            return And(new CSParameter(new CSSimpleType(Exceptions.ThrowOnNull(type, nameof(type))),
                new CSIdentifier(Exceptions.ThrowOnNull(identifier, nameof(identifier))), parameterKind, defaultValue));
        }
    }
}

