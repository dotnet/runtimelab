// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace SyntaxDynamo.CSLang
{
    public class CSFunctionCall : CSBaseExpression, ICSLineable
    {
        public CSFunctionCall(CSIdentifier ident, CommaListElementCollection<CSBaseExpression> paramList, bool isConstructor = false)
        {
            Name = Exceptions.ThrowOnNull(ident, "ident");
            Parameters = Exceptions.ThrowOnNull(paramList, "paramList");
            IsConstructor = isConstructor;
        }

        public CSFunctionCall(string identifier, bool isConstructor, params CSBaseExpression[] parameters)
            : this(new CSIdentifier(identifier), new CommaListElementCollection<CSBaseExpression>(parameters), isConstructor)
        {
        }

        public static CSFunctionCall Function(string identifier, params CSBaseExpression[] parameters)
        {
            return new CSFunctionCall(identifier, false, parameters);
        }
        public static CSLine FunctionLine(string identifier, params CSBaseExpression[] parameters) => new CSLine(Function(identifier, parameters));

        public static CSFunctionCall Ctor(string identifier, params CSBaseExpression[] parameters)
        {
            return new CSFunctionCall(identifier, true, parameters);
        }
        public static CSLine CtorLine(string identifier, params CSBaseExpression[] parameters) => new CSLine(Ctor(identifier, parameters));

        public static CSLine ConsoleWriteLine(params CSBaseExpression[] parameters) => FunctionLine("Console.WriteLine", parameters);

        protected override void LLWrite(ICodeWriter writer, object o)
        {
            if (IsConstructor)
                writer.Write("new ", false);
            Name.WriteAll(writer);
            writer.Write("(", false);
            Parameters.WriteAll(writer);
            writer.Write(")", false);
        }

        public bool IsConstructor { get; private set; }
        public CSIdentifier Name { get; private set; }
        public CommaListElementCollection<CSBaseExpression> Parameters { get; private set; }

        public static CSLine FunctionCallLine(CSIdentifier identifier, params CSBaseExpression[] parameters)
        {
            return FunctionCallLine(identifier, false, parameters);
        }

        public static CSLine FunctionCallLine(CSIdentifier identifier, bool isConstructor, params CSBaseExpression[] parameters)
        {
            return new CSLine(new CSFunctionCall(identifier,
                new CommaListElementCollection<CSBaseExpression>(parameters), isConstructor));
        }

        public static CSLine FunctionCallLine(string identifier, params CSBaseExpression[] parameters)
        {
            return FunctionCallLine(identifier, false, parameters);
        }

        public static CSLine FunctionCallLine(string identifier, bool isConstructor, params CSBaseExpression[] parameters)
        {
            return new CSLine(new CSFunctionCall(new CSIdentifier(Exceptions.ThrowOnNull(identifier, "identifier")),
                new CommaListElementCollection<CSBaseExpression>(parameters), isConstructor));
        }

        static CSIdentifier iNameOf = new CSIdentifier("nameof");

        public static CSFunctionCall Nameof(CSIdentifier id)
        {
            return FooOf(iNameOf, id);
        }

        public static CSFunctionCall Nameof(string name)
        {
            return Nameof(new CSIdentifier(name));
        }

        static CSIdentifier iTypeof = new CSIdentifier("typeof");

        public static CSFunctionCall Typeof(Type t)
        {
            return Typeof(t.Name);
        }

        public static CSFunctionCall Typeof(string t)
        {
            return FooOf(iTypeof, new CSIdentifier(t));
        }

        public static CSFunctionCall Typeof(CSSimpleType t)
        {
            return Typeof(t.Name);
        }

        static CSIdentifier iSizeof = new CSIdentifier("sizeof");

        public static CSFunctionCall Sizeof(CSBaseExpression expr)
        {
            return FooOf(iSizeof, expr);
        }

        static CSIdentifier iDefault = new CSIdentifier("default");

        public static CSFunctionCall Default(Type t)
        {
            return Default(t.Name);
        }

        public static CSFunctionCall Default(string t)
        {
            return FooOf(iDefault, new CSIdentifier(t));
        }

        public static CSFunctionCall Default(CSSimpleType t)
        {
            return Default(t.Name);
        }

        static CSFunctionCall FooOf(CSIdentifier foo, CSBaseExpression parameter)
        {
            CommaListElementCollection<CSBaseExpression> parms = new CommaListElementCollection<CSBaseExpression>();
            parms.Add(parameter);
            return new CSFunctionCall(foo, parms, false);
        }
    }

}

