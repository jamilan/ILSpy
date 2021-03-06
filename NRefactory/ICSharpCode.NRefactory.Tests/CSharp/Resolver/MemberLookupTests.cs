﻿// Copyright (c) AlphaSierraPapa for the SharpDevelop Team
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy of this
// software and associated documentation files (the "Software"), to deal in the Software
// without restriction, including without limitation the rights to use, copy, modify, merge,
// publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons
// to whom the Software is furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all copies or
// substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED,
// INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR
// PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE
// FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
// OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.

using System;
using System.IO;
using System.Linq;

using ICSharpCode.NRefactory.CSharp.TypeSystem;
using ICSharpCode.NRefactory.Semantics;
using ICSharpCode.NRefactory.TypeSystem;
using NUnit.Framework;

namespace ICSharpCode.NRefactory.CSharp.Resolver
{
	[TestFixture]
	public class MemberLookupTests : ResolverTestBase
	{
		MemberLookup lookup;
		
		public override void SetUp()
		{
			base.SetUp();
			lookup = new MemberLookup(null, compilation.MainAssembly);
		}
		
		CSharpParsedFile Parse(string program)
		{
			CompilationUnit cu = new CSharpParser().Parse(new StringReader(program), "test.cs");
			CSharpParsedFile parsedFile = cu.ToTypeSystem();
			project = project.UpdateProjectContent(null, parsedFile);
			compilation = project.CreateCompilation();
			return parsedFile;
		}
		
		[Test]
		public void GroupMethodsByDeclaringType()
		{
			string program = @"
class Base {
	public virtual void Method() {}
}
class Middle : Base {
	public void Method(int p) {}
}
class Derived : Middle {
	public override void Method() {}
}";
			var parsedFile = Parse(program);
			ITypeDefinition derived = compilation.MainAssembly.GetTypeDefinition(parsedFile.TopLevelTypeDefinitions[2]);
			var rr = lookup.Lookup(new ResolveResult(derived), "Method", EmptyList<IType>.Instance, true) as MethodGroupResolveResult;
			Assert.AreEqual(2, rr.MethodsGroupedByDeclaringType.Count());
			
			var baseGroup = rr.MethodsGroupedByDeclaringType.ElementAt(0);
			Assert.AreEqual("Base", baseGroup.DeclaringType.ReflectionName);
			Assert.AreEqual(1, baseGroup.Count);
			Assert.AreEqual("Derived.Method", baseGroup[0].FullName);
			
			var middleGroup = rr.MethodsGroupedByDeclaringType.ElementAt(1);
			Assert.AreEqual("Middle", middleGroup.DeclaringType.ReflectionName);
			Assert.AreEqual(1, middleGroup.Count);
			Assert.AreEqual("Middle.Method", middleGroup[0].FullName);
		}
		
		[Test]
		public void MethodInGenericClassOverriddenByConcreteMethod()
		{
			string program = @"
class Base<T> {
	public virtual void Method(T a) {}
}
class Derived : Base<int> {
	public override void Method(int a) {}
	public override void Method(string a) {}
}";
			var parsedFile = Parse(program);
			ITypeDefinition derived = compilation.MainAssembly.GetTypeDefinition(parsedFile.TopLevelTypeDefinitions[1]);
			var rr = lookup.Lookup(new ResolveResult(derived), "Method", EmptyList<IType>.Instance, true) as MethodGroupResolveResult;
			Assert.AreEqual(2, rr.MethodsGroupedByDeclaringType.Count());
			
			var baseGroup = rr.MethodsGroupedByDeclaringType.ElementAt(0);
			Assert.AreEqual("Base`1[[System.Int32]]", baseGroup.DeclaringType.ReflectionName);
			Assert.AreEqual(1, baseGroup.Count);
			Assert.AreEqual("Derived.Method", baseGroup[0].FullName);
			Assert.AreEqual("System.Int32", baseGroup[0].Parameters[0].Type.ReflectionName);
			
			var derivedGroup = rr.MethodsGroupedByDeclaringType.ElementAt(1);
			Assert.AreEqual("Derived", derivedGroup.DeclaringType.ReflectionName);
			Assert.AreEqual(1, derivedGroup.Count);
			Assert.AreEqual("Derived.Method", derivedGroup[0].FullName);
			Assert.AreEqual("System.String", derivedGroup[0].Parameters[0].Type.ReflectionName);
		}
		
		[Test]
		public void GenericMethod()
		{
			string program = @"
class Base {
	public virtual void Method<T>(T a) {}
}
class Derived : Base {
	public override void Method<S>(S a) {}
}";
			var parsedFile = Parse(program);
			ITypeDefinition derived = compilation.MainAssembly.GetTypeDefinition(parsedFile.TopLevelTypeDefinitions[1]);
			var rr = lookup.Lookup(new ResolveResult(derived), "Method", EmptyList<IType>.Instance, true) as MethodGroupResolveResult;
			Assert.AreEqual(1, rr.MethodsGroupedByDeclaringType.Count());
			
			var baseGroup = rr.MethodsGroupedByDeclaringType.ElementAt(0);
			Assert.AreEqual("Base", baseGroup.DeclaringType.ReflectionName);
			Assert.AreEqual(1, baseGroup.Count);
			Assert.AreEqual("Derived.Method", baseGroup[0].FullName);
			Assert.AreEqual("``0", baseGroup[0].Parameters[0].Type.ReflectionName);
		}
		
		[Test]
		public void TestOuterTemplateParameter()
		{
			string program = @"public class A<T>
{
	public class B
	{
		public T field;
	}
}

public class Foo
{
	public void Bar ()
	{
		A<int>.B baz = new A<int>.B ();
		$baz.field$.ToString ();
	}
}";
			var lrr = Resolve<MemberResolveResult>(program);
			Assert.AreEqual("System.Int32", lrr.Type.FullName);
		}
		
		[Test]
		public void TestOuterTemplateParameterInDerivedClass()
		{
			string program = @"public class A<T>
{
	public class B
	{
		public T field;
	}
}

public class Foo : A<int>.B
{
	public void Bar ()
	{
		$field$.ToString ();
	}
}";
			var lrr = Resolve<MemberResolveResult>(program);
			Assert.AreEqual("System.Int32", lrr.Type.FullName);
		}
		
		[Test]
		public void TestOuterTemplateParameterInDerivedClass2()
		{
			string program = @"public class A<T>
{
	public class B
	{
		public T field;
	}
}

public class Foo : A<int>
{
	public void Bar (B v)
	{
		$v.field$.ToString ();
	}
}";
			var lrr = Resolve<MemberResolveResult>(program);
			Assert.AreEqual("System.Int32", lrr.Type.FullName);
		}
		
		[Test]
		public void MemberInGenericClassReferringToInnerClass()
		{
			string program = @"public class Foo<T> {
	public class TestFoo { }
	public TestFoo Bar = new TestFoo ();
}
public class Test {
	public void SomeMethod (Foo<Test> foo) {
		var f = $foo.Bar$;
	}
}";
			var mrr = Resolve<MemberResolveResult>(program);
			Assert.AreEqual("Foo`1+TestFoo[[Test]]", mrr.Type.ReflectionName);
		}
		
		[Test]
		public void ProtectedBaseMethodCall()
		{
			string program = @"using System;
public class Base {
	protected virtual void M() {}
}
public class Test : Base {
	protected override void M() {
		$base.M()$;
	}
}";
			var rr = Resolve<CSharpInvocationResolveResult>(program);
			Assert.IsFalse(rr.IsError);
			Assert.AreEqual("Base.M", rr.Member.FullName);
		}
		
		[Test]
		public void ProtectedBaseFieldAccess()
		{
			string program = @"using System;
public class Base {
	protected int Field;
}
public class Test : Base {
	public new int Field;
	protected override void M() {
		$base.Field$ = 1;
	}
}";
			var rr = Resolve<MemberResolveResult>(program);
			Assert.IsFalse(rr.IsError);
			Assert.AreEqual("Base.Field", rr.Member.FullName);
		}
		
		[Test]
		public void ThisHasSameTypeAsFieldInGenericClass()
		{
			string program = @"using System;
public struct C<T> {
	public C(C<T> other) {
		$M(this, other)$;
	}
	static void M<T>(T a, T b) {}
}
";
			var rr = Resolve<CSharpInvocationResolveResult>(program);
			Assert.IsFalse(rr.IsError);
			Assert.AreEqual("C`1[[`0]]", rr.Arguments[0].Type.ReflectionName);
			Assert.AreEqual("C`1[[`0]]", rr.Arguments[1].Type.ReflectionName);
		}
		
		[Test]
		public void ProtectedFieldInOuterClass()
		{
			string program = @"using System;
class Base {
  protected int X;
}
class Derived : Base {
  class Inner {
     public int M(Derived d) { return $d.X$; }
}}";
			var rr = Resolve<MemberResolveResult>(program);
			Assert.IsFalse(rr.IsError);
			Assert.AreEqual("Base.X", rr.Member.FullName);
		}
	}
}
