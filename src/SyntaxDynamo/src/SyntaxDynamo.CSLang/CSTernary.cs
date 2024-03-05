// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSTernary : CSBaseExpression
    {
        public CSTernary(CSBaseExpression predicate, CSBaseExpression onTrue, CSBaseExpression onFalse, bool addParentheses)
        {
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));
            ArgumentNullException.ThrowIfNull(onTrue, nameof(onTrue));
            ArgumentNullException.ThrowIfNull(onFalse, nameof(onFalse));
            Predicate = predicate;
            OnTrue = onTrue;
            OnFalse = onFalse;
            AddParentheses = addParentheses;
        }
        public CSBaseExpression Predicate { get; private set; }
        public CSBaseExpression OnTrue { get; private set; }
        public CSBaseExpression OnFalse { get; private set; }
        public bool AddParentheses { get; set; }

        #region implemented abstract members of DelegatedSimpleElem

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            if (AddParentheses)
            {
                writer.Write('(', true);
            }
            Predicate.WriteAll(writer);
            writer.Write(" ? ", true);
            OnTrue.WriteAll(writer);
            writer.Write(" : ", true);
            OnFalse.WriteAll(writer);
            if (AddParentheses)
            {
                writer.Write(')', true);
            }
        }

        #endregion
    }
}

