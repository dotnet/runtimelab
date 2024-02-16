// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSIndexExpression : CSBaseExpression
    {
        public CSIndexExpression(CSBaseExpression aggregate, CommaListElementCollection<CSBaseExpression> paramList, bool addParensAroundAggregate)
        {
            AddParensAroundAggregate = addParensAroundAggregate;
            Aggregate = Exceptions.ThrowOnNull(aggregate, "aggregate");
            Parameters = Exceptions.ThrowOnNull(paramList, "paramList");
        }

        public CSIndexExpression(string identifier, bool addParensAroundAggregate, params CSBaseExpression[] parameters)
            : this(new CSIdentifier(identifier), new CommaListElementCollection<CSBaseExpression>(parameters), addParensAroundAggregate)
        {
        }

        public CSIndexExpression(CSBaseExpression aggregate, bool addParensAroundAggregate, params CSBaseExpression[] parameters)
            : this(aggregate, new CommaListElementCollection<CSBaseExpression>(parameters), addParensAroundAggregate)
        {
        }

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            if (AddParensAroundAggregate)
                writer.Write('(', false);
            Aggregate.WriteAll(writer);
            if (AddParensAroundAggregate)
                writer.Write(')', false);
            writer.Write("[", false);
            Parameters.WriteAll(writer);
            writer.Write("]", false);
        }

        public bool AddParensAroundAggregate { get; private set; }
        public CSBaseExpression Aggregate { get; private set; }
        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }

    }
}

