#nullable enable

using System;
using System.Diagnostics.CodeAnalysis;


namespace Java.NativeAOT
{
	[AttributeUsage (AttributeTargets.Method, AllowMultiple = false)]
	public sealed class JniMethodSignatureAttribute : JniMemberSignatureAttribute {

		public JniMethodSignatureAttribute (string memberName, string memberSignature)
			: base (memberName, memberSignature)
		{
		}
	}
}
