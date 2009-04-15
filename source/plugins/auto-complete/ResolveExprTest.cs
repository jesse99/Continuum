// Copyright (C) 2009 Jesse Jones
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
using System.Diagnostics;

namespace AutoComplete
{
	[TestFixture]
	public sealed class ResolveExprTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			AssertListener.Install();
			Log.SetLevel(TraceLevel.Verbose);
		}
		
		private bool DoGetTheTarget(string text, int offset, MockTargetDatabase database)
		{			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			var locals = new CsParser.LocalsParser();
			var nameResolver = new ResolveName(database, locals, text, offset, globals);
			var resolver = new ResolveExpr(database, globals, nameResolver);
			
			m_target = resolver.Resolve(text, offset);
			return m_target != null;
		}
		
		private bool DoGetTarget(string text, int offset, MockTargetDatabase database)
		{
			Log.WriteLine("AutoComplete", "{0} {1} {2}", new string('-', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('-', 10));
			
			return DoGetTheTarget(text, offset, database);
		}
		
		private bool DoGetTarget(string text, int offset)
		{
			Log.WriteLine("AutoComplete", "{0} {1} {2}", new string('-', 10), new StackTrace().GetFrame(1).GetMethod().Name, new string('-', 10));
			
			var database = new MockTargetDatabase();
			return DoGetTheTarget(text, offset, database);
		}
		
		[Test]
		public void SimpleNames()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		this.
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("."));
			Assert.IsTrue(found);
			Assert.AreEqual("MyClass", m_target.TypeName);
			Assert.AreEqual("MyClass", m_target.Type.Name);
		}
		
		[Test]
		public void BogusNames()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		0x1FF.
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("."));
			Assert.IsFalse(found);
		}
		
		[Test]
		public void QualifiedType()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		System.Collections.List-
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("-"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Collections.List",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Collections.List", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void DbMethod()
		{
			string text = @"
internal partial class MyClass
{
	public void Work(int alpha)
	{
		Process(alpha, Math.Max(alpha - 100, 10))~
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("~"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"MyClass",
				},
				
				Members = new Dictionary<string, Member[]>
				{
					{"MyClass", new Member[]
					{
						new Member("Process(int x;int y)", 2, "System.Int32", "MyClass"),
					}}
				},
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void NestedGeneric()
		{
			string text = @"
internal partial class MyClass
{
	public void Work(int alpha)
	{
		Process(alpha, Foo<int, float>(alpha - 100, 10))~
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("~"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"MyClass",
				},
				
				Members = new Dictionary<string, Member[]>
				{
					{"MyClass", new Member[]
					{
						new Member("Process(int x;int y)", 2, "System.Int32", "MyClass"),
					}}
				},
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void ExtraSpaces()
		{
			string text = @"
internal partial class MyClass
{
	public void Work(int alpha)
	{
		Process 	(
			alpha, Math.Max(alpha - 100, 10)
			 )~
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("~"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"MyClass",
				},
				
				Members = new Dictionary<string, Member[]>
				{
					{"MyClass", new Member[]
					{
						new Member("Process(int x;int y)", 2, "System.Int32", "MyClass"),
					}}
				},
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void ChainedCall()
		{
			string text = @"
internal partial class MyClass
{
	public void Work(int alpha)
	{
		Alpha(alpha).Beta (1, foo(1, 4) ). Gamma(null)~
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("~"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Int32",
					"System.Boolean",
					"MyClass",
				},
				
				Members = new Dictionary<string, Member[]>
				{
					{"MyClass", new Member[]
					{
						new Member("Alpha(int x)", 1, "MyClass", "MyClass"),
						new Member("Beta(int x;int y)", 2, "MyClass", "MyClass"),
						new Member("Gamma(object value)", 1, "System.Boolean", "MyClass"),
					}}
				},
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Boolean", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void CharLiterals()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		'x'-
		'\xdeadbeef'!
		'\''@
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("-"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Char", m_target.TypeName);
			Assert.IsNull(m_target.Type);

			found = DoGetTarget(text, text.IndexOf("!"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Char", m_target.TypeName);
			Assert.IsNull(m_target.Type);

			found = DoGetTarget(text, text.IndexOf("@"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Char", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void StringLiterals()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		""x""-
		""\xdeadbeef""!
		""""@
	}
}
";
			bool found = DoGetTarget(text, text.IndexOf("-"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.String", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, text.IndexOf("!"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.String", m_target.TypeName);
			Assert.IsNull(m_target.Type);
			
			found = DoGetTarget(text, text.IndexOf("@"), new MockTargetDatabase
			{
				Types = new List<string>
				{
					"System.Char",
					"System.String",
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.String", m_target.TypeName);
			Assert.IsNull(m_target.Type);
		}
		
		private ResolvedTarget m_target;
	}
}
#endif	// TEST
