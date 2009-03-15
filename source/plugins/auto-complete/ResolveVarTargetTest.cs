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

namespace AutoComplete
{
	[TestFixture]
	public sealed class ResolveVarTargetTest
	{	
		[TestFixtureSetUp]
		public void Init()
		{
			AssertListener.Install();
		}
		
		private bool DoGetTarget(string text, string target, int offset, MockTargetDatabase database)
		{
//Console.WriteLine("------------------------------------");
//Console.WriteLine(target);
//Console.WriteLine(text);
			
			var locals = new CsParser.LocalsParser();
			var resolver = new ResolveTarget(database, locals);
			
			var parser = new CsParser.Parser();
			CsGlobalNamespace globals = parser.Parse(text);
			
			m_target = resolver.Resolve(text, target, offset, globals).First;
			return m_target != null;
		}
				
		[Test]
		public void Arg()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = alpha;
		xxx.
	}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int32", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Local()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		long beta = 10;
		var x = beta;
		xxx.
	}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void Prop()
		{
			string text = @"
internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = Weight;
		xxx.
	}
	
	public long Weight {get; set;}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Int64", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Int64", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void New1()
		{
			string text = @"
using System;

internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = new String();
		xxx.
	}
	
	public long Weight {get; set;}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.String", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.String", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void New2()
		{
			string text = @"
using System;

internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = new string(""foobar"");
		xxx.
	}
	
	public long Weight {get; set;}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.String", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.String", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void New3()
		{
			string text = @"
using System;
using System.Collections.Generic;

internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = new Dictionary<int, string>();
		xxx.
	}
	
	public long Weight {get; set;}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Collections.Generic.Dictionary`2", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Collections.Generic.Dictionary`2", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		[Test]
		public void New4()
		{
			string text = @"
using System;
using System.Collections.Generic;

internal sealed class MyClass
{
	public void Work(int alpha)
	{
		var x = new Dictionary<int, List<string>>();
		xxx.
	}
	
	public long Weight {get; set;}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Collections.Generic.Dictionary`2", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Collections.Generic.Dictionary`2", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
				
		[Test]
		public void New5()
		{
			string text = @"
using System;
using System.Collections.Generic;

internal sealed class MyClass
{
	public void Work(int[] alpha)
	{
		var x = from a in alpha select a.ToString();
		xxx.
	}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"System.Collections.Generic.IEnumerable`1", "00-01"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("System.Collections.Generic.IEnumerable`1", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
				
		[Test]
		public void Call1()
		{
			string text = @"
using System;
using System.Collections.Generic;

internal sealed class MyClass
{
	public void Work(Boss boss)
	{
		var x = boss.Get<IFoo>();
		xxx.
	}
}
";
			bool found = DoGetTarget(text, "x", text.IndexOf("xxx"), new MockTargetDatabase
			{
				Hashes = new Dictionary<string, string>
				{
					{"System.Int32", "00-01"},
					{"IFoo", "00-02"},
				}
			});
			Assert.IsTrue(found);
			Assert.AreEqual("IFoo", m_target.FullName);
			Assert.IsNull(m_target.Type);
		}
		
		private ResolvedTarget m_target;
	}
}
#endif	// TEST
