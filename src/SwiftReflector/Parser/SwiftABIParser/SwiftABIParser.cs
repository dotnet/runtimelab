// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using SwiftReflector.SwiftXmlReflection;

namespace SwiftReflector.Parser
{
    public class ABIRootNode
    {
        [JsonProperty("ABIRoot")]
        public RootNode ABIRoot { get; set; }
    }

    public class RootNode
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string PrintedName { get; set; }
        public List<Node> Children { get; set; }
    }

    public class Node
    {
        public string Kind { get; set; }
        public string Name { get; set; }
        public string MangledName { get; set; }
        public string ModuleName { get; set; }
        public string PrintedName { get; set; }
        public List<Node> Children { get; set; }
    }

    public class SwiftABIParser : ISwiftParser
    {
        public ModuleDeclaration GetModuleDeclaration(string filePath, ErrorHandling errors)
        {
            string jsonContent = File.ReadAllText(filePath);
            var abiRoot = JsonConvert.DeserializeObject<ABIRootNode>(jsonContent);

            var stack = new Stack<Node>();
            foreach (var child in abiRoot.ABIRoot.Children)
            {
                stack.Push(child);
            }

            string moduleName = Path.GetFileNameWithoutExtension(filePath);
            if (moduleName.EndsWith(".abi", StringComparison.OrdinalIgnoreCase))
                moduleName = moduleName.Substring(0, moduleName.Length - 4);

            ModuleDeclaration moduleDeclaration = new ModuleDeclaration(moduleName);
            while (stack.Count > 0)
            {
                var node = stack.Pop();

                switch (node.Kind)
                {
                    case "Function":
                        var decl = CreateFunctionDecl(node);
                        decl.Module = moduleDeclaration;
                        moduleDeclaration.Declarations.Add(decl);
                        break;
                    default:
                        errors.Add(new NotImplementedException());
                        break;

                }

                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        stack.Push(child);
                    }
                }
            }

            return moduleDeclaration;
        }

        public FunctionDeclaration CreateFunctionDecl(Node decl)
        {
            FunctionDeclaration functionDeclaration = new FunctionDeclaration();
            functionDeclaration.Name = decl.Name;
            functionDeclaration.MangledName = decl.MangledName;
            functionDeclaration.IsStatic = true;

            switch (decl.Children[0].Name)
            {
                case "Void":
                    functionDeclaration.ReturnTypeSpec = new TupleTypeSpec();
                    break;
                default:
                    functionDeclaration.ReturnTypeSpec = new NamedTypeSpec(decl.Children[0].PrintedName);
                    break;
            }

            if (decl.Children.Count == 0)
                functionDeclaration.ParameterLists.Add(new List<ParameterItem>());
            else {
                var funcSignature = decl.PrintedName.Split("(")[1].Split(")")[0].Split(":", StringSplitOptions.RemoveEmptyEntries);
                for (int i = 1; i < decl.Children.Count; i++)
                {
                    var param = decl.Children[i];
                    ParameterItem parameterItem = new ParameterItem();
                    parameterItem.TypeSpec = new NamedTypeSpec(param.PrintedName);
                    if (funcSignature.Length > i - 1)
                        parameterItem.PublicName = funcSignature[i - 1];
                    functionDeclaration.ParameterLists.Add(new List<ParameterItem> { parameterItem });
                }
            }
            return functionDeclaration;
        }
    }
}