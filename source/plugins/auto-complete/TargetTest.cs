// Copyright (C) 2008 Jesse Jones
//
// Permission is hereby granted, free of charge, to any person obtaining
// a copy of this software and associated documentation files (the
// "Software"), to deal in the Software without restriction, including
// without limitation the rights to use, copy, modify, merge, publish,
// distribute, sublicense, and/or sell copies of the Software, and to
// permit persons to whom the Software is furnished to do so, subject to
// the following conditions:
// 
// The above copyright notice and this permission notice shall be
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
// MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
// LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
// OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
// WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

#if TEST
using NUnit.Framework;
using Shared;
using System;
using System.Collections.Generic;
//using System.IO;
//using System.Linq;

namespace AutoComplete
{
	[TestFixture]
	public sealed class TargetTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			AssertListener.Install();
		}
		
		private bool DoGetType(string text, string target, int offset)
		{
Console.WriteLine("------------------------------------");
Console.WriteLine(text);

			var database = new MockTargetDatabase();
			var locals = new CsParser.LocalsParser();
			m_target = new Target(database, locals);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			return m_target.FindType(text, target, offset, globals);
		}
		
		[Test]
		public void ThisCall1()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		this.
	}

	public void Process(int alpha)
	{		
	}

	public static void StaticMethod()
	{
	}
}
";
			bool found = DoGetType(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("MyClass", m_target.FullTypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
		}
		
		[Test]
		public void ThisCall2()
		{
			string text = @"
namespace CoolLib
{
	internal sealed class MyClass
	{
		public void Work(int alpha)
		{
			this.
		}
	
		public void Process(int alpha)
		{		
		}
	
		public static void StaticMethod()
		{
		}
	}
}
";
			bool found = DoGetType(text, "this", text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("CoolLib.MyClass", m_target.FullTypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
		}
		
		[Test]
		public void TargetInsideComment()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		// a comment with this.
	}
}
";
			bool found = DoGetType(text, "this", text.IndexOf("."));
			Assert.IsFalse(found);
		}
		
		// TODO:
		// this inside a comment
		// this inside a string
		// value in an indexer setter
		// value in a property setter
		// value in a normal method
		// LocalType.
		// DatabaseType.
		// local.
		// arg.
		// field.
		// base-field.		
		private Target m_target;
	}
}
#endif	// TEST
