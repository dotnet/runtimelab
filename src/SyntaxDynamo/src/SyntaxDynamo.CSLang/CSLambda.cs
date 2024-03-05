// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Linq;
using System.Collections.Generic;

namespace SyntaxDynamo.CSLang
{
    public class CSLambda : CSBaseExpression
    {
        public CSLambda(CSParameterList parameters, ICSExpression value)
        {
            ArgumentNullException.ThrowIfNull(value, nameof(value));
            Parameters = parameters ?? new CSParameterList();
            Value = value;
            Body = null;
        }

        public CSLambda(CSParameterList parameters, CSCodeBlock body)
        {
            body = body ?? new CSCodeBlock();
            Parameters = parameters ?? new CSParameterList();
            Value = null;
            Body = body;
        }

        public CSLambda(ICSExpression value, params string[] parameters)
            : this(new CSParameterList(parameters.Select(p => new CSParameter(CSSimpleType.Void, new CSIdentifier(p)))), value)
        {
        }

        public CSLambda(CSCodeBlock body, params string[] parameters)
            : this(new CSParameterList(parameters.Select(p => new CSParameter(CSSimpleType.Void, new CSIdentifier(p)))), body)
        {
        }

        public CSParameterList Parameters { get; private set; }
        public ICSExpression Value { get; private set; }
        public CSCodeBlock Body { get; private set; }

        #region implemented abstract members of DelegatedSimpleElem

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            // hack - Parameters really want types. If you set them to void, we'll consider them to be
            // typeless.
            bool allVoid = Parameters.All(p => p.CSType == CSSimpleType.Void);

            writer.Write('(', true);
            if (allVoid)
            {
                bool didFirst = false;
                foreach (CSParameter p in Parameters)
                {
                    if (didFirst)
                    {
                        writer.Write(", ", true);
                    }
                    p.Name.WriteAll(writer);
                    didFirst = true;
                }
            }
            else
            {
                Parameters.WriteAll(writer);
            }
            writer.Write(") => ", true);
            if (Value != null)
            {
                Value.WriteAll(writer);
            }
            else
            {
                Body.WriteAll(writer);
            }
        }

        #endregion
    }
}
