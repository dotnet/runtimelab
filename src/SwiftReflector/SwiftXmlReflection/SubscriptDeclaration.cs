// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace SwiftReflector.SwiftXmlReflection
{
    // this is a pseudo class.
    public class SubscriptDeclaration
    {
        public SubscriptDeclaration(FunctionDeclaration getter, FunctionDeclaration setter, FunctionDeclaration materializer)
        {
            Getter = getter;
            Setter = setter;
            Materializer = materializer;
        }

        public FunctionDeclaration Getter { get; set; }
        public FunctionDeclaration Setter { get; set; }
        public FunctionDeclaration Materializer { get; set; }
        public bool IsAsync => Getter.IsAsync;
    }
}

